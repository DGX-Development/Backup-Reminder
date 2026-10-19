using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BackupReminder;

// Content hashes prove ownership; readable filenames are independent of those hashes.
internal sealed class ProfileAssets : IDisposable
{
    readonly Profile profile;
    readonly List<string> created=[];
    readonly Dictionary<string,string> imported=new(StringComparer.OrdinalIgnoreCase);
    bool committed;
    internal static readonly HashSet<string> IconExtensions=new(StringComparer.OrdinalIgnoreCase){".png",".ico",".jpg",".jpeg",".bmp",".gif"};
    internal ProfileAssets(Profile profile) => this.profile=profile;
    internal static string Resolve(string directory,string path)
    {
        path=Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathFullyQualified(path)?path:Path.GetFullPath(Path.Combine(directory,path));
    }
    internal static bool IsLegacyName(string? path)=>path is not null&&Regex.IsMatch(path.Replace('\\','/'),@"^(icons|sounds)/br-[a-f0-9]{64}\.(png|wav)$");
    internal static bool IsManagedName(string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return false;
        var parts=path.Replace('\\','/').Split('/');
        if(parts.Length!=2||parts[0] is not ("icons" or "sounds")||parts[1].Length==0||parts[1].TrimEnd(' ','.')!=parts[1]||parts[1].IndexOfAny(Path.GetInvalidFileNameChars())>=0)return false;
        if(Regex.IsMatch(parts[1],@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\.)",RegexOptions.IgnoreCase))return false;
        return parts[0]=="sounds"?Path.GetExtension(parts[1]).Equals(".wav",StringComparison.OrdinalIgnoreCase):IconExtensions.Contains(Path.GetExtension(parts[1]));
    }
    internal static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static Dictionary<string,string> Owned(ChecklistSettings settings)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (name,hash) in settings.ManagedFileHashes)
            if(IsManagedName(name)&&hash is not null&&Regex.IsMatch(hash,@"^[a-f0-9]{64}$"))result[name.Replace('\\','/')]=hash;
        foreach(string name in settings.ManagedFiles)
            if(IsLegacyName(name))result.TryAdd(name.Replace('\\','/'),Path.GetFileNameWithoutExtension(name)[3..]);
        return result;
    }
    internal static string ImportName(string source,bool sound,string? preferred=null)
    {
        string extension=sound?".wav":IconExtensions.Contains(Path.GetExtension(source))?Path.GetExtension(source):".png";
        string name=Path.GetFileName(string.IsNullOrWhiteSpace(preferred)?source:preferred);
        if(IsLegacyName((sound?"sounds/":"icons/")+name)||string.IsNullOrWhiteSpace(name))name=sound?"Custom sound.wav":"Custom icon.png";
        string stem=Path.GetFileNameWithoutExtension(name);
        foreach(char c in Path.GetInvalidFileNameChars())stem=stem.Replace(c,'_');
        stem=stem.Trim().TrimEnd('.');if(stem.Length==0)stem=sound?"Custom sound":"Custom icon";
        if(Regex.IsMatch(stem,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\.)",RegexOptions.IgnoreCase))stem="Custom "+stem;
        if(stem.Length>100)stem=stem[..100];
        return stem+extension;
    }
    internal static string NumberedName(string name,int number)=>number==1?name:Path.GetFileNameWithoutExtension(name)+" ("+number+")"+Path.GetExtension(name);
    internal static void CheckDirectory(string root,string folder)
    {
        string current=Path.GetFullPath(root);
        while(!string.IsNullOrEmpty(current))
        {
            if(Directory.Exists(current)&&new DirectoryInfo(current).LinkTarget is not null)
                throw new IOException("Configuration asset folders cannot use directory links.");
            current=Path.GetDirectoryName(current)!;
        }
        string target=Path.Combine(root,folder);
        if(Directory.Exists(target)&&new DirectoryInfo(target).LinkTarget is not null)
            throw new IOException("Configuration asset folders cannot use directory links.");
    }
    internal static byte[] ReadAsset(string source,bool sound,int? iconIndex=null)
    {
        var file=new FileInfo(source);
        string extension=Path.GetExtension(source).ToLowerInvariant();
        if((sound||extension is not (".exe" or ".dll" or ".lnk"))&&file.Length>25*1024*1024)throw new InvalidDataException("Custom icons and sounds must be smaller than 25 MB.");
        if(sound)
        {
            ReminderSound.ValidateWave(source);
            return File.ReadAllBytes(source);
        }
        using var output=new MemoryStream();
        if(extension is ".exe" or ".dll" or ".lnk")
        {
            using var bitmap=iconIndex is int index?ShellIcons.Read(source,index):ShellIcons.Associated(source);bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);
        }
        else
        {
            using var input=File.OpenRead(source);
            using var image=Image.FromStream(input,true,true);
            if((long)image.Width*image.Height>16_000_000)throw new InvalidDataException("Choose an icon image smaller than 16 megapixels.");
            if(IconExtensions.Contains(extension))return File.ReadAllBytes(source);
            using var bitmap=new Bitmap(image);bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);
        }
        if(output.Length>25*1024*1024)throw new InvalidDataException("The converted icon exceeds 25 MB.");
        return output.ToArray();
    }
    internal string Import(string path,bool sound,string? preferred=null,int? iconIndex=null)
    {
        string source=Resolve(profile.DirectoryPath,path);
        if(!File.Exists(source))return path;
        string folder=sound?"sounds":"icons";CheckDirectory(profile.DirectoryPath,folder);
        byte[] bytes=ReadAsset(source,sound,iconIndex);string hash=Hash(bytes);
        var owned=Owned(profile.Settings);
        if(IsManagedName(path)&&!IsLegacyName(path))
        {
            string normal=path.Replace('\\','/');
            if(owned.TryGetValue(normal,out string? expected)&&expected==hash)imported[normal]=hash;
            return normal;
        }
        string name=ImportName(source,sound,preferred);
        // Reuse identical app-owned assets, while migrating old opaque names.
        foreach(var (existing,expected) in owned.Concat(imported).ToArray())
        {
            if(!existing.StartsWith(folder+"/",StringComparison.Ordinal)||IsLegacyName(existing)||expected!=hash)continue;
            string file=Resolve(profile.DirectoryPath,existing);
            if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)==0&&Hash(File.ReadAllBytes(file))==hash){imported[existing]=hash;return existing;}
        }
        Directory.CreateDirectory(Path.Combine(profile.DirectoryPath,folder));
        for(int number=1;number<10000;number++)
        {
            string relative=folder+"/"+NumberedName(name,number);string target=Resolve(profile.DirectoryPath,relative);
            if(Directory.Exists(target))continue;
            if(File.Exists(target))
            {
                if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0||!File.ReadAllBytes(target).SequenceEqual(bytes))continue;
                if(imported.ContainsKey(relative)||owned.TryGetValue(relative,out string? expected)&&expected==hash)imported[relative]=hash;
                return relative;
            }
            using(var file=new FileStream(target,FileMode.CreateNew,FileAccess.Write)){created.Add(target);file.Write(bytes);file.Flush(true);}
            imported[relative]=hash;return relative;
        }
        throw new IOException("Too many custom files share this name. Choose another filename.");
    }
    internal void Prepare(ChecklistSettings draft)
    {
        foreach(var entry in draft.Items.Concat(draft.Groups))
        {
            if(string.IsNullOrWhiteSpace(entry.IconPath)){entry.IconName=null;continue;}
            entry.IconPath=Import(entry.IconPath,false,entry.IconName??(IsLegacyName(entry.IconPath)?entry.Title+".png":null),entry.IconIndex);
            if(IsManagedName(entry.IconPath))entry.IconIndex=null;
            if(IsManagedName(entry.IconPath)&&!IsLegacyName(entry.IconPath))entry.IconName=Path.GetFileName(entry.IconPath);
        }
        if(!string.IsNullOrWhiteSpace(draft.ReminderSoundPath))
        {
            draft.ReminderSoundPath=Import(draft.ReminderSoundPath,true,draft.ReminderSoundName);
            if(IsManagedName(draft.ReminderSoundPath)&&!IsLegacyName(draft.ReminderSoundPath))draft.ReminderSoundName=Path.GetFileName(draft.ReminderSoundPath);
        }
        else draft.ReminderSoundName=null;
        var referenced=References(draft).ToHashSet(StringComparer.OrdinalIgnoreCase);
        draft.ManagedFileHashes=imported.Where(x=>referenced.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
        draft.ManagedFiles=draft.ManagedFileHashes.Keys.ToList();
    }
    internal static IEnumerable<string> References(ChecklistSettings settings)=>settings.Items.Concat(settings.Groups)
        .Select(x=>x.IconPath).Append(settings.ReminderSoundPath).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!);
    internal void Commit(ChecklistSettings previous,ChecklistSettings saved)
    {
        committed=true;
        var referenced=References(saved).Select(x=>Resolve(profile.DirectoryPath,x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var (name,expected) in Owned(previous))
        {
            try
            {
                string file=Resolve(profile.DirectoryPath,name);CheckDirectory(profile.DirectoryPath,name.Split('/')[0]);
                if(referenced.Contains(file)||!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                if(Hash(File.ReadAllBytes(file))==expected)File.Delete(file);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    public void Dispose()
    {
        if(committed)return;
        foreach(var file in created){try{File.Delete(file);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
