using System.Globalization;

namespace BackupReminder;

internal sealed partial class MainWindow
{
    internal static List<ChecklistEntry> RootEntries(ChecklistSettings settings)
    {
        var natural=new List<ChecklistEntry>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in settings.Items)
        {
            var entry=string.IsNullOrEmpty(item.Group)?item:settings.Groups.Single(g=>string.Equals(g.Id,item.Group,StringComparison.OrdinalIgnoreCase));
            if(seen.Add(entry.Id))natural.Add(entry);
        }
        foreach(var group in settings.Groups)if(seen.Add(group.Id))natural.Add(group);
        var ordered=new List<ChecklistEntry>();seen.Clear();
        foreach(string id in settings.RootOrder)
        {
            var entry=natural.FirstOrDefault(x=>string.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));
            if(entry is not null&&seen.Add(id))ordered.Add(entry);
        }
        ordered.AddRange(natural.Where(x=>seen.Add(x.Id)));return ordered;
    }
    internal static void NormalizeGroups(ChecklistSettings settings)
    {
        var roots=RootEntries(settings);var groups=settings.Groups.ToHashSet();
        settings.RootOrder=roots.Select(x=>x.Id).ToList();
        settings.Items=roots.SelectMany(x=>groups.Contains(x)?settings.Items.Where(i=>string.Equals(i.Group,x.Id,StringComparison.OrdinalIgnoreCase)):new[]{x}).ToList();
    }
    internal static void MoveRoot(ChecklistSettings draft,string id,int offset)
    {
        var roots=RootEntries(draft);int from=roots.FindIndex(x=>string.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase)),to=from+offset;
        if(from<0||to<0||to>=roots.Count)return;
        (roots[from],roots[to])=(roots[to],roots[from]);draft.RootOrder=roots.Select(x=>x.Id).ToList();NormalizeGroups(draft);
    }
    internal static int MoveTask(ChecklistSettings draft,int from,int offset)
    {
        var selected=draft.Items[from];NormalizeGroups(draft);from=draft.Items.IndexOf(selected);int to=from+offset;
        if(to>=0&&to<draft.Items.Count&&string.Equals(selected.Group,draft.Items[to].Group,StringComparison.OrdinalIgnoreCase))
        {
            (draft.Items[from],draft.Items[to])=(draft.Items[to],draft.Items[from]);
            if(string.IsNullOrEmpty(selected.Group))
            {
                // Separate tasks each occupy their own root position.
                MoveRoot(draft,selected.Id,offset);
            }
        }
        else MoveRoot(draft,selected.Group??selected.Id,offset);
        return draft.Items.IndexOf(selected);
    }
    internal static FolderBrowserDialog CreateTargetFolderPicker(string target)
    {
        string resolved=PickerLocations.Resolve(Environment.CurrentDirectory,target);
        return new FolderBrowserDialog {Description="Choose the folder to open",UseDescriptionForTitle=true,
            InitialDirectory=Directory.Exists(resolved)?resolved:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            SelectedPath=Directory.Exists(resolved)?resolved:PickerLocations.Documents};
    }
    Form CreateScheduleSettings()
    {
        var draft=profile.CopySettings();var saved=draft.Schedule??new ReminderSchedule();
        var (dialog,area,footer)=StyledPage("Reminder schedule","Choose when you want to be reminded about your backup routine.",new Size(740,940));
        var card=new RoundedPanel {Dock=DockStyle.Fill,Padding=new Padding(24,20,24,20)};area.Controls.Add(card);
        var viewport=new EditorScrollArea {Name="ScheduleFields",Dock=DockStyle.Fill};card.Controls.Add(viewport);
        var fields=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Margin=Padding.Empty};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));viewport.SetContent(fields);
        fields.Controls.Add(new Label {Text="Reminder timing",Font=groupFont,Height=36,Dock=DockStyle.Fill,Margin=Padding.Empty});
        var enabled=new CheckBox {Name="EnableReminders",Text="Enable scheduled reminders",Checked=saved.Enabled,Height=40,Dock=DockStyle.Fill,Margin=Padding.Empty};fields.Controls.Add(enabled);
        var status=new Label {Name="ScheduleStatus",Tag="muted",Height=48,Dock=DockStyle.Fill,Margin=Padding.Empty};fields.Controls.Add(status);
        void RefreshStatus(bool applied=false)
        {
            try
            {
                var task=reminderScheduler.Read();
                status.Text=(applied?"Saved. ":"")+(task.Enabled?"Reminder is on. Next run: "+(task.NextRun?.ToString("g")??"not yet available")+" (local time).":"Reminder is off. Enable it and save to create a Windows task.");
                if(task.Enabled&&task.Executable is string path&&!string.Equals(path,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))status.Text+="\r\nSave again to use this executable's location.";
            }
            catch(Exception error){status.Text="Could not read Windows reminder status: "+error.Message;}
        }
        RefreshStatus();
        void Field(string caption,Control input)
        {
            var row=new Panel {Height=76,Dock=DockStyle.Fill,Margin=Padding.Empty};
            row.Controls.Add(new Label {Text=caption,Bounds=new Rectangle(0,0,500,24)});
            var host=new RoundedPanel {Tag="window",Bounds=new Rectangle(0,28,500,42),Padding=new Padding(12,10,12,8)};
            row.SizeChanged+=(_,_)=>host.Width=Math.Max(120,row.ClientSize.Width-2);
            if(input is ThemeNumberInput or ScheduleDateTimeInput)host.Padding=new Padding(12,4,6,4);
            input.Dock=DockStyle.Fill;input.AccessibleName=caption;host.Controls.Add(input);row.Controls.Add(host);fields.Controls.Add(row);
        }
        var mode=new EditorPicker {Name="ScheduleFrequency",Height=42};mode.AddChoice("Monthly","Monthly");mode.AddChoice("Every N days","Interval");mode.SelectedIndex=saved.Mode=="Monthly"?0:1;
        var frequency=new Panel {Height=76,Dock=DockStyle.Fill,Margin=Padding.Empty};frequency.Controls.Add(new Label {Text="Frequency",Bounds=new Rectangle(0,0,500,24)});
        mode.SetBounds(0,28,500,42);frequency.SizeChanged+=(_,_)=>mode.Width=Math.Max(120,frequency.ClientSize.Width-2);frequency.Controls.Add(mode);fields.Controls.Add(frequency);
        var day=new ThemeNumberInput(1,31,saved.DayOfMonth) {Name="ScheduleDay"};Field("Day of month",day);var monthlyRow=day.Parent!.Parent!;
        var interval=new ThemeNumberInput(1,3650,saved.IntervalDays) {Name="ScheduleInterval"};Field("Days between reminders",interval);var intervalRow=interval.Parent!.Parent!;
        var culture=CultureInfo.CurrentCulture;
        var start=new ScheduleDateTimeInput(false,DateTime.ParseExact(saved.StartDate,"yyyy-MM-dd",CultureInfo.InvariantCulture)) {Name="ScheduleStart"};
        Field("Start date",start);var startRow=start.Parent!.Parent!;
        var time=new ScheduleDateTimeInput(true,DateTime.Today.Add(TimeOnly.ParseExact(saved.TimeOfDay,"HH:mm",CultureInfo.InvariantCulture).ToTimeSpan())) {Name="ScheduleTime"};Field("Time",time);
        var summary=new Label {Name="ScheduleSummary",Height=44,Dock=DockStyle.Fill,Margin=new Padding(0,10,0,0)};fields.Controls.Add(summary);
        void RefreshSummary()
        {
            try{summary.Text=mode.SelectedIndex==0
                ?$"Every month on day {day.Value} at {time.Value.ToString("t",culture)}."
                :$"Every {interval.Value} {(interval.Value==1?"day":"days")} starting {start.Value.ToString("d",culture)} at {time.Value.ToString("t",culture)}.";}
            catch(InvalidDataException){summary.Text="Choose a valid date and time to preview your schedule.";}
        }
        day.ValueChanged+=(_,_)=>RefreshSummary();interval.ValueChanged+=(_,_)=>RefreshSummary();
        start.ValueChanged+=(_,_)=>RefreshSummary();time.ValueChanged+=(_,_)=>RefreshSummary();
        var explanation=new Label {Name="ScheduleExplanation",Height=64,Dock=DockStyle.Fill,Tag="muted",Margin=new Padding(0,10,0,0)};fields.Controls.Add(explanation);
        void ModeChanged(){RefreshSummary();bool monthly=mode.SelectedIndex==0;monthlyRow.Visible=monthly;intervalRow.Visible=!monthly;startRow.Visible=!monthly;explanation.Text=(monthly?"Shorter months use their last day.":"Windows tasks support intervals of 1 to 365 days, from the start date.")+"\r\nYou must be signed in. Completing backups early leaves the schedule unchanged.";}
        mode.SelectedIndexChanged+=(_,_)=>ModeChanged();ModeChanged();
        fields.Controls.Add(new Label {Text="Notification",Font=groupFont,Height=38,Dock=DockStyle.Fill,Margin=Padding.Empty});
        var sound=new CheckBox {Name="PlayReminderSound",Text="Play a sound for scheduled reminders",Checked=draft.PlayReminderSound,Height=34,Dock=DockStyle.Fill,Margin=Padding.Empty};fields.Controls.Add(sound);
        var toggleRow=new Panel {Height=46,Dock=DockStyle.Fill,Margin=Padding.Empty};
        var toggle=MakeButton("Sound options");toggle.Name="SoundOptions";toggle.SetBounds(0,2,160,36);toggle.AccessibleDescription="Show or hide custom notification sound options.";toggleRow.Controls.Add(toggle);fields.Controls.Add(toggleRow);
        var soundOptions=new TableLayoutPanel {Name="SoundOptionsPanel",Dock=DockStyle.Fill,ColumnCount=1,AutoSize=true,Visible=false,Margin=Padding.Empty};soundOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.Controls.Add(soundOptions);
        var choice=new EditorPicker {Name="ReminderSoundKind",AccessibleName="Notification sound",Height=42,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10)};
        choice.AddChoice("Windows sound (default)","Windows");choice.AddChoice("Custom sound (.wav)","Custom");choice.SelectedIndex=string.IsNullOrWhiteSpace(draft.ReminderSoundPath)?0:1;soundOptions.Controls.Add(choice);
        var fileRow=new Panel {Height=44,Dock=DockStyle.Fill,Margin=Padding.Empty};
        var fileHost=new RoundedPanel {Tag="window",Height=40,Padding=new Padding(10,9,10,6)};
        var soundSelection=new ReminderSoundSelection(draft.ReminderSoundPath,draft.ReminderSoundName);
        var soundPath=new TextBox {Name="ReminderSoundPath",AccessibleName="Custom WAV sound file",BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,Text=soundSelection.DisplayText,PlaceholderText="Choose a .wav file"};fileHost.Controls.Add(soundPath);fileRow.Controls.Add(fileHost);
        var browse=MakeButton("Browse...");browse.Name="BrowseReminderSound";fileRow.Controls.Add(browse);
        fileRow.SizeChanged+=(_,_)=>{fileHost.SetBounds(0,0,Math.Max(100,fileRow.ClientSize.Width-106),40);browse.SetBounds(fileRow.ClientSize.Width-96,0,96,40);};soundOptions.Controls.Add(fileRow);
        var previewRow=new Panel {Height=46,Dock=DockStyle.Fill,Margin=Padding.Empty};
        var preview=MakeButton("Preview");preview.Name="PreviewReminderSound";preview.SetBounds(0,6,110,36);previewRow.Controls.Add(preview);soundOptions.Controls.Add(previewRow);
        var soundHint=new Label {Name="ReminderSoundHint",Height=88,Dock=DockStyle.Fill,Tag="muted",Margin=Padding.Empty};soundOptions.Controls.Add(soundHint);
        void SoundChanged()
        {
            bool custom=choice.SelectedIndex==1;fileRow.Visible=custom;soundOptions.Enabled=sound.Checked;toggle.Enabled=sound.Checked;
            soundHint.Text=custom?(ReminderSound.Problem(profile.ResolveAsset(soundSelection.SelectedPath(soundPath.Text)))??"Your sound is kept in the data folder when saved. The original filename is shown above; missing or unplayable sounds fall back to Windows."):"Uses your Windows Information sound. Manual launches are quiet.";
        }
        toggle.Click+=(_,_)=>{soundOptions.Visible=!soundOptions.Visible;toggle.Text=soundOptions.Visible?"Hide sound options":"Sound options";};
        choice.SelectedIndexChanged+=(_,_)=>SoundChanged();sound.CheckedChanged+=(_,_)=>SoundChanged();soundPath.TextChanged+=(_,_)=>SoundChanged();
        browse.Click+=(_,_)=>{using var picker=soundSelection.CreatePicker(profile.DirectoryPath,soundPath.Text);if(picker.ShowDialog(dialog)==DialogResult.OK)soundPath.Text=picker.FileName;};
        preview.Click+=(_,_)=>
        {
            try
            {
                string? path=choice.SelectedIndex==1?soundSelection.SelectedPath(soundPath.Text):null;
                if(path is not null)
                {
                    path=profile.ResolveAsset(path);
                    if(ReminderSound.Problem(path) is string problem){soundHint.Text=problem;return;}
                }
                bool custom=reminderAttention.PlaySound(path);
                soundHint.Text=path is not null&&!custom?"Windows could not play this WAV encoding. Export it as PCM WAV. Preview used the Windows sound.":"Preview requested. Playback follows your Windows volume and sound settings.";
            }
            catch(Exception error){if(options.TestDirectory is not null)throw;MessageBox.Show(dialog,error.Message,"Could not preview sound",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        };
        SoundChanged();
        var save=PageButton(footer,"Save schedule",160,true);save.Name="SaveSchedule";
        var cancel=PageButton(footer,"Cancel",110);cancel.Name="CancelSchedule";cancel.Click+=(_,_)=>dialog.Close();dialog.CancelButton=cancel;
        bool SaveSchedule()
        {
            try
            {
                saved.Mode=mode.SelectedIndex==0?"Monthly":"Interval";saved.DayOfMonth=(int)day.Value;saved.IntervalDays=(int)interval.Value;
                saved.StartDate=start.Value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);saved.TimeOfDay=time.Value.ToString("HH:mm",CultureInfo.InvariantCulture);draft.Schedule=saved;
                saved.Enabled=enabled.Checked;draft.PlayReminderSound=sound.Checked;
                draft.ReminderSoundPath=choice.SelectedIndex==1?soundSelection.SelectedPath(soundPath.Text):null;
                draft.ReminderSoundName=choice.SelectedIndex==1?soundSelection.SelectedName(soundPath.Text):null;
                if(enabled.Checked&&sound.Checked&&draft.ReminderSoundPath is not null)ReminderSound.ValidatePath(profile.ResolveAsset(draft.ReminderSoundPath));
                ReminderSettings.Save(profile,draft,reminderScheduler);draft=profile.CopySettings();saved=draft.Schedule!;soundSelection=new(draft.ReminderSoundPath,draft.ReminderSoundName);soundPath.Text=soundSelection.DisplayText;
                RefreshStatus(true);cancel.Text="Close";return true;
            }
            catch(Exception error){if(options.TestDirectory is not null)throw;MessageBox.Show(dialog,error.Message,"Could not save schedule",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        }
        string Snapshot()=>System.Text.Json.JsonSerializer.Serialize(new {
            enabled.Checked,Mode=mode.SelectedIndex,Day=Descendants<TextBox>(day).Single().Text,Interval=Descendants<TextBox>(interval).Single().Text,
            Start=Descendants<TextBox>(start).Single().Text,Time=Descendants<TextBox>(time).Single().Text,
            Sound=sound.Checked,Kind=choice.SelectedIndex,Path=soundPath.Text
        });
        bool requireApply=profile.NeedsAssetMigration;
        try{var actual=reminderScheduler.Read();requireApply|=actual.Enabled!=saved.Enabled||(saved.Enabled&&actual.Executable is string path&&!string.Equals(path,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase));}
        catch{requireApply=true;}
        FinishPage(dialog);
        _=new EditSession(dialog,save,"Save schedule",Snapshot,SaveSchedule,()=>true,()=>ConfirmUnsaved(dialog),requireApply,()=>StyleSaveButton(save));
        return dialog;
    }

    static IEnumerable<T> Descendants<T>(Control parent) where T:Control
    {
        foreach(Control child in parent.Controls)
        {if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    internal void VerifySettingsWindows(Action<bool,string> check)
    {
        Show();Application.DoEvents();
        foreach(var factory in new Func<Form>[]{CreateAbout,CreateHelp,CreateChecklistEditor,CreateScheduleSettings})
        {
            using var dialog=factory();
            Exception? callbackError=null;bool entered=false;
            using var timer=new System.Windows.Forms.Timer {Interval=80};
            timer.Tick+=(_,_)=>
            {
                timer.Stop();
                try
                {
                    entered=true;
                    check(dialog.Modal&&dialog.Owner==this&&!dialog.ShowInTaskbar&&!WindowStyle.IsWindowEnabled(Handle),dialog.Text+" is a modal owned window without a separate taskbar entry");
                    Close();Application.DoEvents();
                    check(!IsDisposed&&Visible&&dialog.Visible,dialog.Text+" blocks closing its main window");
                    ActivateWindow();
                    check(!WindowStyle.IsWindowEnabled(Handle)&&dialog.Visible,"Relaunch activation preserves the open "+dialog.Text+" dialog");
                }
                catch(Exception error){callbackError=error;}
                finally{dialog.Close();}
            };
            timer.Start();ShowOwnedDialog(dialog);
            if(callbackError is not null)throw callbackError;
            check(entered&&WindowStyle.IsWindowEnabled(Handle)&&!IsDisposed,"Closing "+dialog.Text+" returns control to the main window");
        }
        string initialAppearance=appearance;
        foreach(string mode in new[]{"Dark","Light","System"})
        {
            appearanceMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(x=>(string?)x.Tag==mode).PerformClick();
            check(appearanceMenu.DropDown.Renderer is AppMenuRenderer&&menu.Renderer is AppMenuRenderer,"Appearance uses the shared check and arrow renderer in "+mode);
            check(appearanceMenu.DropDownItems.Cast<ToolStripMenuItem>().Count(x=>x.Checked)==1&&appearanceMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(x=>x.Checked).Tag?.ToString()==mode,"Appearance check follows the selected "+mode+" setting");
        }
        SetAppearance(initialAppearance);
        string originalText=File.ReadAllText(profile.SettingsPath);
        string originalId=profile.Settings.Items[0].Id;
        using(var editor=CreateChecklistEditor())
        {
            editor.Show();Application.DoEvents();
            var tree=Descendants<TreeView>(editor).Single();
            check(tree.Nodes.Count==3&&tree.Nodes[2].Nodes.Count==3,"Editor shows standalone tasks and a category with nested tasks");
            check(!Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon").Visible,"Advanced launch and icon fields are collapsed initially");
            var iconInput=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon");
            var preview=Descendants<PictureBox>(editor).Single(x=>x.Name=="EntryIconPreview");
            var defaultIcon=preview.Image;
            string testIcon=Path.Combine(profile.DirectoryPath,"editor-preview-icon.png");
            using(var sample=new Bitmap(3,6)){using var paint=Graphics.FromImage(sample);paint.Clear(Color.Orange);sample.Save(testIcon,System.Drawing.Imaging.ImageFormat.Png);}
            iconInput.Text=testIcon;
            check(preview.Image is Bitmap custom&&custom.Width==3&&custom.Height==6&&custom.GetPixel(1,1).ToArgb()==Color.Orange.ToArgb(),"Editor preview uses the selected custom artwork without stretching");
            iconInput.Text="";
            check(ReferenceEquals(preview.Image,defaultIcon),"Clearing a custom icon restores the program icon in the editor");
            File.Delete(testIcon);
            var viewport=Descendants<EditorScrollArea>(editor).Single();
            check(viewport.MaximumOffset==0,"Collapsed editor does not need a scrollbar");
            Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();Application.DoEvents();
            editor.Size=editor.MinimumSize;Application.DoEvents();
            check(viewport.MaximumOffset>0,"Expanded options scroll at the minimum editor size");
            viewport.ScrollToEnd();Application.DoEvents();
            check(viewport.ScrollOffset==viewport.MaximumOffset,"Editor can scroll to the last field");
            var last=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryWorkingDirectory");
            var lastBounds=last.RectangleToScreen(last.ClientRectangle);
            var visibleBounds=viewport.RectangleToScreen(viewport.ContentViewport);
            check(visibleBounds.Contains(lastBounds),"Last input remains fully visible above the fixed footer");
            var gutter=viewport.RectangleToScreen(viewport.ScrollGutter);
            check(!Descendants<Button>(viewport).Where(x=>x.Visible).Any(x=>x.RectangleToScreen(x.ClientRectangle).IntersectsWith(gutter)),"Scroll gutter never overlaps editor buttons");
            var first=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName");first.Focus();Application.DoEvents();
            check(viewport.ScrollOffset<viewport.MaximumOffset&&visibleBounds.Contains(first.RectangleToScreen(first.ClientRectangle)),"Keyboard focus scrolls the first field back into view");
            Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();Application.DoEvents();
            check(viewport.MaximumOffset==0&&viewport.ScrollOffset==0,"Collapsing options resets scrolling without a leftover scrollbar");
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="Canceled change";
            Descendants<Button>(editor).Single(x=>x.Name=="CancelEditor").PerformClick();
        }
        check(File.ReadAllText(profile.SettingsPath)==originalText,"Editor Cancel preserves configuration bytes");
        boxes[0].Checked=true;string originalCheckedAt=checkedTimes[originalId];
        using(var editor=CreateChecklistEditor())
        {
            editor.Show();Application.DoEvents();
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="Renamed backup task";
            var kind=Descendants<EditorPicker>(editor).Single(x=>x.Name=="OpenKind");
            kind.PerformClick();
            check(kind.ContextMenuStrip?.Items.Count==4,"What to open exposes four working menu choices");
            check(kind.ContextMenuStrip!.ShowCheckMargin&&kind.ContextMenuStrip.Renderer is AppMenuRenderer&&kind.ContextMenuStrip.Items.Cast<ToolStripMenuItem>().Single(x=>x.Checked).Text=="Program or file","Open-kind dropdown reserves a visible check column for the selected option");
            ((ToolStripMenuItem)kind.ContextMenuStrip!.Items[3]).PerformClick();kind.ContextMenuStrip.Close();
            check(kind.SelectedIndex==3,"Website selection works through the displayed menu");
            check(kind.ContextMenuStrip.Items.Cast<ToolStripMenuItem>().Single(x=>x.Checked).Text=="Website","Dropdown check moves when a different option is selected");
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryTarget").Text="https://example.com/";
            Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();
            check(Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon").Visible,"More options reveals the independent custom icon field");
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon").Text="custom-icon.png";
            Descendants<Button>(editor).Single(x=>x.Name=="AddCategory").PerformClick();
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="New group";
            Descendants<Button>(editor).Single(x=>x.Name=="AddTask").PerformClick();
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="New backup task";
            var tree=Descendants<TreeView>(editor).Single();
            check(tree.SelectedNode?.Parent?.Text=="New group","Adding a task under a group assigns it automatically");
            Descendants<Button>(editor).Single(x=>x.Name=="MoveUp").PerformClick();
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();
        }
        check(profile.Settings.Items[0].Id==originalId&&profile.Settings.Items[0].Title=="Renamed backup task"&&boxes[0].Checked,"Saving a renamed task retains its ID and current checkmark");
        check(checkedTimes[originalId]==originalCheckedAt,"Renaming and reordering preserve the original checked timestamp");
        check(profile.Settings.Items[0].Target=="https://example.com/"&&profile.Settings.Items[0].IconPath=="custom-icon.png","Editor saves independent launch target and icon");
        check(profile.Settings.Items.Count==6&&profile.Settings.Groups.Count==2&&profile.Settings.Items[2].Title=="New backup task"&&profile.Settings.Items[2].Group==profile.Settings.Groups[1].Id,"Editor adds categories and moves grouped tasks as a unit");
        using(var editor=CreateChecklistEditor())
        {
            editor.Show();Application.DoEvents();
            var tree=Descendants<TreeView>(editor).Single();
            var task=profile.Settings.Items.Single(x=>x.Title=="New backup task");
            tree.SelectedNode=tree.Nodes.Find(task.Id,true).Single();
            Descendants<Button>(editor).Single(x=>x.Name=="RemoveEntry").PerformClick();
            var emptyCategory=profile.Settings.Groups.Single(x=>x.Title=="New group");
            tree.SelectedNode=tree.Nodes.Find(emptyCategory.Id,true).Single();
            check(Descendants<Button>(editor).Single(x=>x.Name=="MoveUp").Enabled,"Empty groups remain movable after their last task is removed");
            Descendants<Button>(editor).Single(x=>x.Name=="RemoveEntry").PerformClick();
            Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist").PerformClick();
        }
        check(profile.Settings.Items.Count==5&&profile.Settings.Groups.Count==1&&!profile.Settings.Items.Any(x=>x.Title=="New backup task"),"Editor removes tasks and empty categories and refreshes completion count");
        var originalCulture=CultureInfo.CurrentCulture;
        try
        {
            foreach(string name in new[]{"en-US","en-GB"})
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);
                using var localized=CreateScheduleSettings();localized.Show();Application.DoEvents();
                var clock=Descendants<ScheduleDateTimeInput>(localized).Single(x=>x.Name=="ScheduleTime");
                var calendar=Descendants<ScheduleDateTimeInput>(localized).Single(x=>x.Name=="ScheduleStart");
                check(clock.DisplayFormat==CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern&&calendar.DisplayFormat==CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,"Schedule uses regional picker formats for "+name);
                clock.Value=DateTime.Today.AddHours(17).AddMinutes(45);
                var summary=Descendants<Label>(localized).Single(x=>x.Name=="ScheduleSummary");
                check(summary.Text.Contains(clock.Value.ToString("t")),"Monthly summary uses regional time for "+name);
                Descendants<ThemeNumberInput>(localized).Single(x=>x.Name=="ScheduleDay").Value=31;
                check(summary.Text.Contains("day 31"),"Monthly summary follows day changes for "+name);
                var clockText=Descendants<TextBox>(clock).Single();
                clockText.Text=clock.Value.ToString(clock.DisplayFormat,CultureInfo.CurrentCulture);
                check(clock.Value.Hour==17&&clock.Value.Minute==45,"Regional time entry preserves 17:45 for "+name);
                clockText.Text="invalid";
                bool rejected=false;try{_=clock.Value;}catch(InvalidDataException){rejected=true;}
                check(rejected&&summary.Text.Contains("valid date and time"),"Invalid time is rejected without crashing the summary for "+name);
                clock.Value=DateTime.Today.AddHours(23).AddMinutes(59);
                Descendants<Button>(clock).Single(x=>x.Text=="+").PerformClick();
                check(clock.Value.Hour==0&&clock.Value.Minute==0,"Time adjustment wraps at midnight for "+name);
                calendar.Value=new DateTime(2028,2,29);
                check(calendar.Value==new DateTime(2028,2,29),"Regional date entry preserves leap day for "+name);
            }
        }
        finally{CultureInfo.CurrentCulture=originalCulture;}
        string beforeSchedule=File.ReadAllText(profile.SettingsPath);
        using(var schedule=CreateScheduleSettings())
        {
            schedule.Show();Application.DoEvents();Descendants<ThemeNumberInput>(schedule).First().Value=17;
            Descendants<Button>(schedule).Single(x=>x.Text=="Cancel").PerformClick();
        }
        check(File.ReadAllText(profile.SettingsPath)==beforeSchedule,"Schedule Cancel preserves configuration");
        using(var schedule=CreateScheduleSettings())
        {
            schedule.Show();Application.DoEvents();var frequency=Descendants<EditorPicker>(schedule).Single(x=>x.Name=="ScheduleFrequency");
            var day=Descendants<ThemeNumberInput>(schedule).Single(x=>x.Name=="ScheduleDay");
            var interval=Descendants<ThemeNumberInput>(schedule).Single(x=>x.Name=="ScheduleInterval");
            day.Value=31;Descendants<Button>(day).Single(x=>x.Text=="-").PerformClick();
            check(day.Value==30,"Modern number decrement button updates the day");
            Descendants<Button>(day).Single(x=>x.Text=="+").PerformClick();
            check(day.Value==31&&!Descendants<Button>(day).Single(x=>x.Text=="+").Enabled,"Number buttons respect the maximum day");
            day.Value=1;check(!Descendants<Button>(day).Single(x=>x.Text=="-").Enabled,"Number buttons respect the minimum day");
            check(day.Visible&&!interval.Visible,"Monthly schedule shows only applicable timing fields");
            frequency.PerformClick();((ToolStripMenuItem)frequency.ContextMenuStrip!.Items[1]).PerformClick();frequency.ContextMenuStrip.Close();
            check(!day.Visible&&interval.Visible&&Descendants<ScheduleDateTimeInput>(schedule).Single(x=>x.Name=="ScheduleStart").Visible,"Interval selection reveals interval and start date");
            var time=Descendants<ScheduleDateTimeInput>(schedule).Single(x=>x.Name=="ScheduleTime");
            var start=Descendants<ScheduleDateTimeInput>(schedule).Single(x=>x.Name=="ScheduleStart");
            check(time.DisplayFormat==CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern&&start.DisplayFormat==CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,"Schedule pickers follow the Windows regional date and time formats");
            time.Value=DateTime.Today.AddHours(17).AddMinutes(45);start.Value=new DateTime(2028,2,29);
            Descendants<Button>(start).Single().PerformClick();Application.DoEvents();
            check(start.CurrentCalendar is not null,"Start date button opens a Windows calendar");
            start.CurrentCalendar!.SetDate(new DateTime(2028,3,1));Application.DoEvents();
            check(start.Value==new DateTime(2028,3,1),"Calendar selection updates the date field");
            ((ToolStripDropDown)start.CurrentCalendar!.Parent!).Close();start.Value=new DateTime(2028,2,29);
            Descendants<CheckBox>(schedule).Single(x=>x.Name=="EnableReminders").Checked=true;
            var sound=Descendants<CheckBox>(schedule).Single(x=>x.Name=="PlayReminderSound");
            var soundPanel=Descendants<TableLayoutPanel>(schedule).Single(x=>x.Name=="SoundOptionsPanel");
            check(!soundPanel.Visible,"Custom sound options are collapsed by default");
            Descendants<Button>(schedule).Single(x=>x.Name=="SoundOptions").PerformClick();Application.DoEvents();
            check(soundPanel.Visible,"Sound options expand on request");
            var soundChoice=Descendants<EditorPicker>(schedule).Single(x=>x.Name=="ReminderSoundKind");soundChoice.SelectedIndex=1;
            var customPath=Descendants<TextBox>(schedule).Single(x=>x.Name=="ReminderSoundPath");customPath.Text=Path.Combine(profile.DirectoryPath,"reminder.wav");SoundVerification.WriteWave(customPath.Text);
            int soundsBefore=((RecordingReminderAttention)reminderAttention).Sounds;
            Descendants<Button>(schedule).Single(x=>x.Name=="PreviewReminderSound").PerformClick();
            check(((RecordingReminderAttention)reminderAttention).Sounds==soundsBefore+1&&((RecordingReminderAttention)reminderAttention).SoundPath==customPath.Text,"Preview uses the unsaved custom selection without registering a task");
            sound.Checked=false;
            check(!Descendants<Button>(schedule).Single(x=>x.Name=="PreviewReminderSound").Enabled,"Turning sound off disables sound option controls");
            Descendants<ThemeNumberInput>(schedule).Last().Value=45;
            string summary=Descendants<Label>(schedule).Single(x=>x.Name=="ScheduleSummary").Text;
            check(summary.Contains("45 days")&&summary.Contains(start.Value.ToString("d"))&&summary.Contains(time.Value.ToString("t")),"Interval summary updates with the selected date, time and day count");
            Descendants<Button>(schedule).Single(x=>x.Text=="Save schedule").PerformClick();
        }
        check(reminderScheduler.Read().Enabled,"Saving an enabled schedule registers the task through the scheduler");
        check(!profile.Settings.PlayReminderSound&&ProfileAssets.IsManagedName(profile.Settings.ReminderSoundPath!)&&profile.Settings.ReminderSoundName=="reminder.wav","Sound opt-out and custom path are saved with schedule settings");
        check(profile.Settings.Schedule?.Mode=="Interval"&&profile.Settings.Schedule.IntervalDays==45&&profile.Settings.Schedule.TimeOfDay=="17:45"&&profile.Settings.Schedule.StartDate=="2028-02-29","Schedule screen saves interval and local time preferences");
        check(profile.State.LastCompleted is null&&profile.Settings.Items[0].Id==originalId,"Schedule preferences preserve checklist and completion state");
        using(var help=CreateHelp())
        {
            help.Show();Application.DoEvents();
            var search=Descendants<TextBox>(help).Single(x=>x.Name=="HelpSearch");
            var topics=Descendants<ListBox>(help).Single();
            search.Text="icons";check(topics.Items.Count>0&&Descendants<RichTextBox>(help).Single().Text.Contains("icons",StringComparison.OrdinalIgnoreCase),"Help search selects matching content");
            search.Text="zzzz-no-match";check(topics.Items.Count==0&&Descendants<Label>(help).Single(x=>x.Name=="HelpTopicTitle").Text=="No matching topics","Help has an explicit empty search state");
            search.Clear();check(topics.Items.Count==Topics.Count,"Clearing Help search restores all topics");
        }
        using(var about=CreateAbout())
        {check(Descendants<LinkLabel>(about).Single().Enabled&&Descendants<LinkLabel>(about).Single().Text=="DGX.digitalguy.me"&&Descendants<Label>(about).Any(label=>label.Text=="Developer Website")&&DeveloperWebsite=="https://dgx.digitalguy.me","About shows Developer Website and links DGX.digitalguy.me to the main website");}
        Close();
    }
    void ReloadChecklist(bool restored=false)
    {
        var checkedIds=(restored?profile.State.Checked:boxes.Where(x=>x.Checked).Select(x=>(string)x.Tag!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        reloadingChecklist=true;stack.SuspendLayout();
        try
        {
            foreach(var row in stack.Controls.OfType<Panel>().ToArray()){stack.Controls.Remove(row);row.Dispose();}
            boxes.Clear();bar.Maximum=profile.Settings.Items.Count;
            var knownIds=profile.Settings.Items.Select(x=>x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(string removed in checkedTimes.Keys.Where(x=>!knownIds.Contains(x)).ToArray())checkedTimes.Remove(removed);
            var shown=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in profile.Settings.Items)
            {
                if(!string.IsNullOrWhiteSpace(item.Group)&&shown.Add(item.Group))AddGroup(profile.Settings.Groups.Single(x=>string.Equals(x.Id,item.Group,StringComparison.OrdinalIgnoreCase)));
                AddItem(item);boxes[^1].Checked=checkedIds.Contains(item.Id);
            }
        }
        finally{reloadingChecklist=false;stack.ResumeLayout(true);}
        SetAppearance(appearance);LayoutRows();UpdateProgress();
    }
}
