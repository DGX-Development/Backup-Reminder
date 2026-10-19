namespace BackupReminder;

internal sealed partial class MainWindow
{
    internal const string DeveloperWebsite = "https://dgx.digitalguy.me";
    internal const string IssuesWebsite = "https://github.com/DGX-Development/Backup-Reminder/issues/new";
    // Both destinations are available; private GitHub repositories require account access.
    static readonly bool WebsiteAvailable = true;
    static readonly bool IssuesAvailable = true;
    readonly Dictionary<ToolStripItem,string> menuGlyphs=[];
    readonly Dictionary<(string Glyph,int Color),Image> menuGlyphImages=[];
    void AddMenuGlyph(ToolStripItem item,string glyph)=>menuGlyphs.Add(item,glyph);
    void RefreshMenuGlyphs()
    {
        var color=SystemInformation.HighContrast?SystemColors.MenuText:TextColor;
        foreach(var (item,glyph) in menuGlyphs)
        {
            var key=(glyph,color.ToArgb());
            if(!menuGlyphImages.TryGetValue(key,out var image)){image=MenuGlyphs.Create(glyph,color);menuGlyphImages.Add(key,image);owned.Add(image);}
            item.Image=image;
        }
    }
    void BuildMenu()
    {
        menu.Font=body;
        var file=new ToolStripMenuItem("&File");
        AddMenuGlyph(file.DropDownItems.Add("Edit checklist...",null,(_,_)=>{using var dialog=CreateChecklistEditor();ShowOwnedDialog(dialog);}),"checklist");
        file.DropDownItems.Add(new ToolStripSeparator());
        AddMenuGlyph(file.DropDownItems.Add("Back up configuration...",null,(_,_)=>BackUpConfiguration()),"backup");
        AddMenuGlyph(file.DropDownItems.Add("Restore configuration...",null,(_,_)=>RestoreConfiguration()),"restore");
        file.DropDownItems.Add(new ToolStripSeparator()); AddMenuGlyph(file.DropDownItems.Add("E&xit",null,(_,_)=>Close()),"exit");
        var settings=new ToolStripMenuItem("&Settings");
        AddMenuGlyph(settings.DropDownItems.Add("Reminder schedule...",null,(_,_)=>{using var dialog=CreateScheduleSettings();ShowOwnedDialog(dialog);}),"clock");
        foreach(string choice in new[]{"System","Light","Dark"})
        {var item=new ToolStripMenuItem(choice){Tag=choice};item.Click+=(_,_)=>SetAppearance(choice);appearanceMenu.DropDownItems.Add(item);}
        settings.DropDownItems.Add(appearanceMenu);AddMenuGlyph(appearanceMenu,"appearance");
        settings.DropDownItems.Add(new ToolStripSeparator());
        AddMenuGlyph(settings.DropDownItems.Add("Open &data folder",null,(_,_)=>{try{Profile.Open(new ChecklistEntry{Target=profile.DirectoryPath});}catch(Exception error){ShowError(error.Message);}}),"folder");
        var help=new ToolStripMenuItem("&Help");
        var viewHelp=new ToolStripMenuItem("View &Help"){ShortcutKeys=Keys.F1};viewHelp.Click+=(_,_)=>{using var dialog=CreateHelp();ShowOwnedDialog(dialog);};help.DropDownItems.Add(viewHelp);AddMenuGlyph(viewHelp,"help");
        var report=new ToolStripMenuItem("Report an &issue"){Enabled=IssuesAvailable,ToolTipText="Open a new issue on GitHub."};report.Click+=(_,_)=>OpenWebsite(IssuesWebsite);help.DropDownItems.Add(report);AddMenuGlyph(report,"report");
        help.DropDownItems.Add(new ToolStripSeparator());
        AddMenuGlyph(help.DropDownItems.Add("&About Backup Reminder",null,(_,_)=>{using var dialog=CreateAbout();ShowOwnedDialog(dialog);}),"about");
        menu.Items.AddRange([file,settings,help]);
    }
    void OpenWebsite(string target)
    {try{Profile.Open(new ChecklistEntry{Target=target});}catch(Exception error){ShowError(error.Message);}}
    void ShowOwnedDialog(Form dialog)
    {
        if(VisibleDialog() is not null){ActivateWindow();return;}
        dialog.ShowDialog(this);
    }
    Form? VisibleDialog()=>OwnedForms.LastOrDefault(dialog=>!dialog.IsDisposed&&dialog.Visible);
    internal void ActivateWindow()
    {
        if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
        if(!Visible)Show();
        var target=VisibleDialog()??this;
        if(target.WindowState==FormWindowState.Minimized)target.WindowState=FormWindowState.Normal;
        target.BringToFront();target.Activate();
    }
    Form NewDialog(string text, Size size)
    {
        var dialog=new Form{Text=text,Size=size,StartPosition=FormStartPosition.CenterParent,Owner=this,ShowInTaskbar=false,Font=body,Icon=Icon,MinimizeBox=false,BackColor=Surface,ForeColor=TextColor,AutoScaleMode=AutoScaleMode.Dpi};
        dialog.HandleCreated+=(_,_)=>WindowStyle.Apply(dialog,dark);return dialog;
    }
    // Shared page frame keeps secondary windows aligned with the checklist editor.
    (Form Dialog, Panel Content, Panel Footer) StyledPage(string caption,string subtitle,Size size)
    {
        var dialog=NewDialog(caption,size);dialog.Tag="window";dialog.MinimumSize=size;
        var page=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(28,0,28,0),Tag="window"};
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,104));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,78));dialog.Controls.Add(page);
        var header=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="window"};
        header.Controls.Add(new Label {Text=caption,Font=heading,Bounds=new Rectangle(0,20,size.Width-80,38),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right});
        header.Controls.Add(new Label {Text=subtitle,Tag="muted",Bounds=new Rectangle(2,65,size.Width-80,28),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right});
        var content=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="window"};
        var footer=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="window"};
        page.Controls.Add(header,0,0);page.Controls.Add(content,0,1);page.Controls.Add(footer,0,2);
        return(dialog,content,footer);
    }
    Button PageButton(Panel footer,string text,int width,bool primary=false)
    {
        var button=MakeButton(text);button.Tag=primary?"primary":null;button.SetBounds(0,20,width,38);
        footer.Controls.Add(button);
        void Arrange(){int right=footer.ClientSize.Width;foreach(var item in footer.Controls.OfType<Button>()){item.Left=right-item.Width;right=item.Left-10;}}
        footer.SizeChanged+=(_,_)=>Arrange();Arrange();return button;
    }
    void FinishPage(Form dialog)
    {
        ApplyTheme(dialog);
        foreach(var button in Descendants<Button>(dialog).Where(x=>(string?)x.Tag=="primary"))
        {button.BackColor=Accent;button.ForeColor=dark?Color.FromArgb(20,35,55):Color.White;}
    }
    Form CreateAbout()
    {
        var (dialog,content,footer)=StyledPage("About Backup Reminder","Your manual backup routine, in one place.",new Size(640,570));
        var card=new RoundedPanel {Dock=DockStyle.Fill,Padding=new Padding(24)};content.Controls.Add(card);
        card.Controls.Add(new BrandPictureBox {Image=brand,SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(24,22,64,64)});
        card.Controls.Add(new Label {Text="Backup Reminder",Font=groupFont,Bounds=new Rectangle(106,26,390,30)});
        card.Controls.Add(new Label {Text="Version "+System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0],Tag="muted",Bounds=new Rectangle(107,60,390,25)});
        card.Controls.Add(new Label {Text="A checklist and tool launcher for your manual backup routine.\r\n\r\nCreated by DGX Development.",Bounds=new Rectangle(24,110,490,82)});
        card.Controls.Add(new Label {Text="Licensed under PolyForm Noncommercial 1.0.0.\r\nApplication icons belong to their respective owners.",Tag="muted",Font=small,Bounds=new Rectangle(24,200,490,52)});
        card.Controls.Add(new Label {Text="Developer Website",Tag="muted",Font=small,Bounds=new Rectangle(24,268,450,22)});
        var website=new LinkLabel {Text="DGX.digitalguy.me",Enabled=WebsiteAvailable,Bounds=new Rectangle(24,294,450,26)};
        website.LinkClicked+=(_,_)=>OpenWebsite(DeveloperWebsite);card.Controls.Add(website);
        var close=PageButton(footer,"Close",110);close.Click+=(_,_)=>dialog.Close();dialog.CancelButton=close;FinishPage(dialog);return dialog;
    }
    static readonly Dictionary<string,string> Topics=new()
    {
        ["Getting started"]="Use Open to launch the tool for a backup step. Perform the backup yourself, review its results, then check the item.\r\n\r\nOpening a tool does not mark a backup complete. Backup Reminder does not copy files or run backup jobs automatically.",
        ["Items and grouped jobs"]="Standalone items can have their own Open buttons. A task with What to open set to Nothing is a checklist-only step, with no launcher.\r\n\r\nA group can have one tool launcher and several independently checked jobs. The group heading is not an extra task. Select a group to move it and its tasks together; empty groups can also move before saving and retain that position when tasks are added. In Edit checklist, choose a Group to put tasks under one heading, or None (show as a separate task) to keep a task independent.\r\n\r\nThe starter examples use Notepad and File Explorer. Replace their targets with your preferred tools.",
        ["Programs, folders and icons"]="Open can launch a program, folder, or HTTP/HTTPS webpage. Documents and Pictures targets can follow your Windows folder locations. Program/file Browse opens beside the current target, or in Program Files when blank. Folder Browse opens at the selected folder, including redirected Windows Documents or Pictures locations; blank or unavailable folders start in Documents. Working directory Browse opens at its current folder, then the selected program folder, then Documents. Cancel leaves the values unchanged.\r\n\r\nCustom icons are independent of launch targets and can come from a local image, ICO, or executable. Save changes imports image copies into the data folder's icons subfolder; executable icons are extracted as PNG images. Originals are left untouched. Imported files keep readable names; different files with the same name receive numbered suffixes. The custom icon field shows the saved filename. Browse opens an icon chooser for the current custom file or selected program; when neither is available, it shows the Windows shell32 icon library. Select an individual icon, or use Browse inside the chooser to choose an EXE, DLL, ICO or image. That file browser starts beside the current source. Library icons are extracted to PNG on Save; canceling the chooser imports nothing. Discarding unsaved edits imports nothing. Existing external paths still load; available files are imported the next time settings are saved. Missing files retain their references and use a fallback.\r\n\r\nFile > Edit checklist lets you add, remove and reorder tasks, edit groups, and choose program paths, folders, websites and custom icons. Changes are applied when you click Save changes or choose Save changes in the close confirmation.",
        ["Saving and unsaved changes"]="Edit checklist and Reminder schedule show Saved with a disabled button when their values match the saved configuration. Editing a value changes the button to Save changes or Save schedule and enables it. A successful save stays on the page and returns the button to Saved; another edit enables saving again. Reverting every edit also returns to Saved. Expanding options or previewing sound does not change saved settings. A moved or missing Windows reminder may need Save schedule even when the displayed values are unchanged.\r\n\r\nCancel, Close, X and Escape ask what to do when there are unsaved edits: Save changes saves and closes, Discard changes closes without saving, and Keep editing leaves the page open. Escape or X on that confirmation keeps editing. A failed save leaves the page open and the edits unsaved. Unchanged or already saved pages close without prompting. These confirmations concern editor drafts; the main checklist still saves checkmarks when you close the app.",
        ["Progress and completion"]="Close saves your progress. Complete backups records a finished session.\r\n\r\nClose for now, the window close button, or File > Exit saves unfinished checkmarks and exits completely. No background or tray process remains.\r\n\r\nComplete backups becomes available after all items are checked. It records your confirmation and closes the window. The next session after completion starts with unchecked items. An unfinished checklist keeps its checkmarks even when the next regular reminder arrives.\r\n\r\nA checked item shows when you checked its box, not proof of when a backup ran. Older checkmarks without a timestamp say Checked date not recorded. Unchecking clears that time; completing the routine clears all individual times.\r\n\r\nThe app does not independently verify backup integrity or restoreability.",
        ["Reminders and schedules"]="Settings > Reminder schedule lets you enable reminders and save a real Windows scheduled task. The page shows the actual task status and next run. The task appears directly under Task Scheduler Library, named Backup Reminder - followed by your Windows username. Additional profiles include a profile label; matching labels get a numbered suffix. Under Notification, Play a sound for scheduled reminders is on by default and can be turned off. Expand Sound options to choose Windows sound or a custom .wav file, use Browse to select it, and Preview to try the unsaved selection. Click Save schedule to keep your choice and import an available WAV into the data folder's sounds subfolder. The field shows the saved filename. Imported files keep readable names; different files with the same name receive numbered suffixes. Browse opens beside the selected file (the managed sounds folder after saving), falling back to Windows Media when no selected folder is available. Older saved sounds without filename information show Saved custom sound; Browse to select the original again to record its name. A .wav extension alone does not establish its format: a disguised Ogg, FLAC or MP3 file needs conversion to PCM WAV. Preview shows format/read errors without claiming custom playback. The original file is left untouched. If the custom file is missing, invalid or cannot play, the Windows Information sound is used. Playback follows Windows volume and sound settings. Manual launches are quiet. Scheduled launches request the selected sound once when enabled and briefly highlight the taskbar if the app is behind another window, without taking focus from an open edit. Turn off Enable scheduled reminders and click Save schedule to remove this profile's task from Windows. Your timing preferences are retained. Disable reminders before resetting or abandoning a profile.\r\n\r\nChoose a monthly day/time or a 1-365 day interval with a start date. Date and time follow your Windows regional settings. Use the calendar button to choose a start date, and review the live schedule summary. Shorter months use their last day for monthly reminders. Completing an extra session will not move or skip the regular reminder. You must be signed in to Windows; a missed reminder can run later when Windows is available. The computer is not woken from sleep. Keep the executable in a permanent folder. After moving it, open the app there and save the schedule again. Existing owned tasks with the earlier identifier name are migrated to readable names on normal launch when their app action is recognized, preserving their timing and enabled state.",
        ["Configuration and backups"]="Settings > Open data folder opens the current profile. Close the app before manually copying or replacing its files. File menu configuration backup and restore work while the app is open.\r\n\r\nsettings.json contains checklist items, groups, launch paths, icon choices, reminder timing and the scheduled-reminder sound preference/custom file path. checklist.json contains checkmarks, per-item checked timestamps and the last completion date.\r\n\r\nConfiguration backup and restore pickers start in Documents. File > Back up configuration saves a ZIP of settings, progress, custom icons and WAV sound. It does not back up your actual documents, backup tools or Windows task definitions. Unavailable custom files are reported. File > Restore configuration validates that ZIP and preserves your current configuration in recovery\\before-restore.zip before replacing it. Restore turns reminders off; check your program paths and enable reminders in Reminder schedule when ready. Treat configuration ZIPs as personal files: they contain your paths and preferences.\r\n\r\nSaved custom icons and sounds are copied into icons and sounds subfolders with profile-relative paths. Older hashed filenames are migrated on Save; when the original icon filename is unavailable, the task or group name is used. Configuration backups include checksums to detect damaged assets, and older configuration backups remain supported. Existing external references remain supported; unavailable files use the normal fallback. The default data folder is %LOCALAPPDATA%\\Backup Reminder. When updating, replace application files and keep your data folder. Do not overwrite your settings or progress with starter examples. Windows scheduled tasks are stored separately from these files. After restoring your settings on another computer, review program paths, open Reminder schedule, enable reminders and click Save schedule to register the reminder there.",
        ["Website and support"]="About links to the DGX Development website at dgx.digitalguy.me. Help > Report an issue opens the new-issue form on GitHub. You review and submit the report yourself.\r\n\r\nSign in to GitHub to submit a report. Include your app version and steps to reproduce the problem; leave out personal backup paths and private data.",
        ["Profiles and updates"]="Application files and user data are separate. By default, settings.json and checklist.json are stored in your Windows LocalAppData Backup Reminder folder.\r\n\r\nDevelopment mode uses a separate Development subfolder. An explicit --data-directory selects another profile. Only one window opens per profile. Opening the app manually again activates its existing window or open dialog. A scheduled reminder keeps that window and any edits intact and uses the configured sound and taskbar attention instead of forcing it to the front.\r\n\r\nTo update: 1. Close Backup Reminder. 2. Download and extract the new release ZIP. 3. Replace all supplied application files in the same app folder. 4. Reopen the app. Your separate data folder remains intact, and keeping the EXE at the same path preserves the scheduled reminder. If you move it, save the reminder schedule from the new location. An automatic updater is not implemented.",
        ["Keyboard and appearance"]="Alt+F opens File. Alt+S opens Settings. Alt+H opens Help. F1 opens Help. Tab moves between controls; Space toggles a focused checkbox.\r\n\r\nSettings > Appearance changes System, Light, or Dark for the current session."
    };
    internal static string[] SearchTopics(string query)=>Topics.Where(x=>x.Key.Contains(query,StringComparison.OrdinalIgnoreCase)||x.Value.Contains(query,StringComparison.OrdinalIgnoreCase)).Select(x=>x.Key).ToArray();
    Form CreateHelp()
    {
        var (dialog,area,footer)=StyledPage("Backup Reminder Help","Find guidance for your checklist, tools and saved progress.",new Size(980,720));
        var columns=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Tag="window"};
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));area.Controls.Add(columns);
        var navigation=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,0,22,0),Margin=Padding.Empty,Tag="window"};columns.Controls.Add(navigation,0,0);
        var searchHeader=new Panel {Dock=DockStyle.Top,Height=100,Tag="window"};
        searchHeader.Controls.Add(new Label {Text="SEARCH HELP",Font=small,Tag="muted",Bounds=new Rectangle(2,0,250,22)});
        var searchHost=new RoundedPanel {Tag="window",Bounds=new Rectangle(0,34,266,42),Padding=new Padding(12,10,12,8)};
        var search=new TextBox {Name="HelpSearch",AccessibleName="Search help",PlaceholderText="Enter a word or topic",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None};searchHost.Controls.Add(search);searchHeader.Controls.Add(searchHost);
        var list=new ListBox {Name="HelpTopics",AccessibleName="Help topics",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,IntegralHeight=false,Tag="window",DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=42};
        list.DrawItem+=(_,e)=>
        {
            using(var background=new SolidBrush(WindowColor))e.Graphics.FillRectangle(background,e.Bounds);if(e.Index<0)return;
            bool selected=e.Index==list.SelectedIndex;
            if(selected){using var shape=WindowStyle.Round(new RectangleF(e.Bounds.Left+.5f,e.Bounds.Top+2,e.Bounds.Width-2,e.Bounds.Height-4),6);using var fill=new SolidBrush(ButtonColor);e.Graphics.FillPath(fill,shape);using var pen=new Pen(Accent,3);e.Graphics.DrawLine(pen,e.Bounds.Left+2,e.Bounds.Top+12,e.Bounds.Left+2,e.Bounds.Bottom-12);}
            TextRenderer.DrawText(e.Graphics,(string)list.Items[e.Index],body,new Rectangle(e.Bounds.Left+12,e.Bounds.Top,e.Bounds.Width-20,e.Bounds.Height),TextColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
        };
        navigation.Controls.Add(list);navigation.Controls.Add(searchHeader);
        var card=new RoundedPanel {Dock=DockStyle.Fill,Padding=new Padding(24,20,24,20),Margin=Padding.Empty};columns.Controls.Add(card,1,0);
        var titleLabel=new Label {Name="HelpTopicTitle",Font=groupFont,Dock=DockStyle.Top,Height=52};
        var content=new RichTextBox {Name="HelpContent",Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,Font=body,ScrollBars=RichTextBoxScrollBars.Vertical};
        card.Controls.Add(content);card.Controls.Add(titleLabel);
        list.SelectedIndexChanged+=(_,_)=>{if(list.SelectedItem is string topic){titleLabel.Text=topic;content.Text=Topics[topic];}list.Invalidate();};
        void Filter(){list.Items.Clear();list.Items.AddRange(SearchTopics(search.Text.Trim()));if(list.Items.Count>0)list.SelectedIndex=0;else{titleLabel.Text="No matching topics";content.Text="Try a different word, such as icons, schedule or progress.";}}
        search.TextChanged+=(_,_)=>Filter();Filter();
        var close=PageButton(footer,"Close",110);close.Click+=(_,_)=>dialog.Close();dialog.CancelButton=close;
        FinishPage(dialog);content.BackColor=Surface;return dialog;
    }
}
