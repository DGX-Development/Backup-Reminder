using System.Media;
using System.Runtime.InteropServices;

namespace BackupReminder;
internal interface IReminderAttention
{
    bool IsForeground(Form owner);
    bool PlaySound(string? customPath);
    void Highlight(Form owner,Form target);
}
internal sealed class WindowsReminderAttention:IReminderAttention
{
    [DllImport("user32.dll")]static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]static extern nint GetAncestor(nint window,uint flags);
    [StructLayout(LayoutKind.Sequential)]
    struct FlashInfo {public uint Size;public nint Window;public uint Flags;public uint Count;public uint Timeout;}
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]static extern bool FlashWindowEx(ref FlashInfo info);
    public bool IsForeground(Form owner)=>GetAncestor(GetForegroundWindow(),3)==owner.Handle;
    [DllImport("winmm.dll",CharSet=CharSet.Unicode,EntryPoint="PlaySoundW")]
    [return:MarshalAs(UnmanagedType.Bool)]static extern bool PlayWave(string path,nint module,uint flags);
    public bool PlaySound(string? customPath)=>ReminderSound.Play(customPath,path=>PlayWave(path,0,0x20003),()=>SystemSounds.Asterisk.Play());
    public void Highlight(Form owner,Form target)
    {
        // Dialogs share the owner's taskbar button. Highlight it without changing focus or flashing content.
        var info=new FlashInfo {Size=(uint)Marshal.SizeOf<FlashInfo>(),Window=owner.Handle,Flags=2,Count=3,Timeout=0};
        _=FlashWindowEx(ref info);
    }
}
internal sealed class RecordingReminderAttention:IReminderAttention
{
    internal bool Foreground {get;set;}
    internal int Sounds {get;private set;}
    internal int Highlights {get;private set;}
    internal Form? Target {get;private set;}
    public bool IsForeground(Form owner)=>Foreground;
    internal string? SoundPath {get;private set;}
    public bool PlaySound(string? customPath){Sounds++;SoundPath=customPath;return !string.IsNullOrWhiteSpace(customPath);}
    public void Highlight(Form owner,Form target){Highlights++;Target=target;}
}
// Friendly display is independent of the profile-relative storage path.
internal sealed record ReminderSoundSelection(string? Path,string? Name)
{
    internal static string? FileName(string? path)
    {
        string? name=string.IsNullOrWhiteSpace(path)?null:System.IO.Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name)?null:name;
    }
    internal string DisplayText=>string.IsNullOrWhiteSpace(Path)?"":ProfileAssets.IsManagedName(Path)?FileName(Name)??(ProfileAssets.IsLegacyName(Path)?"Saved custom sound":FileName(Path)??"Saved custom sound"):Path;
    internal string SelectedPath(string text)=>text==DisplayText?Path??text:text.Trim();
    internal string? SelectedName(string text)=>text==DisplayText?
        FileName(Name)??(Path is not null&&!ProfileAssets.IsManagedName(Path)?FileName(Path):null):FileName(text.Trim());
    internal OpenFileDialog CreatePicker(string directory,string text)=>AssetPicker.Create(directory,SelectedPath(text),true);
}
internal sealed record IconSelection(string? Path,string? Name,int? Index=null)
{
    internal int? SelectedIndex(string text)=>text==DisplayText?Index:null;
    internal string DisplayText=>string.IsNullOrWhiteSpace(Path)?"":Index is not null?System.IO.Path.GetFileName(Path)+" (icon "+(Index+1)+")":ProfileAssets.IsManagedName(Path)?ReminderSoundSelection.FileName(Name)??(ProfileAssets.IsLegacyName(Path)?"Saved custom icon":ReminderSoundSelection.FileName(Path)??"Saved custom icon"):Path;
    internal string SelectedPath(string text)=>text==DisplayText?Path??text:text.Trim();
    internal string? SelectedName(string text)=>text==DisplayText?ReminderSoundSelection.FileName(Name):ReminderSoundSelection.FileName(text.Trim());
    internal OpenFileDialog CreatePicker(string directory,string text)=>AssetPicker.Create(directory,SelectedPath(text),false);
}
internal static class AssetPicker
{
    internal static OpenFileDialog Create(string directory,string selected,bool sound)
    {
        string folder="",file="";
        try
        {
            if(!string.IsNullOrWhiteSpace(selected))
            {
                string resolved=ProfileAssets.Resolve(directory,selected);
                string? parent=Directory.Exists(resolved)?resolved:System.IO.Path.GetDirectoryName(resolved);
                if(parent is not null&&Directory.Exists(parent))folder=parent;
                if(File.Exists(resolved))file=resolved;
            }
        }
        catch(ArgumentException){}catch(IOException){}catch(UnauthorizedAccessException){}
        if(folder.Length==0)
        {
            string fallback=sound?System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Media"):System.IO.Path.Combine(directory,"icons");
            folder=Directory.Exists(fallback)?fallback:Environment.GetFolderPath(sound?Environment.SpecialFolder.MyDocuments:Environment.SpecialFolder.MyPictures);
        }
        return new OpenFileDialog {Title=sound?"Choose reminder sound":"Choose custom icon",Filter=sound?"WAV sound files (*.wav)|*.wav":"Icons and images|*.png;*.ico;*.exe;*.dll;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",CheckFileExists=true,RestoreDirectory=true,InitialDirectory=folder,FileName=file};
    }

}
internal static class ReminderSound
{
    internal static void ValidatePath(string? path)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||!string.Equals(Path.GetExtension(path),".wav",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a .wav sound file with a full path, or select Windows sound.");
    }
    internal static string? Problem(string? path)
    {
        try
        {
            ValidatePath(path);
            using var file=File.OpenRead(path!);Span<byte> header=stackalloc byte[12];
            int count=file.ReadAtLeast(header,12,throwOnEndOfStream:false);
            if(count==12&&header[..4].SequenceEqual("RIFF"u8)&&header[8..].SequenceEqual("WAVE"u8))return null;
            string? format=count>=4&&header[..4].SequenceEqual("OggS"u8)?"Ogg":
                count>=4&&header[..4].SequenceEqual("fLaC"u8)?"FLAC":
                count>=3&&header[..3].SequenceEqual("ID3"u8)?"MP3":null;
            return format is not null?$"This file contains {format} audio despite its .wav name. Convert it to PCM WAV; renaming the file does not convert its audio.":
                "This file is not a standard RIFF/WAVE sound. Export or convert it as PCM WAV; changing its extension is not enough.";
        }
        catch(FileNotFoundException){return "Sound file not found. Choose an existing .wav file.";}
        catch(DirectoryNotFoundException){return "Sound folder not found. Choose an existing .wav file.";}
        catch(UnauthorizedAccessException){return "The sound file cannot be read. Choose a file you can access.";}
        catch(IOException){return "The sound file could not be read. Check that it is available and not locked.";}
        catch(ArgumentException){return "Choose a .wav sound file using Browse or enter its full path.";}
        catch(InvalidDataException error){return error.Message;}
    }
    internal static bool IsWave(string? path)=>Problem(path) is null;
    internal static void ValidateWave(string path)
    {
        if(Problem(path) is string problem)throw new InvalidDataException(problem);
    }
    // Failed custom playback must fall back once, without blocking or canceling a reminder.
    internal static bool Play(string? path,Func<string,bool> wave,Action windows)
    {
        try{if(IsWave(path)&&wave(path!))return true;}catch{ }
        try{windows();}catch{ }
        return false;
    }
}
internal sealed partial class MainWindow
{
    internal void VerifyReminderAttention(Action<bool,string> check)
    {
        Show();Application.DoEvents();
        var attention=(RecordingReminderAttention)reminderAttention;
        var settingsBytes=File.ReadAllBytes(profile.SettingsPath);var originalLast=profile.State.LastCompleted;
        check(attention.Sounds==0&&attention.Highlights==0,"Manual startup is quiet");
        ActivateWindow();check(attention.Sounds==0&&attention.Highlights==0,"Manual reactivation is quiet");
        SetFirstCheck(true);attention.Foreground=false;
        HandleScheduledReminder();
        check(attention.Sounds==1&&attention.Highlights==1&&attention.Target==this,"Background scheduled reminder requests one sound and one taskbar highlight");
        attention.Foreground=true;HandleScheduledReminder();
        check(attention.Sounds==2&&attention.Highlights==1,"Foreground scheduled reminder plays once without highlighting the taskbar");
        profile.Settings.ReminderSoundPath="custom.wav";HandleScheduledReminder();
        check(attention.SoundPath==profile.ResolveAsset("custom.wav")&&attention.Sounds==3,"Scheduled reminders pass the saved custom sound to playback");
        profile.Settings.ReminderSoundPath=null;
        profile.Settings.PlayReminderSound=false;attention.Foreground=false;HandleScheduledReminder();
        check(attention.Sounds==3&&attention.Highlights==2,"Sound opt-out keeps the visual reminder");
        using(var editor=CreateChecklistEditor())
        {
            Exception? error=null;using var timer=new System.Windows.Forms.Timer {Interval=80};
            timer.Tick+=(_,_)=>
            {
                timer.Stop();
                try
                {
                    Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="Unsaved edit";
                    HandleScheduledReminder();
                    check(attention.Target==editor&&editor.Modal&&!WindowStyle.IsWindowEnabled(Handle)&&Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text=="Unsaved edit","Scheduled attention preserves the active dialog and its unsaved edit");
                }
                catch(Exception failure){error=failure;}
                finally{editor.Close();}
            };
            timer.Start();ShowOwnedDialog(editor);if(error is not null)throw error;
        }
        check(boxes[0].Checked&&profile.State.LastCompleted==originalLast&&settingsBytes.SequenceEqual(File.ReadAllBytes(profile.SettingsPath)),"Scheduled reminders preserve unfinished checks, completion date and configuration");
        string checkedId=(string)boxes[0].Tag!;
        Close();
        var reopened=new Profile(profile.DirectoryPath);
        check(reopened.State.Checked.Contains(checkedId)&&reopened.State.LastCompleted==originalLast,"Closing an unfinished session saves its checkmarks without recording completion");
    }
    internal void HandleScheduledReminder()
    {
        // Each scheduled launch reaches here once. Preserve checkmarks and any open edit/dialog.
        var target=VisibleDialog()??this;
        if(profile.Settings.PlayReminderSound)reminderAttention.PlaySound(string.IsNullOrWhiteSpace(profile.Settings.ReminderSoundPath)?null:profile.ResolveAsset(profile.Settings.ReminderSoundPath));
        if(!reminderAttention.IsForeground(this))reminderAttention.Highlight(this,target);
    }
}
