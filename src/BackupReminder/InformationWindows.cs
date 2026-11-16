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
        var color=SystemInformation.HighContrast?SystemColors.MenuText:ColorTranslator.FromHtml(dark?"#D2D2D2":"#4B5563");
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
        AddMenuGlyph(file.DropDownItems.Add("Open data folder",null,(_,_)=>{try{Profile.Open(new ChecklistEntry{Target=profile.DirectoryPath});}catch(Exception error){ShowError(error.Message);}}),"folder");
        file.DropDownItems.Add(new ToolStripSeparator()); AddMenuGlyph(file.DropDownItems.Add("E&xit",null,(_,_)=>ExitApplication()),"exit");
        var settings = new ToolStripMenuItem("&Settings");
        settings.Click += (_, _) => { using var dialog = CreateSettings(); ShowOwnedDialog(dialog); };
        var help=new ToolStripMenuItem("&Help");
        var viewHelp=new ToolStripMenuItem("&Help && user guide"){ShortcutKeys=Keys.F1};viewHelp.Click+=(_,_)=>{using var dialog=CreateHelp();ShowOwnedDialog(dialog);};help.DropDownItems.Add(viewHelp);AddMenuGlyph(viewHelp,"help");
        var report=new ToolStripMenuItem("Report an &issue"){Enabled=IssuesAvailable,ToolTipText="Open a new issue on GitHub."};report.Click+=(_,_)=>OpenWebsite(IssuesWebsite);help.DropDownItems.Add(report);AddMenuGlyph(report,"report");
        help.DropDownItems.Add(new ToolStripSeparator());
        AddMenuGlyph(help.DropDownItems.Add("&About Backup Reminder",null,(_,_)=>{using var dialog=CreateAbout();ShowOwnedDialog(dialog);}),"about");
        var pin = new ToolStripButton
        {
            Name = "OnTop", AccessibleName = "Always on top", ToolTipText = "Keep Backup Reminder on top",
            Alignment = ToolStripItemAlignment.Right, AutoSize = false, Size = new Size(36,36),
            DisplayStyle = ToolStripItemDisplayStyle.None, CheckOnClick = true
        };
        pin.CheckedChanged += (_,_) =>
        {
            TopMost = pin.Checked;
            pin.ToolTipText = pin.Checked ? "Turn off always on top" : "Keep Backup Reminder on top";
            pin.Invalidate();
        };
        menu.Items.AddRange([file,settings,help,pin]);
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
        if(trayIcon is not null)trayIcon.Visible=false;
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
        var button=MakeButton(text);button.Tag=primary?"primary":null;button.SetBounds(0,20,width,36);
        footer.Controls.Add(button);
        void Arrange(){int right=footer.ClientSize.Width-footer.Padding.Right;foreach(var item in footer.Controls.OfType<Button>()){item.Left=right-item.Width;right=item.Left-8;}}
        footer.SizeChanged+=(_,_)=>Arrange();Arrange();return button;
    }
    void FinishPage(Form dialog)
    {
        ApplyTheme(dialog);
        foreach(var button in Descendants<Button>(dialog).Where(x=>(string?)x.Tag=="primary"))
        {button.BackColor=Accent;button.ForeColor=dark?Color.FromArgb(20,35,55):Color.White;}
    }
    DialogResult ShowNotice(IWin32Window owner,string message,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
    {
        using var dialog=NewDialog(caption,new Size(560,320));
        dialog.Owner=owner as Form ?? this;
        dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
        dialog.MaximizeBox=false;
        var text=new TextBox{Text=message,Multiline=true,ReadOnly=true,TabStop=false,ScrollBars=ScrollBars.Vertical,
            BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,AccessibleName=caption};
        var footer=new Panel{Dock=DockStyle.Bottom,Height=64,Padding=new Padding(0,0,24,0)};
        var content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24,24,24,0)};
        content.Controls.Add(text);
        dialog.Controls.Add(content);
        dialog.Controls.Add(footer);
        var confirm=PageButton(footer,buttons==MessageBoxButtons.YesNo?"Yes":buttons==MessageBoxButtons.OKCancel?"Restore":"OK",88);
        confirm.DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.Yes:DialogResult.OK;
        dialog.AcceptButton=confirm;
        if(buttons==MessageBoxButtons.OK)
            dialog.CancelButton=confirm;
        else
        {
            var cancel=PageButton(footer,buttons==MessageBoxButtons.YesNo?"No":"Cancel",88);
            cancel.DialogResult=buttons==MessageBoxButtons.YesNo?DialogResult.No:DialogResult.Cancel;
            dialog.CancelButton=cancel;
            dialog.AcceptButton=cancel;
            dialog.ActiveControl=cancel;
        }
        FinishPage(dialog);
        return dialog.ShowDialog(owner);
    }

    Form CreateAbout()
    {
        var dialog=NewDialog("About Backup Reminder",new Size(560,440));
        dialog.Tag="window";
        dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
        dialog.MaximizeBox=false;
        var page=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28,28,28,8),ColumnCount=1,RowCount=4,Tag="window"};
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,88));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,155));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        dialog.Controls.Add(page);

        var header=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="window"};
        header.Controls.Add(new BrandPictureBox{Image=brand,Bounds=new Rectangle(0,0,64,64)});
        using var nameFont=new Font("Segoe UI Variable Display",18.75f,FontStyle.Bold);
        var productName=new Label{Text="Backup Reminder",Font=(Font)nameFont.Clone(),Bounds=new Rectangle(82,4,400,34)};
        productName.Disposed+=(_,_)=>productName.Font.Dispose();
        header.Controls.Add(productName);
        var version=System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
        header.Controls.Add(new Label{Text="Version "+version.ToString(3),Bounds=new Rectangle(82,40,380,24)});
        page.Controls.Add(header,0,0);

        var card=new RoundedPanel{Dock=DockStyle.Fill,Margin=Padding.Empty};
        card.Controls.Add(new Label{Text="Keep track of your manual backups and open the tools you use to run them.",Bounds=new Rectangle(18,16,445,42)});
        card.Controls.Add(new Label{Text="Developed by",Font=title,Bounds=new Rectangle(18,61,445,21)});
        using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("publisher.png")!;
        using var original=Image.FromStream(stream);
        var mark=new Bitmap(280,106);
        using(var graphics=Graphics.FromImage(mark))
        using(var attributes=new System.Drawing.Imaging.ImageAttributes())
        {
            Color ink=dark?Color.White:ColorTranslator.FromHtml("#162921");
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][]{
                new float[5],new float[5],new float[5],new float[]{0,0,0,1,0},
                new float[]{ink.R/255f,ink.G/255f,ink.B/255f,0,1}}));
            graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original,new Rectangle(0,0,mark.Width,mark.Height),0,0,original.Width,original.Height,GraphicsUnit.Pixel,attributes);
        }
        var publisher=new BrandPictureBox{Image=mark,AccessibleName="DGX Development",Bounds=new Rectangle(18,82,140,53),Cursor=Cursors.Hand};
        publisher.Disposed+=(_,_)=>mark.Dispose();
        publisher.Click+=(_,_)=>OpenWebsite(DeveloperWebsite);
        card.Controls.Add(publisher);
        var website=new LinkLabel{Text="dgx.digitalguy.me",AccessibleName="DGX Development website",Enabled=WebsiteAvailable,Bounds=new Rectangle(174,97,275,26)};
        website.LinkClicked+=(_,_)=>OpenWebsite(DeveloperWebsite);
        card.Controls.Add(website);
        page.Controls.Add(card,0,1);

        page.Controls.Add(new Label{Text="Licensed under PolyForm Noncommercial 1.0.0. Application icons belong to their respective owners.",Tag="muted",Dock=DockStyle.Fill,Margin=new Padding(0,10,0,0)},0,2);
        var footer=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Tag="window"};
        page.Controls.Add(footer,0,3);
        var close=PageButton(footer,"Close",88);
        close.Click+=(_,_)=>dialog.Close();
        dialog.AcceptButton=close;
        dialog.CancelButton=close;
        FinishPage(dialog);
        return dialog;
    }

    static readonly Dictionary<string,string> Topics=new()
    {
        ["Getting started"]="Use Open to launch the tool for a backup step. Perform the backup yourself, review its results, then check the item.\r\n\r\nOpening a tool does not mark a backup complete. Backup Reminder does not copy files or run backup jobs automatically.",
        ["Items and grouped jobs"]="Standalone items can have their own Open buttons. A task with What to open set to Nothing is a checklist-only step, with no launcher.\r\n\r\nA group can have one tool launcher and several independently checked jobs. The group heading is not an extra task. Select a group to move it and its tasks together; empty groups can also move before saving and retain that position when tasks are added. In Edit checklist, choose a Group to put tasks under one heading, or None (show as a separate task) to keep a task independent.\r\n\r\nThe starter examples use Notepad and File Explorer. Replace their targets with your preferred tools.",
        ["Programs, folders and icons"]="Open can launch a program, folder, or HTTP/HTTPS webpage. Documents and Pictures targets can follow your Windows folder locations. Program/file Browse opens beside the current target, or in Program Files when blank. Folder Browse opens at the selected folder, including redirected Windows Documents or Pictures locations; blank or unavailable folders start in Documents. Working directory Browse opens at its current folder, then the selected program folder, then Documents. Cancel leaves the values unchanged.\r\n\r\nCustom icons are independent of launch targets and can come from a local image, ICO, or executable. Save changes imports image copies into the data folder's icons subfolder; executable icons are extracted as PNG images. Originals are left untouched. Imported files keep readable names; different files with the same name receive numbered suffixes. The custom icon field shows the saved filename. Browse opens an icon chooser for the current custom file or selected program; when neither is available, it shows the Windows shell32 icon library. Select an individual icon, or use Browse inside the chooser to choose an EXE, DLL, ICO or image. That file browser starts beside the current source. Library icons are extracted to PNG on Save; canceling the chooser imports nothing. Discarding unsaved edits imports nothing. Existing external paths still load; available files are imported the next time settings are saved. Missing files retain their references and use a fallback.\r\n\r\nFile > Edit checklist lets you add, remove and reorder tasks, edit groups, and choose program paths, folders, websites and custom icons. Changes are applied when you click Save changes or choose Save changes in the close confirmation.",
        ["Saving and unsaved changes"]="Edit checklist and Reminder schedule show Saved with a disabled button when their values match the saved configuration. Editing a value changes the button to Save changes or Save schedule and enables it. A successful save stays on the page and returns the button to Saved; another edit enables saving again. Reverting every edit also returns to Saved. Expanding options or previewing sound does not change saved settings. A moved or missing Windows reminder may need Save schedule even when the displayed values are unchanged.\r\n\r\nCancel, Close, X and Escape ask what to do when there are unsaved edits: Save changes saves and closes, Discard changes closes without saving, and Keep editing leaves the page open. Escape or X on that confirmation keeps editing. A failed save leaves the page open and the edits unsaved. Unchanged or already saved pages close without prompting. These confirmations concern editor drafts; the main checklist still saves checkmarks when you close the app.",
        ["Progress and completion"]="Close saves your progress. Complete backups records a finished session.\r\n\r\nClose for now and File > Exit save unfinished checkmarks and quit. By default, X saves progress and hides the window in the system tray. Change that behavior in Settings > Window.\r\n\r\nComplete backups becomes available after all items are checked. It records your confirmation and closes the window. The next session after completion starts with unchecked items. An unfinished checklist keeps its checkmarks even when the next regular reminder arrives.\r\n\r\nA checked item shows when you checked its box, not proof of when a backup ran. Older checkmarks without a timestamp say Checked date not recorded. Unchecking clears that time; completing the routine clears all individual times.\r\n\r\nThe app does not independently verify backup integrity or restoreability.",
        ["Reminders and schedules"]="Settings > Reminder schedule lets you enable reminders and save a real Windows scheduled task. The page shows the actual task status and next run. The task appears directly under Task Scheduler Library, named Backup Reminder - followed by your Windows username. Additional profiles include a profile label; matching labels get a numbered suffix. Under Notification, Play a sound for scheduled reminders is on by default and can be turned off. Expand Sound options to choose Windows sound or a custom .wav file, use Browse to select it, and Preview to try the unsaved selection. Click Save schedule to keep your choice and import an available WAV into the data folder's sounds subfolder. The field shows the saved filename. Imported files keep readable names; different files with the same name receive numbered suffixes. Browse opens beside the selected file (the managed sounds folder after saving), falling back to Windows Media when no selected folder is available. Older saved sounds without filename information show Saved custom sound; Browse to select the original again to record its name. A .wav extension alone does not establish its format: a disguised Ogg, FLAC or MP3 file needs conversion to PCM WAV. Preview shows format/read errors without claiming custom playback. The original file is left untouched. If the custom file is missing, invalid or cannot play, the Windows Information sound is used. Playback follows Windows volume and sound settings. Manual launches are quiet. Scheduled launches request the selected sound once when enabled and briefly highlight the taskbar if the app is behind another window, without taking focus from an open edit. Turn off Enable scheduled reminders and click Save schedule to remove this profile's task from Windows. Your timing preferences are retained. Disable reminders before resetting or abandoning a profile.\r\n\r\nChoose a monthly day/time or a 1-365 day interval with a start date. Date and time follow your Windows regional settings. Use the calendar button to choose a start date, and review the live schedule summary. Shorter months use their last day for monthly reminders. Completing an extra session will not move or skip the regular reminder. You must be signed in to Windows; a missed reminder can run later when Windows is available. The computer is not woken from sleep. Keep the executable in a permanent folder. After moving it, open the app there and save the schedule again. Existing owned tasks with the earlier identifier name are migrated to readable names on normal launch when their app action is recognized, preserving their timing and enabled state.",
        ["Configuration and backups"]="Settings > Open data folder opens the current profile. Close the app before manually copying or replacing its files. File menu configuration backup and restore work while the app is open.\r\n\r\nsettings.json contains checklist items, groups, launch paths, icon choices, reminder timing and the scheduled-reminder sound preference/custom file path. checklist.json contains checkmarks, per-item checked timestamps and the last completion date.\r\n\r\nConfiguration backup and restore pickers start in Documents. File > Back up configuration saves a ZIP of settings, progress, custom icons and WAV sound. It does not back up your actual documents, backup tools or Windows task definitions. Unavailable custom files are reported. File > Restore configuration validates that ZIP and preserves your current configuration in recovery\\before-restore.zip before replacing it. Restore turns reminders off; check your program paths and enable reminders in Reminder schedule when ready. Treat configuration ZIPs as personal files: they contain your paths and preferences.\r\n\r\nSaved custom icons and sounds are copied into icons and sounds subfolders with profile-relative paths. Older hashed filenames are migrated on Save; when the original icon filename is unavailable, the task or group name is used. Configuration backups include checksums to detect damaged assets, and older configuration backups remain supported. Existing external references remain supported; unavailable files use the normal fallback. The default data folder is %LOCALAPPDATA%\\Backup Reminder. When updating, replace application files and keep your data folder. Do not overwrite your settings or progress with starter examples. Windows scheduled tasks are stored separately from these files. After restoring your settings on another computer, review program paths, open Reminder schedule, enable reminders and click Save schedule to register the reminder there.",
        ["Website and support"]="About links to the DGX Development website at dgx.digitalguy.me. Help > Report an issue opens the new-issue form on GitHub. You review and submit the report yourself.\r\n\r\nSign in to GitHub to submit a report. Include your app version and steps to reproduce the problem; leave out personal backup paths and private data.",
        ["Profiles and updates"]="Application files and user data are separate. By default, settings.json and checklist.json are stored in your Windows LocalAppData Backup Reminder folder.\r\n\r\nDevelopment mode uses a separate Development subfolder. An explicit --data-directory selects another profile. Only one window opens per profile. Opening the app manually again activates its existing window or open dialog. A scheduled reminder keeps that window and any edits intact and uses the configured sound and taskbar attention instead of forcing it to the front.\r\n\r\nTo update: 1. Close Backup Reminder. 2. Download and extract the new release ZIP. 3. Replace all supplied application files in the same app folder. 4. Reopen the app. Your separate data folder remains intact, and keeping the EXE at the same path preserves the scheduled reminder. If you move it, save the reminder schedule from the new location. An automatic updater is not implemented.",
        ["Keyboard and appearance"]="Alt+F opens File. Alt+S opens Settings. Alt+H opens Help. F1 opens Help. Tab moves between controls; Space toggles a focused checkbox.\r\n\r\nIn Settings > Appearance, choose System, Light, or Dark and select Save. In Settings > Window, choose how X and Minimize behave and whether to launch at Windows sign-in or start in the system tray. File > Exit saves progress and quits."
    };
    internal static string[] SearchTopics(string query)=>Topics.Where(x=>x.Key.Contains(query,StringComparison.OrdinalIgnoreCase)||x.Value.Contains(query,StringComparison.OrdinalIgnoreCase)).Select(x=>x.Key).ToArray();
    Form CreateHelp()
    {
        var dialog = NewDialog("Help & user guide", new Size(900, 620));
        dialog.Tag = "window";
        var frame = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 3, Tag = "window" };
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        dialog.Controls.Add(frame);
        var searchRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        var searchLabel = new Label { Text = "Search help", Dock = DockStyle.Top, Height = 22 };
        var searchBorder = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(10, 8, 10, 4), Tag = "window" };
        var search = new TextBox { Name = "HelpSearch", AccessibleName = "Search help", BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
        searchBorder.Controls.Add(search);
        searchBorder.Paint += (_, e) =>
        {
            using var outline = new Pen(dark ? Color.FromArgb(74, 74, 74) : Color.FromArgb(169, 169, 169));
            e.Graphics.DrawRectangle(outline, 0, 0, searchBorder.Width - 1, searchBorder.Height - 1);
            if (search.Focused)
            {
                using var focus = new Pen(Accent, 2);
                e.Graphics.DrawLine(focus, 1, searchBorder.Height - 2, searchBorder.Width - 1, searchBorder.Height - 2);
            }
        };
        search.GotFocus += (_, _) => searchBorder.Invalidate();
        search.LostFocus += (_, _) => searchBorder.Invalidate();
        searchRow.Controls.Add(searchBorder);
        searchRow.Controls.Add(searchLabel);
        frame.Controls.Add(searchRow, 0, 0);

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Tag = "window" };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 278));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        frame.Controls.Add(columns, 0, 1);
        var topicScroll = new EditorScrollArea { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 18, 0), Name = "HelpTopics" };
        var topics = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1 };
        topics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topicScroll.SetContent(topics);
        columns.Controls.Add(topicScroll, 0, 0);

        var articleScroll = new EditorScrollArea { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        var article = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Tag = "window" };
        article.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var articleTitle = new Label { Name = "HelpTopicTitle", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 14) };
        var articleBody = new Label { Name = "HelpContent", AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty };
        article.Controls.Add(articleTitle);
        article.Controls.Add(articleBody);
        article.SizeChanged += (_, _) => articleBody.MaximumSize = new Size(Math.Max(1, article.ClientSize.Width), 0);
        articleScroll.SetContent(article);
        columns.Controls.Add(articleScroll, 1, 0);

        void SelectTopic(string? topic)
        {
            articleTitle.Text = topic ?? "No matching topics";
            articleBody.Text = topic is null ? "Try a different word, such as icons, schedule or progress." : Topics[topic];
            articleScroll.SetOffset(0);
            foreach (RoundedButton button in topics.Controls)
            {
                bool active = button.Text == topic;
                button.BackColor = active ? MenuHover : ButtonColor;
                button.ForeColor = active ? Accent : TextColor;
                button.SelectedAccent = active ? Accent : null;
                button.Invalidate();
            }
        }
        void Filter()
        {
            foreach (Control child in topics.Controls.Cast<Control>().ToArray()) child.Dispose();
            string[] matches = SearchTopics(search.Text.Trim());
            foreach (string topic in matches)
            {
                var button = MakeButton(topic);
                button.Dock = DockStyle.Top;
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Padding = new Padding(10, 0, 6, 0);
                button.Margin = new Padding(6, 0, 0, 4);
                button.Click += (_, _) => SelectTopic(topic);
                topics.Controls.Add(button);
                ApplyTheme(button);
            }
            topicScroll.SetOffset(0);
            SelectTopic(matches.FirstOrDefault());
        }
        var footer = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        frame.Controls.Add(footer, 0, 2);
        var close = PageButton(footer, "Close", 88);
        close.Click += (_, _) => dialog.Close();
        dialog.CancelButton = close;
        FinishPage(dialog);
        articleScroll.ApplyBackground(WindowColor);
        search.TextChanged += (_, _) => Filter();
        Filter();
        return dialog;
    }
}
