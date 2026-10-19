using System.IO.Compression;
using System.Text.Json;
namespace BackupReminder;
internal static class NamedAssetVerification
{
    static void ImageFile(string path,Color color)
    {
        using var image=new Bitmap(12,12);using(var graphics=Graphics.FromImage(image))graphics.Clear(color);
        image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
    internal static void Run(string root,Action<bool,string> check)
    {
        string source=Path.Combine(root,"named-sources");Directory.CreateDirectory(Path.Combine(source,"a"));Directory.CreateDirectory(Path.Combine(source,"b"));
        string a=Path.Combine(source,"a","My icon.png"),b=Path.Combine(source,"b","My icon.png");ImageFile(a,Color.Red);ImageFile(b,Color.Blue);
        var profile=new Profile(Path.Combine(root,"named-assets"));var draft=profile.CopySettings();
        foreach(var item in draft.Items.Concat(draft.Groups))item.IconPath=null;
        draft.Items[0].IconPath=a;draft.Items[1].IconPath=b;profile.SaveSettings(draft);
        check(profile.Settings.Items[0].IconPath=="icons/My icon.png"&&profile.Settings.Items[1].IconPath=="icons/My icon (2).png","Different icons with the same original name get readable numbered filenames");
        check(File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(profile.ResolveAsset("icons/My icon.png")))&&File.ReadAllBytes(b).SequenceEqual(File.ReadAllBytes(profile.ResolveAsset("icons/My icon (2).png"))),"Original icon bytes and filenames are preserved without overwriting collisions");
        string duplicate=Path.Combine(source,"copy.png");File.Copy(a,duplicate);
        draft=profile.CopySettings();draft.Items[2].IconPath=duplicate;draft.Items[2].IconName=null;profile.SaveSettings(draft);
        check(profile.Settings.Items[2].IconPath==profile.Settings.Items[0].IconPath&&profile.Settings.ManagedFiles.Count==2,"Identical imported artwork is reused even with a different source filename");
        string wave=Path.Combine(source,"Chime.wav");SoundVerification.WriteWave(wave);
        Directory.CreateDirectory(Path.Combine(profile.DirectoryPath,"sounds"));string occupied=profile.ResolveAsset("sounds/Chime.wav");File.Copy(wave,occupied);
        var bytes=File.ReadAllBytes(wave);bytes[^1]=127;File.WriteAllBytes(wave,bytes);
        draft=profile.CopySettings();draft.ReminderSoundPath=wave;draft.ReminderSoundName=null;profile.SaveSettings(draft);
        check(profile.Settings.ReminderSoundPath=="sounds/Chime (2).wav"&&profile.Settings.ReminderSoundName=="Chime (2).wav"&&File.ReadAllBytes(occupied)[^1]==128,"Different sounds sharing an occupied filename are numbered and preserve manually placed files");
        string backup=Path.Combine(root,"named-assets.zip");ProfileBackup.Export(profile,backup);
        using(var zip=ZipFile.OpenRead(backup))check(zip.GetEntry("icons/My icon.png") is not null&&zip.GetEntry("sounds/Chime (2).wav") is not null,"Configuration backups keep readable icon and sound filenames");
        var target=new Profile(Path.Combine(root,"named-restored"));ProfileBackup.Restore(target,backup,new MemoryReminderScheduler());
        check(target.Settings.Items[1].IconPath=="icons/My icon (2).png"&&target.Settings.ReminderSoundPath=="sounds/Chime (2).wav","Named asset references survive configuration restore into another profile");
        string corrupt=Path.Combine(root,"named-corrupt.zip");File.Copy(backup,corrupt);
        using(var zip=ZipFile.Open(corrupt,ZipArchiveMode.Update))
        {zip.GetEntry("icons/My icon.png")!.Delete();using var file=zip.CreateEntry("icons/My icon.png").Open();file.Write("changed"u8);}
        bool rejected=false;try{ProfileBackup.Read(corrupt);}catch(InvalidDataException){rejected=true;}
        check(rejected,"Named assets use manifest checksums to reject damaged backup contents");
        // Real version-1 archive plus legacy ownership data exercises upgrade compatibility.
        var legacy=new Profile(Path.Combine(root,"hashed-assets"));var old=legacy.CopySettings();foreach(var item in old.Items.Concat(old.Groups))item.IconPath=null;
        byte[] iconBytes=File.ReadAllBytes(a),waveBytes=File.ReadAllBytes(wave);
        string oldIcon="icons/br-"+ProfileAssets.Hash(iconBytes)+".png",oldSound="sounds/br-"+ProfileAssets.Hash(waveBytes)+".wav";
        Directory.CreateDirectory(Path.Combine(legacy.DirectoryPath,"icons"));Directory.CreateDirectory(Path.Combine(legacy.DirectoryPath,"sounds"));
        File.WriteAllBytes(legacy.ResolveAsset(oldIcon),iconBytes);File.WriteAllBytes(legacy.ResolveAsset(oldSound),waveBytes);
        old.Items[0].Title="FileZilla";old.Items[0].IconPath=oldIcon;old.ReminderSoundPath=oldSound;old.ReminderSoundName="Notice.wav";old.ManagedFiles=[oldIcon,oldSound];
        File.WriteAllText(legacy.SettingsPath,JsonSerializer.Serialize(old,Profile.Json));legacy=new Profile(legacy.DirectoryPath);
        string oldBackup=Path.Combine(root,"version-one.zip");
        using(var zip=ZipFile.Open(oldBackup,ZipArchiveMode.Create))
        {
            void Put(string name,byte[] data){using var file=zip.CreateEntry(name).Open();file.Write(data);}
            Put("settings.json",JsonSerializer.SerializeToUtf8Bytes(old,Profile.Json));Put("checklist.json",JsonSerializer.SerializeToUtf8Bytes(new ChecklistState(),Profile.Json));
            Put("backup.json",JsonSerializer.SerializeToUtf8Bytes(new{format="BackupReminder",version=1},Profile.Json));Put(oldIcon,iconBytes);Put(oldSound,waveBytes);
        }
        var oldTarget=new Profile(Path.Combine(root,"v1-restored"));ProfileBackup.Restore(oldTarget,oldBackup,new MemoryReminderScheduler());
        check(oldTarget.Settings.Items[0].IconPath=="icons/FileZilla.png"&&oldTarget.Settings.ReminderSoundPath=="sounds/Notice.wav","Old hashed version-1 backups restore with readable filenames");
        using(var locked=new FileStream(legacy.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed=false;try{legacy.SaveSettings(legacy.CopySettings());}catch(Exception error) when(error is IOException or UnauthorizedAccessException){failed=true;}
            check(failed&&File.Exists(legacy.ResolveAsset(oldIcon))&&File.Exists(legacy.ResolveAsset(oldSound))&&!File.Exists(legacy.ResolveAsset("icons/FileZilla.png")),"Failed filename migration preserves old files and rolls back new copies");
        }
        legacy.SaveSettings(legacy.CopySettings());
        check(legacy.Settings.Items[0].IconPath=="icons/FileZilla.png"&&legacy.Settings.ReminderSoundPath=="sounds/Notice.wav"&&!File.Exists(legacy.ResolveAsset(oldIcon))&&!File.Exists(legacy.ResolveAsset(oldSound)),"Successful save migrates old hashed references and removes only their unchanged app-owned copies");
        File.WriteAllText(legacy.ResolveAsset("icons/FileZilla.png"),"owner changed this");
        draft=legacy.CopySettings();draft.Items[0].IconPath=null;legacy.SaveSettings(draft);
        check(File.Exists(legacy.ResolveAsset("icons/FileZilla.png")),"Cleanup preserves an app-imported file whose contents the owner later changed");
        string ui=Path.Combine(root,"named-icon-ui");using(var window=new MainWindow(new Profile(ui),Options.Parse(["--data-directory",ui,"--self-test",root])))window.VerifyNamedIconEditor(a,check);
        string ordering=Path.Combine(root,"empty-group-ui");using(var window=new MainWindow(new Profile(ordering),Options.Parse(["--data-directory",ordering,"--self-test",root])))window.VerifyEmptyGroupMovement(check);
    }
}
internal sealed partial class MainWindow
{
    internal void VerifyEmptyGroupMovement(Action<bool,string> check)
    {
        Show();Application.DoEvents();string groupId;
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();var tree=Descendants<TreeView>(editor).Single();
            var up=Descendants<Button>(editor).Single(x=>x.Name=="MoveUp");var down=Descendants<Button>(editor).Single(x=>x.Name=="MoveDown");
            Descendants<Button>(editor).Single(x=>x.Name=="AddCategory").PerformClick();groupId=tree.SelectedNode!.Name;
            check(up.Enabled&&!down.Enabled,"New unsaved empty group can move up from the bottom");
            up.PerformClick();check(tree.SelectedNode!.Index==2&&tree.SelectedNode.Nodes.Count==0,"Empty group moves above a populated group before Save");
            down.PerformClick();check(tree.SelectedNode!.Index==3,"Empty group moves down again without saving");
            up.PerformClick();up.PerformClick();up.PerformClick();
            check(tree.SelectedNode!.Index==0&&!up.Enabled&&down.Enabled,"Empty group can move past standalone tasks and stops at the top");
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();editor.Close();
        }
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();var tree=Descendants<TreeView>(editor).Single();
            tree.SelectedNode=tree.Nodes.Find(groupId,true).Single();
            check(tree.SelectedNode.Index==0,"Saved empty group keeps its position after reopening the editor");
            Descendants<Button>(editor).Single(x=>x.Name=="AddTask").PerformClick();
            check(tree.SelectedNode!.Parent?.Name==groupId&&tree.SelectedNode.Parent.Index==0,"Adding the first task keeps the moved group's position");
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();
            check(profile.Settings.Items[0].Group==groupId,"Saving the first task retains group order on the main checklist");editor.Close();
        }
        foreach(string target in new[]{"knownfolder:Documents","knownfolder:Pictures",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)})
        {
            using var picker=CreateTargetFolderPicker(target);string resolved=Profile.ResolveTarget(target);
            check(picker.InitialDirectory==resolved&&picker.SelectedPath==resolved,"Folder Browse resolves and preselects the Windows folder for "+target);
        }
        check(profile.Settings.Items.Any(x=>x.Target=="knownfolder:Documents"),"Preparing folder Browse leaves the original known-folder target unchanged");
        Close();
    }
    internal void VerifyNamedIconEditor(string source,Action<bool,string> check)
    {
        Show();Application.DoEvents();
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();
            check(Descendants<Button>(editor).Single(x=>x.Name=="AddCategory").Text=="+ Add group"&&Descendants<EditorPicker>(editor).Single(x=>x.Name=="EntryCategory").ChoiceValue(0)==""&&Descendants<Label>(editor).Any(x=>x.Text=="Group"),"Checklist editor consistently calls categories Groups with a separate-task option");
            var input=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon");input.Text=source;
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();
            var entry=profile.Settings.Items[0];check(input.Text=="My icon.png"&&entry.IconPath=="icons/My icon.png"&&entry.IconName==input.Text,"Checklist custom icon field displays the readable filename after Save");
            using(var picker=new IconSelection(entry.IconPath,entry.IconName).CreatePicker(profile.DirectoryPath,input.Text))
                check(picker.InitialDirectory==Path.Combine(profile.DirectoryPath,"icons")&&picker.FileName==profile.ResolveAsset(entry.IconPath!),"Icon Browse opens in the actual saved icon folder and preselects the file");
            editor.Close();
        }
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();check(Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon").Text=="My icon.png","Reopening the editor retains the friendly icon selection");editor.Close();
        }
        Close();
    }
}
