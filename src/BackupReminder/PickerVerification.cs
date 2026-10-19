namespace BackupReminder;

internal static class PickerVerification
{
    internal static void Run(string root,Action<bool,string> check)
    {
        string directory=Path.Combine(root,"picker-defaults");Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"sample program.exe");File.WriteAllText(file,"picker location only");
        using(var picker=PickerLocations.Program(directory,""))check(picker.InitialDirectory==Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)&&picker.FileName==""&&picker.RestoreDirectory,"Blank Program/file Browse explicitly starts in Program Files");
        using(var picker=PickerLocations.Program(directory,file))check(picker.InitialDirectory==directory&&picker.FileName==file,"Program/file Browse opens beside and selects its existing target");
        using(var picker=PickerLocations.Program(directory,Path.Combine(directory,"missing.exe")))check(picker.InitialDirectory==directory&&picker.FileName=="","Missing program uses its existing parent folder without selecting a nonexistent file");
        using(var picker=PickerLocations.Program(directory,"%WINDIR%\\System32\\notepad.exe"))check(picker.InitialDirectory==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32"),"Program/file Browse resolves environment variables");
        using(var picker=PickerLocations.WorkingDirectory(directory,"",file))check(picker.InitialDirectory==directory&&picker.SelectedPath==directory,"Blank Working directory Browse uses the selected program folder");
        using(var picker=PickerLocations.WorkingDirectory(directory,"",""))check(picker.InitialDirectory==PickerLocations.Documents&&picker.SelectedPath==PickerLocations.Documents,"Working directory without a program explicitly starts in Documents");
        using(var picker=PickerLocations.WorkingDirectory(directory,Environment.GetFolderPath(Environment.SpecialFolder.Windows),file))check(picker.SelectedPath==Environment.GetFolderPath(Environment.SpecialFolder.Windows),"An existing working directory takes priority over the program folder");
        using(var picker=MainWindow.CreateTargetFolderPicker(""))check(picker.InitialDirectory==PickerLocations.Documents&&picker.SelectedPath==PickerLocations.Documents,"Blank Folder Browse explicitly starts in Documents");
        using(var picker=MainWindow.CreateTargetFolderPicker("knownfolder:Documents"))check(picker.SelectedPath==PickerLocations.Documents,"Folder Browse resolves redirected Windows Documents");
        check(ShellIcons.InitialSource(directory,"",null)==ShellIcons.DefaultLibrary&&ShellIcons.Count(ShellIcons.DefaultLibrary)>1,"Blank custom icon opens a selectable Windows shell32 icon library");
        string ui=Path.Combine(root,"picker-ui");using var window=new MainWindow(new Profile(ui),Options.Parse(["--data-directory",ui,"--self-test",root]));window.VerifyPickers(root,check);
    }
}
internal sealed partial class MainWindow
{
    internal void VerifyPickers(string root,Action<bool,string> check)
    {
        Show();Application.DoEvents();
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();
            Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();
            var group=Descendants<EditorPicker>(editor).Single(x=>x.Name=="EntryCategory");group.SelectedIndex=0;
            check(group.Text=="None (show as a separate task)"&&group.Text.All(c=>c<128),"Separate-task label is plain ASCII without encoding-sensitive punctuation");
            var target=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryTarget");target.Text="";
            var icon=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon");string before=icon.Text;
            void Choose(bool cancel)
            {
                Exception? failure=null;using var timer=new System.Windows.Forms.Timer {Interval=100};
                timer.Tick+=(_,_)=>
                {
                    var picker=editor.OwnedForms.FirstOrDefault(x=>x.Text=="Choose custom icon");if(picker is null)return;timer.Stop();
                    try
                    {
                        var list=Descendants<ListView>(picker).Single(x=>x.Name=="IconChoices");
                        var browse=Descendants<Button>(picker).Single(x=>x.Name=="BrowseIconSource");
                        var source=Descendants<TextBox>(picker).Single(x=>x.Name=="IconSource");
                        check(browse.Height>=32&&browse.Bottom<=browse.Parent!.ClientSize.Height&&browse.Top==source.Parent!.Top,"Icon source Browse button is fully visible and aligned with its source field");
                        check(picker.Modal&&picker.Owner==editor&&!WindowStyle.IsWindowEnabled(editor.Handle)&&list.Items.Count>1,"Icon gallery is a modal child of the editor with selectable Windows icons");
                        if(icon.Text=="shell32.dll (icon 2)")check(((IconChoice)list.SelectedItems[0].Tag!).Index==1,"Reopening icon Browse retains the selected draft library icon");
                        if(cancel)Descendants<Button>(picker).Single(x=>x.Name=="CancelIcon").PerformClick();
                        else
                        {
                            list.Items[0].Selected=false;list.Items[list.Items.Count-1].Selected=true;
                            var viewport=Descendants<EditorScrollArea>(picker).Single();
                            check(viewport.MaximumOffset>0&&viewport.ScrollOffset>0&&list.GetItemRect(list.Items.Count-1).Bottom<=viewport.ScrollOffset+viewport.ContentViewport.Height,"Icon gallery scrolls to the final icon using its themed scrollbar");
                            list.Items[list.Items.Count-1].Selected=false;list.Items[1].Selected=true;
                            using var screenshot=new Bitmap(picker.Width,picker.Height);picker.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));screenshot.Save(Path.Combine(root,"icon-chooser.png"));
                            Descendants<Button>(picker).Single(x=>x.Name=="UseIcon").PerformClick();
                        }
                    }
                    catch(Exception error){failure=error;picker.Close();}
                };
                timer.Start();Descendants<Button>(editor).Single(x=>x.Name=="BrowseIcon").PerformClick();if(failure is not null)throw failure;
            }
            Choose(true);check(icon.Text==before&&!Directory.Exists(Path.Combine(profile.DirectoryPath,"icons")),"Canceling icon choice leaves the field and profile assets unchanged");
            Choose(false);Choose(true);check(icon.Text=="shell32.dll (icon 2)"&&!Directory.Exists(Path.Combine(profile.DirectoryPath,"icons")),"Choosing a Windows icon previews a draft without importing files");
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();var saved=profile.Settings.Items[0];
            check(saved.IconPath=="icons/shell32 icon 2.png"&&saved.IconIndex is null&&File.Exists(profile.ResolveAsset(saved.IconPath)),"Save extracts the selected library icon as a readable managed PNG");
            using var expected=ShellIcons.Read(ShellIcons.DefaultLibrary,1);using var actual=new Bitmap(profile.ResolveAsset(saved.IconPath!));
            bool same=expected.Size==actual.Size;
            for(int y=0;same&&y<expected.Height;y++)for(int x=0;x<expected.Width;x++)if(expected.GetPixel(x,y)!=actual.GetPixel(x,y)){same=false;break;}
            check(same,"Saved PNG is the selected library icon rather than the library's default icon");
            editor.Close();
        }
        string archive=Path.Combine(root,"picked-icon.zip");ProfileBackup.Export(profile,archive);var restored=new Profile(Path.Combine(root,"picked-icon-restored"));ProfileBackup.Restore(restored,archive,new MemoryReminderScheduler());
        check(File.ReadAllBytes(profile.ResolveAsset(profile.Settings.Items[0].IconPath!)).SequenceEqual(File.ReadAllBytes(restored.ResolveAsset(restored.Settings.Items[0].IconPath!))),"Selected Windows icon survives configuration backup and restore");
        Close();
    }
}
