using System.IO.Compression;
using System.Text.Json;
namespace BackupReminder;

internal static class AssetVerification
{
    internal static void Run(string root,Action<bool,string> check)
    {
        string directory=Path.Combine(root,"managed-assets");var profile=new Profile(directory);
        string source=Path.Combine(root,"original.png"),wave=Path.Combine(root,"original.wav");
        using(var bitmap=new Bitmap(16,16)){using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.Green);bitmap.Save(source);}
        using(var output=new BinaryWriter(File.Create(wave)))
        {
            output.Write("RIFF"u8);output.Write(38);output.Write("WAVEfmt "u8);output.Write(16);output.Write((short)1);output.Write((short)1);
            output.Write(8000);output.Write(8000);output.Write((short)1);output.Write((short)8);output.Write("data"u8);output.Write(2);output.Write(new byte[]{128,128});
        }
        var draft=profile.CopySettings();foreach(var item in draft.Items.Concat(draft.Groups))item.IconPath=null;draft.Items[0].IconPath=source;draft.Groups[0].IconPath=source;draft.ReminderSoundPath=wave;
        check(!Directory.Exists(Path.Combine(directory,"icons")),"Choosing unsaved customizations creates no imported files");
        profile.SaveSettings(draft);
        string icon=profile.Settings.Items[0].IconPath!,sound=profile.Settings.ReminderSoundPath!;
        check(ProfileAssets.IsManagedName(icon)&&ProfileAssets.IsManagedName(sound)&&profile.Settings.Groups[0].IconPath==icon&&profile.Settings.ManagedFiles.Count==2,"Save imports and deduplicates custom icons and WAV with relative paths");
        check(File.Exists(source)&&File.Exists(wave),"Import preserves original custom files");
        File.Delete(source);File.Delete(wave);
        var reopened=new Profile(directory);
        check(File.Exists(reopened.ResolveAsset(icon))&&ReminderSound.IsWave(reopened.ResolveAsset(sound)),"Managed files survive loss of their originals and a profile reopen");
        reopened.Save([reopened.Settings.Items[0].Id],"2026-10-18T10:00:00-05:00",new Dictionary<string,string>{{reopened.Settings.Items[0].Id,"2026-10-18T09:00:00-05:00"}});
        string backup=Path.Combine(root,"configuration.zip");ProfileBackup.Export(reopened,backup);
        var content=ProfileBackup.Read(backup);
        check(content.Assets.Count==2&&content.State.Checked.Count==1,"Configuration ZIP contains referenced assets and checked progress");
        var target=new Profile(Path.Combine(root,"restored-profile"));var scheduler=new MemoryReminderScheduler();scheduler.Apply(new ReminderSchedule {Enabled=true});
        string recovery=ProfileBackup.Restore(target,backup,scheduler);
        var restored=new Profile(target.DirectoryPath);
        check(restored.Settings.Schedule?.Enabled==false&&!scheduler.Read().Enabled&&File.Exists(recovery),"Restore disables the owned reminder and preserves one prior configuration ZIP");
        check(restored.State.Checked.SequenceEqual(reopened.State.Checked)&&restored.State.CheckedAt.Count==1&&restored.State.LastCompleted==reopened.State.LastCompleted&&restored.Settings.ManagedFiles.Count==2,"Restore preserves completion, check timestamps and asset ownership");
        check(File.Exists(restored.ResolveAsset(restored.Settings.Items[0].IconPath!))&&ReminderSound.IsWave(restored.ResolveAsset(restored.Settings.ReminderSoundPath!)),"Restored icons and sound work in a different profile directory");
        string manual=Path.Combine(target.DirectoryPath,"icons","manual.png");File.Copy(target.ResolveAsset(target.Settings.Items[0].IconPath!),manual);
        draft=target.CopySettings();foreach(var item in draft.Items.Concat(draft.Groups))item.IconPath=null;draft.ReminderSoundPath=null;target.SaveSettings(draft);
        check(File.Exists(manual)&&!File.Exists(target.ResolveAsset(icon))&&!File.Exists(target.ResolveAsset(sound)),"Cleanup removes only unreferenced app-owned assets and retains manually placed files");
        // An EXE selection must become an image, not a copied executable.
        draft=target.CopySettings();draft.Items[0].IconPath=Environment.ProcessPath;target.SaveSettings(draft);
        check(target.Settings.Items[0].IconPath!.EndsWith(".png")&&!Directory.GetFiles(Path.Combine(target.DirectoryPath,"icons"),"*.exe").Any(),"Custom icons selected from executables are extracted as PNG images");
        string before=File.ReadAllText(target.SettingsPath);draft=target.CopySettings();draft.Items[0].IconPath=manual;
        using(var locked=new FileStream(target.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed=false;try{target.SaveSettings(draft);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){failed=true;}
            check(failed&&File.ReadAllText(target.SettingsPath)==before&&!File.Exists(target.ResolveAsset(icon)),"Failed configuration save rolls back newly imported files");
        }
        // A rejected restore must not alter existing files or reminder state.
        scheduler.Apply(new ReminderSchedule {Enabled=true});
        byte[] stateBefore=File.ReadAllBytes(target.StatePath);
        using(var locked=new FileStream(target.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed=false;try{ProfileBackup.Restore(target,backup,scheduler);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){failed=true;}
            check(failed&&File.ReadAllText(target.SettingsPath)==before&&File.ReadAllBytes(target.StatePath).SequenceEqual(stateBefore)&&scheduler.Read().Enabled,"Failed restore preserves configuration, progress and scheduled reminder");
        }
        string evil=Path.Combine(root,"invalid-config.zip");File.Copy(backup,evil);
        using(var zip=ZipFile.Open(evil,ZipArchiveMode.Update)){using var output=new StreamWriter(zip.CreateEntry("../escaped.txt").Open());output.Write("unexpected");}
        bool rejected=false;try{ProfileBackup.Restore(target,evil,scheduler);}catch(InvalidDataException){rejected=true;}
        check(rejected&&File.ReadAllText(target.SettingsPath)==before&&!File.Exists(Path.Combine(root,"escaped.txt")),"Restore rejects traversal and unexpected files before changing configuration");
        string duplicate=Path.Combine(root,"duplicate-config.zip");File.Copy(backup,duplicate);
        using(var zip=ZipFile.Open(duplicate,ZipArchiveMode.Update)){using var output=new StreamWriter(zip.CreateEntry("settings.json").Open());output.Write("{}");}
        rejected=false;try{ProfileBackup.Read(duplicate);}catch(InvalidDataException){rejected=true;}
        check(rejected,"Restore rejects duplicate ZIP entries");
        draft=target.CopySettings();draft.Items[0].IconPath=Path.Combine(root,"legacy-missing.png");target.SaveSettings(draft);
        check(target.Settings.Items[0].IconPath==draft.Items[0].IconPath,"Missing legacy custom paths remain readable for fallback");
        var missing=ProfileBackup.Export(target,Path.Combine(root,"missing-assets.zip"));
        check(missing.Count==1,"Configuration export explicitly reports unavailable external files");
        check(!Directory.EnumerateDirectories(target.DirectoryPath,".restore-*").Any(),"Restore staging is removed after success and failure");
        string uiDirectory=Path.Combine(root,"restore-ui");
        using(var window=new MainWindow(new Profile(uiDirectory),Options.Parse(["--data-directory",uiDirectory,"--self-test",root])))window.VerifyConfigurationRestore(backup,check);
        string legacyDirectory=Path.Combine(root,"beta-upgrade");Directory.CreateDirectory(legacyDirectory);
        string legacyIcon=Path.Combine(root,"legacy-icon.png");File.Copy(manual,legacyIcon);
        var legacySettings=ChecklistSettings.Parse(Profile.Resource("starter.json"));legacySettings.Items[0].IconPath=legacyIcon;
        legacySettings.Schedule=new ReminderSchedule {Enabled=true,DayOfMonth=18,TimeOfDay="00:02"};
        File.WriteAllText(Path.Combine(legacyDirectory,"settings.json"),JsonSerializer.Serialize(legacySettings,Profile.Json));
        var legacyProfile=new Profile(legacyDirectory);legacyProfile.Save([legacySettings.Items[0].Id],null);
        byte[] legacyState=File.ReadAllBytes(legacyProfile.StatePath);
        legacyProfile.SaveSettings(legacyProfile.CopySettings());
        check(ProfileAssets.IsManagedName(legacyProfile.Settings.Items[0].IconPath!)&&File.Exists(legacyIcon)&&legacyProfile.Settings.Schedule?.Enabled==true&&legacyProfile.Settings.Schedule.DayOfMonth==18&&legacyProfile.Settings.Schedule.TimeOfDay=="00:02"&&File.ReadAllBytes(legacyProfile.StatePath).SequenceEqual(legacyState),"Beta configuration upgrades external artwork without changing reminder timing or saved progress");
    }
}
