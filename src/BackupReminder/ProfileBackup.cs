using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace BackupReminder;

internal static class ProfileBackup
{
    const long Limit=100*1024*1024;
    internal static List<string> Export(Profile profile,string destination)
    {
        profile.EnsureSettingsUnchanged();
        var settings=profile.CopySettings();var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        var missing=new List<string>();
        string? Collect(string? path,bool sound,string? preferred,int? iconIndex=null)
        {
            if(string.IsNullOrWhiteSpace(path))return path;
            string source=profile.ResolveAsset(path);
            if(!File.Exists(source)){missing.Add(path);return source;}
            byte[] bytes=ProfileAssets.ReadAsset(source,sound,iconIndex);string folder=sound?"sounds/":"icons/";
            foreach(var (existing,data) in files)
                if(existing.StartsWith(folder,StringComparison.Ordinal)&&data.SequenceEqual(bytes))return existing;
            string name=ProfileAssets.ImportName(source,sound,preferred);
            for(int number=1;number<10000;number++)
            {
                string relative=folder+ProfileAssets.NumberedName(name,number);
                if(files.ContainsKey(relative))continue;
                files[relative]=bytes;return relative;
            }
            throw new InvalidDataException("Too many custom files share the same name.");
        }
        foreach(var item in settings.Items.Concat(settings.Groups))
        {
            item.IconPath=Collect(item.IconPath,false,item.IconName??(ProfileAssets.IsLegacyName(item.IconPath)?item.Title+".png":null),item.IconIndex);
            if(ProfileAssets.IsManagedName(item.IconPath))item.IconIndex=null;
            if(ProfileAssets.IsManagedName(item.IconPath))item.IconName=Path.GetFileName(item.IconPath);
        }
        settings.ReminderSoundPath=Collect(settings.ReminderSoundPath,true,settings.ReminderSoundName);
        if(ProfileAssets.IsManagedName(settings.ReminderSoundPath))settings.ReminderSoundName=Path.GetFileName(settings.ReminderSoundPath);
        settings.ManagedFiles=files.Keys.ToList();
        settings.ManagedFileHashes=files.ToDictionary(x=>x.Key,x=>ProfileAssets.Hash(x.Value),StringComparer.OrdinalIgnoreCase);
        files["settings.json"]=JsonSerializer.SerializeToUtf8Bytes(settings,Profile.Json);
        files["checklist.json"]=JsonSerializer.SerializeToUtf8Bytes(profile.State,Profile.Json);
        files["backup.json"]=JsonSerializer.SerializeToUtf8Bytes(new {format="BackupReminder",version=2,missingFiles=missing,assetHashes=settings.ManagedFileHashes},Profile.Json);
        if(files.Count>1003||files.Values.Any(x=>x.Length>25*1024*1024)||files.Values.Sum(x=>(long)x.Length)>Limit)throw new InvalidDataException("Configuration backup exceeds 100 MB.");
        string full=Path.GetFullPath(destination);
        // A backup must never replace live configuration or one of its assets.
        var protectedFiles=ProfileAssets.References(profile.Settings).Select(profile.ResolveAsset)
            .Append(profile.SettingsPath).Append(profile.StatePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(protectedFiles.Contains(full))throw new IOException("Choose a ZIP filename outside your configuration files.");
        string pending=full+"."+Guid.NewGuid().ToString("N")+".pending";
        try
        {
            using(var archive=ZipFile.Open(pending,ZipArchiveMode.Create))
                foreach(var (name,bytes) in files){using var output=archive.CreateEntry(name,CompressionLevel.Optimal).Open();output.Write(bytes);}
            File.Move(pending,full,true);
        }
        finally{if(File.Exists(pending))File.Delete(pending);}
        return missing;
    }
    internal sealed record Content(ChecklistSettings Settings,ChecklistState State,Dictionary<string,byte[]> Assets);
    internal static Content Read(string file)
    {
        using var archive=ZipFile.OpenRead(file);
        if(archive.Entries.Count>1003)throw new InvalidDataException("Configuration backup contains too many files.");
        var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var entry in archive.Entries)
        {
            string name=entry.FullName;
            if(name is not ("backup.json" or "settings.json" or "checklist.json")&&!ProfileAssets.IsManagedName(name))
                throw new InvalidDataException("Unexpected file in configuration backup: "+name);
            if(name.Contains('\\')||entry.Length>25*1024*1024||(total+=entry.Length)>Limit||files.ContainsKey(name))
                throw new InvalidDataException("Configuration backup has invalid or oversized entries.");
            using var input=entry.Open();using var output=new MemoryStream();
            byte[] buffer=new byte[8192];int count;
            while((count=input.Read(buffer))>0)
            {
                if(output.Length+count>entry.Length)throw new InvalidDataException("Invalid ZIP entry length.");
                output.Write(buffer,0,count);
            }
            files.Add(name,output.ToArray());
        }
        if(!files.ContainsKey("backup.json")||!files.ContainsKey("settings.json")||!files.ContainsKey("checklist.json"))
            throw new InvalidDataException("This is not a Backup Reminder configuration backup.");
        int version;Dictionary<string,string>? expectedHashes=null;
        using(var marker=JsonDocument.Parse(files["backup.json"]))
        {
            if(marker.RootElement.GetProperty("format").GetString()!="BackupReminder"||(version=marker.RootElement.GetProperty("version").GetInt32()) is not (1 or 2))
                throw new InvalidDataException("Unsupported configuration backup version.");
            if(version==2)expectedHashes=JsonSerializer.Deserialize<Dictionary<string,string>>(marker.RootElement.GetProperty("assetHashes").GetRawText(),Profile.Json)
                ??throw new InvalidDataException("Missing backup asset checksums.");
        }
        var settings=ChecklistSettings.Parse(System.Text.Encoding.UTF8.GetString(files["settings.json"]));
        var state=Profile.ParseState(System.Text.Encoding.UTF8.GetString(files["checklist.json"]));
        var assets=files.Where(x=>ProfileAssets.IsManagedName(x.Key)).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
        var references=ProfileAssets.References(settings).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(string path in references)
            if(!Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(path))&&(!ProfileAssets.IsManagedName(path)||!assets.ContainsKey(path)))
                throw new InvalidDataException("Configuration backup is missing a referenced icon or sound.");
        if(version==2&&expectedHashes!.Count!=assets.Count)throw new InvalidDataException("Backup asset checksums do not match its contents.");
        foreach(var (name,bytes) in assets)
        {
            string hash=ProfileAssets.Hash(bytes);
            bool valid=version==1?ProfileAssets.IsLegacyName(name)&&Path.GetFileNameWithoutExtension(name)=="br-"+hash:
                expectedHashes!.TryGetValue(name,out string? expected)&&expected==hash;
            if(!references.Contains(name)||!valid)throw new InvalidDataException("Configuration backup has an unreferenced or damaged asset.");
        }
        return new(settings,state,assets);
    }
    internal static string Restore(Profile profile,string file,IReminderScheduler scheduler)
    {
        var incoming=Read(file);profile.EnsureSettingsUnchanged();
        string staging=Path.Combine(profile.DirectoryPath,".restore-"+Guid.NewGuid().ToString("N"));
        ProfileAssets.CheckDirectory(profile.DirectoryPath,"recovery");
        Directory.CreateDirectory(staging);
        try
        {
            foreach(var (name,bytes) in incoming.Assets)
            {
                string target=Path.Combine(staging,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllBytes(target,bytes);
                _=ProfileAssets.ReadAsset(target,name.StartsWith("sounds/",StringComparison.Ordinal));
            }
            foreach(var item in incoming.Settings.Items.Concat(incoming.Settings.Groups))
                if(item.IconPath is string path&&ProfileAssets.IsManagedName(path))
                {
                    if(ProfileAssets.IsLegacyName(path)&&string.IsNullOrWhiteSpace(item.IconName))item.IconName=item.Title+".png";
                    item.IconPath=Path.Combine(staging,path);
                }
            if(incoming.Settings.ReminderSoundPath is string sound&&ProfileAssets.IsManagedName(sound))incoming.Settings.ReminderSoundPath=Path.Combine(staging,sound);
            incoming.Settings.Schedule??=new();incoming.Settings.Schedule.Enabled=false;
            string recovery=Path.Combine(profile.DirectoryPath,"recovery","before-restore.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(recovery)!);
            // Preserve a single previous configuration; backups contain settings and assets, never programs or task definitions.
            Export(profile,recovery);
            byte[]? previousState=File.Exists(profile.StatePath)?File.ReadAllBytes(profile.StatePath):null;
            var known=incoming.Settings.Items.Select(x=>x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            try
            {
                profile.Save(incoming.State.Checked.Where(known.Contains),incoming.State.LastCompleted,incoming.State.CheckedAt);
                ReminderSettings.Save(profile,incoming.Settings,scheduler);
            }
            catch
            {
                if(previousState is null)File.Delete(profile.StatePath);else File.WriteAllBytes(profile.StatePath,previousState);
                profile.ReloadState();throw;
            }
            return recovery;
        }
        finally{Directory.Delete(staging,true);}
    }
}

internal sealed partial class MainWindow
{
    void SaveCurrentProgress()=>profile.Save(boxes.Where(x=>x.Checked).Select(x=>(string)x.Tag!),profile.State.LastCompleted,checkedTimes);
    void BackUpConfiguration()
    {
        using var picker=new SaveFileDialog {Title="Back up configuration",Filter="Backup Reminder configuration (*.zip)|*.zip",DefaultExt="zip",InitialDirectory=PickerLocations.Documents,RestoreDirectory=true,FileName="Backup-Reminder-config-"+DateTime.Now.ToString("yyyy-MM-dd")+".zip"};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            SaveCurrentProgress();var missing=ProfileBackup.Export(profile,picker.FileName);
            MessageBox.Show(this,"Configuration backup saved. It includes your checklist, progress, icons and sound, but not backup programs or their data."+(missing.Count>0?"\r\n\r\nSome existing custom files were unavailable and could not be included:\r\n"+string.Join("\r\n",missing):""),"Configuration backed up",MessageBoxButtons.OK,missing.Count>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
        }
        catch(Exception error){ShowError("Could not back up configuration: "+error.Message);}
    }
    void RestoreConfiguration()
    {
        using var picker=new OpenFileDialog {Title="Restore configuration",Filter="Backup Reminder configuration (*.zip)|*.zip",InitialDirectory=PickerLocations.Documents,RestoreDirectory=true,CheckFileExists=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            var incoming=ProfileBackup.Read(picker.FileName);
            if(MessageBox.Show(this,$"Replace this profile with {incoming.Settings.Items.Count} tasks from the backup?\r\n\r\nYour current configuration will be preserved in recovery\\before-restore.zip. Reminders will be turned off. Program paths may need adjustment on this PC; enable reminders in Reminder schedule when ready.","Restore configuration",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
            string recovery=ApplyConfigurationBackup(picker.FileName);
            MessageBox.Show(this,"Configuration restored. Check your program paths, then use Settings > Reminder schedule to enable reminders.\r\n\r\nPrevious configuration: "+recovery,"Configuration restored",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception error){ShowError("Could not restore configuration: "+error.Message);}
    }
    string ApplyConfigurationBackup(string file)
    {
        SaveCurrentProgress();string recovery=ProfileBackup.Restore(profile,file,reminderScheduler);
        checkedTimes.Clear();foreach(var pair in profile.State.CheckedAt)checkedTimes[pair.Key]=pair.Value;
        ReloadChecklist(true);RefreshCompletionHeader();return recovery;
    }
    internal void VerifyConfigurationRestore(string archive,Action<bool,string> check)
    {
        Show();Application.DoEvents();
        foreach(var box in boxes)box.Checked=false;
        boxes[1].Checked=true;
        ApplyConfigurationBackup(archive);
        check(boxes[0].Checked&&!boxes[1].Checked&&checkedTimes.ContainsKey((string)boxes[0].Tag!),"Restore refreshes displayed checks and timestamps from the backup, rather than the previous window");
        var label=Controls.OfType<Panel>().SelectMany(x=>x.Controls.OfType<Label>()).Single(x=>x.Name=="LastCompleted");
        check(label.Text.Contains("2026")&&profile.State.LastCompleted is not null,"Restore refreshes the visible last-completed date");
        Close();
    }
    void RefreshCompletionHeader()
    {
        var label=Controls.OfType<Panel>().SelectMany(x=>x.Controls.OfType<Label>()).FirstOrDefault(x=>x.Name=="LastCompleted");
        if(label is not null)label.Text=(options.Development?"Development profile  |  ":"")+"Last completed: "+(profile.State.LastCompleted is null?"No completion recorded":DateTimeOffset.Parse(profile.State.LastCompleted).ToLocalTime().ToString("MMM d, yyyy  h:mm tt"));
    }
}
