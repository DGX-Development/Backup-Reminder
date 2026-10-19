namespace BackupReminder;

internal static class PickerLocations
{
    internal static string Documents=>Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    internal static string Resolve(string directory,string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return "";
        try
        {
            string path=Profile.ResolveTarget(value.Trim().Trim('"'));
            if(Uri.TryCreate(path,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https")return "";
            return Path.GetFullPath(path,directory);
        }
        catch(ArgumentException){}catch(NotSupportedException){}catch(IOException){}catch(System.Security.SecurityException){}
        return "";
    }
    internal static string Folder(string directory,string? value,bool parent=false)
    {
        string resolved=Resolve(directory,value);
        if(Directory.Exists(resolved))return resolved;
        if(parent&&resolved.Length>0)
        {
            string? folder=Path.GetDirectoryName(resolved);
            if(Directory.Exists(folder))return folder!;
        }
        return "";
    }
    internal static OpenFileDialog Program(string directory,string? target)
    {
        string resolved=Resolve(directory,target),folder=Folder(directory,target,true);
        if(folder.Length==0)folder=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return new OpenFileDialog {Title="Choose a program or file",CheckFileExists=true,RestoreDirectory=true,
            Filter="Programs and files|*.exe;*.lnk;*.bat;*.cmd;*.ffs_gui;*.ffs_batch|All files|*.*",
            InitialDirectory=folder,FileName=File.Exists(resolved)?resolved:""};
    }
    internal static FolderBrowserDialog WorkingDirectory(string directory,string? current,string? target)
    {
        string folder=Folder(directory,current);
        if(folder.Length==0)folder=Folder(directory,target,true);
        if(folder.Length==0)folder=Documents;
        return new FolderBrowserDialog {Description="Choose working directory",UseDescriptionForTitle=true,InitialDirectory=folder,SelectedPath=folder};
    }
}
