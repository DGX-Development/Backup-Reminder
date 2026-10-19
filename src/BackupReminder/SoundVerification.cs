using System.Text.Json;
namespace BackupReminder;
internal static class SoundVerification
{
    internal static void WriteWave(string file)
    {
        using var output=new BinaryWriter(File.Create(file));
        output.Write("RIFF"u8);output.Write(38);output.Write("WAVEfmt "u8);output.Write(16);output.Write((short)1);output.Write((short)1);
        output.Write(8000);output.Write(8000);output.Write((short)1);output.Write((short)8);output.Write("data"u8);output.Write(2);output.Write(new byte[]{128,128});
    }
    internal static void Run(string root,Action<bool,string> check)
    {
        string directory=System.IO.Path.Combine(root,"sound-labels");var profile=new Profile(directory);
        string source=System.IO.Path.Combine(directory,"Original chime.wav");WriteWave(source);
        string disguised=System.IO.Path.Combine(directory,"not-really-wave.wav");File.WriteAllBytes(disguised,"OggS\0\0\0\0\0\0\0\0"u8.ToArray());
        check(ReminderSound.Problem(disguised)?.Contains("Ogg")==true&&!ReminderSound.IsWave(disguised),"A renamed Ogg file is identified by its contents with specific conversion guidance");
        bool rejected=false;try{ProfileAssets.ReadAsset(disguised,true);}catch(InvalidDataException error){rejected=error.Message.Contains("Ogg")&&error.Message.Contains("PCM WAV");}
        check(rejected,"Saving an Ogg file named WAV reports the actual format instead of a generic valid-WAV error");
        check(ReminderSound.Problem(source) is null,"Standard PCM WAV remains accepted");
        check(ReminderSound.Problem(System.IO.Path.Combine(directory,"missing.wav"))?.Contains("not found")==true,"Missing sound files have a distinct diagnostic");
        using(var window=new MainWindow(profile,Options.Parse(["--data-directory",directory,"--self-test",root])))window.VerifySoundSelection(source,disguised,check);
        var reopened=new Profile(directory);string archive=System.IO.Path.Combine(root,"friendly-sound.zip");ProfileBackup.Export(reopened,archive);
        var restored=new Profile(System.IO.Path.Combine(root,"friendly-sound-restored"));ProfileBackup.Restore(restored,archive,new MemoryReminderScheduler());
        check(restored.Settings.ReminderSoundName=="Original chime.wav"&&new ReminderSoundSelection(restored.Settings.ReminderSoundPath,restored.Settings.ReminderSoundName).DisplayText=="Original chime.wav","Original sound filename survives configuration ZIP backup and restore");
        var namedSelection=new ReminderSoundSelection(reopened.Settings.ReminderSoundPath,reopened.Settings.ReminderSoundName);
        using(var picker=namedSelection.CreatePicker(directory,namedSelection.DisplayText))
            check(picker.InitialDirectory==System.IO.Path.Combine(directory,"sounds")&&picker.FileName==reopened.ResolveAsset(reopened.Settings.ReminderSoundPath!)&&picker.RestoreDirectory,"Browse resolves the friendly label to the managed sound folder and selects the real file");
        using(var picker=namedSelection.CreatePicker(directory,disguised))
            check(picker.InitialDirectory==directory&&picker.FileName==disguised,"Browse starts beside a newly entered unsaved full path");
        using(var picker=new ReminderSoundSelection(null,null).CreatePicker(directory,""))
            check(picker.InitialDirectory==System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Media")&&picker.FileName.Length==0,"An empty sound selection opens Browse in Windows Media");
        using(var picker=namedSelection.CreatePicker(directory,System.IO.Path.Combine(directory,"nonexistent","missing.wav")))
            check(picker.InitialDirectory==System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Media"),"An unavailable sound folder falls back to Windows Media");
        var selection=new ReminderSoundSelection(reopened.Settings.ReminderSoundPath,null);
        check(selection.DisplayText=="Original chime.wav"&&selection.SelectedPath(selection.DisplayText)==reopened.Settings.ReminderSoundPath,"Named managed sounds remain readable without display-name metadata");
        string other=System.IO.Path.Combine(root,"elsewhere","Original chime.wav");
        check(new ReminderSoundSelection(reopened.Settings.ReminderSoundPath,reopened.Settings.ReminderSoundName).SelectedPath(other)==other,"Pasting a new full path with the same filename selects the new file");
    }
}
internal sealed partial class MainWindow
{
    internal void VerifySoundSelection(string source,string disguised,Action<bool,string> check)
    {
        Show();Application.DoEvents();
        using(var schedule=CreateScheduleSettings())
        {
            schedule.Show(this);Application.DoEvents();
            Descendants<Button>(schedule).Single(x=>x.Name=="SoundOptions").PerformClick();
            var choice=Descendants<EditorPicker>(schedule).Single(x=>x.Name=="ReminderSoundKind");choice.SelectedIndex=1;
            var field=Descendants<TextBox>(schedule).Single(x=>x.Name=="ReminderSoundPath");
            var hint=Descendants<Label>(schedule).Single(x=>x.Name=="ReminderSoundHint");
            var preview=Descendants<Button>(schedule).Single(x=>x.Name=="PreviewReminderSound");
            var save=Descendants<Button>(schedule).Single(x=>x.Name=="SaveSchedule");
            var attention=(RecordingReminderAttention)reminderAttention;
            field.Text=disguised;int sounds=attention.Sounds;preview.PerformClick();
            check(hint.Text.Contains("Ogg")&&hint.Text.Contains("PCM WAV")&&attention.Sounds==sounds,"Preview identifies incompatible content without falsely playing or reporting the custom file");
            field.Text=source;preview.PerformClick();
            check(attention.SoundPath==source,"Unsaved custom sound Preview uses the selected full path");
            save.PerformClick();string managed=profile.Settings.ReminderSoundPath!;
            check(field.Text=="Original chime.wav"&&profile.Settings.ReminderSoundName==field.Text&&ProfileAssets.IsManagedName(managed)&&save.Text=="Saved"&&!save.Enabled,"Save shows the original filename while storing the managed relative sound path separately");
            File.Delete(source);preview.PerformClick();
            check(attention.SoundPath==profile.ResolveAsset(managed)&&save.Text=="Saved"&&!save.Enabled,"Preview uses the saved copy after the original is removed without marking the schedule dirty");
            var enabled=Descendants<CheckBox>(schedule).Single(x=>x.Name=="PlayReminderSound");enabled.Checked=false;save.PerformClick();
            check(profile.Settings.ReminderSoundPath==managed&&profile.Settings.ReminderSoundName=="Original chime.wav","Saving another preference retains the managed sound and its original filename");
            schedule.Close();
        }
        using(var schedule=CreateScheduleSettings())
        {
            schedule.Show(this);Application.DoEvents();
            check(Descendants<TextBox>(schedule).Single(x=>x.Name=="ReminderSoundPath").Text=="Original chime.wav","Reopening Reminder schedule keeps the friendly filename");
            schedule.Close();
        }
        Close();
    }
}
