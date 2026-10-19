using System.Reflection;
using Microsoft.Win32;

namespace BackupReminder;

internal sealed partial class MainWindow : Form
{
    readonly Profile profile;
    readonly Options options;
    readonly IReminderScheduler reminderScheduler;
    readonly IReminderAttention reminderAttention;
    readonly List<CheckBox> boxes = [];
    readonly Dictionary<string,string> checkedTimes=new(StringComparer.OrdinalIgnoreCase);
    bool reloadingChecklist;
    readonly List<IDisposable> owned = [];
    readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 12, 22, 12), Tag = "window" };
    readonly TableLayoutPanel stack = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Margin = Padding.Empty, Tag = "window" };
    readonly Label progress = new() { AutoSize = true, Location = new Point(26, 14) };
    readonly ProgressBar bar = new() { Location = new Point(26, 38), Size = new Size(680, 5), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    readonly Button complete = new RoundedButton() { Text = "Complete backups", Location = new Point(530, 57), Size = new Size(175, 36), Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = "primary" };
    readonly MenuStrip menu = new() { Dock = DockStyle.Top, Padding = new Padding(15, 4, 0, 4), ShowItemToolTips = true };
    readonly ToolStripMenuItem appearanceMenu = new("&Appearance");
    readonly Font body = new("Segoe UI Variable Text", 10);
    readonly Font small = new("Segoe UI Variable Text", 9);
    readonly Font title = new("Segoe UI Variable Text", 10.5f, FontStyle.Bold);
    readonly Font heading = new("Segoe UI Variable Display", 21, FontStyle.Bold);
    readonly Font groupFont = new("Segoe UI Variable Display", 14, FontStyle.Bold);
    readonly Image brand;
    bool completed;
    bool dark;
    string appearance;
    internal int ItemCount => boxes.Count;
    internal bool CanComplete => complete.Enabled;
    internal void SetAllChecks(bool value) { foreach (var box in boxes) box.Checked = value; }
    internal void SetFirstCheck(bool value) => boxes[0].Checked = value;
    Color WindowColor => ColorTranslator.FromHtml(dark ? "#202020" : "#F3F3F3");
    Color Surface => ColorTranslator.FromHtml(dark ? "#2B2B2B" : "#FFFFFF");
    Color TextColor => ColorTranslator.FromHtml(dark ? "#F2F4F8" : "#1C2534");
    Color Muted => ColorTranslator.FromHtml(dark ? "#B0B8C6" : "#596779");
    Color Accent => ColorTranslator.FromHtml(dark ? "#78B7FF" : "#2468BB");
    Color ButtonColor => ColorTranslator.FromHtml(dark ? "#353535" : "#FAFAFA");
    Color DisabledColor => ColorTranslator.FromHtml(dark ? "#353C46" : "#DDE3EB");

    public MainWindow(Profile profile, Options options,IReminderScheduler? scheduler=null,IReminderAttention? attention=null)
    {
        this.profile = profile; this.options = options; appearance = options.Appearance;
        reminderScheduler=scheduler??(options.PreviewPath is not null||options.TestDirectory is not null?new MemoryReminderScheduler():new WindowsReminderScheduler(profile.DirectoryPath,profile.SettingsPath,Environment.ProcessPath!));
        reminderAttention=attention??(options.PreviewPath is not null||options.TestDirectory is not null||options.SmokeTest?new RecordingReminderAttention():new WindowsReminderAttention());
        foreach(var pair in profile.State.CheckedAt)if(profile.State.Checked.Contains(pair.Key,StringComparer.OrdinalIgnoreCase))checkedTimes[pair.Key]=pair.Value;
        owned.AddRange([body, small, title, heading, groupFont]);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.png")!)
        using (var loaded = Image.FromStream(stream)) brand = new Bitmap(loaded);
        owned.Add(brand);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.ico")!) Icon = new Icon(stream);
        owned.Add(Icon);
        Text = options.Development ? "Backup Reminder - Development" : "Backup Reminder";
        Size = new Size(760, 960); MinimumSize = new Size(700, 580); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; Font = body;
        var header = new Panel { Dock = DockStyle.Top, Height = 118 };
        header.Controls.Add(new BrandPictureBox { Image = brand, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(24,25,58,58) });
        header.Controls.Add(new Label { Text = "Backup Reminder", Font = heading, Bounds = new Rectangle(99,21,550,42) });
        string last = profile.State.LastCompleted is null ? "No completion recorded" : DateTimeOffset.Parse(profile.State.LastCompleted).ToLocalTime().ToString("MMM d, yyyy  h:mm tt");
        header.Controls.Add(new Label { Name="LastCompleted", Text = (options.Development ? "Development profile  |  " : "") + "Last completed: " + last, Tag = "muted", Bounds = new Rectangle(102,67,590,25) });
        Controls.Add(header);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 112, Width = ClientSize.Width };
        footer.Controls.Add(progress); footer.Controls.Add(bar); footer.Controls.Add(complete);
        var close = MakeButton("Close for now"); close.SetBounds(26,57,145,36); close.Click += (_,_) => Close(); footer.Controls.Add(close);
        Controls.Add(footer);
        Controls.Add(scroll); scroll.BringToFront();
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); scroll.Controls.Add(stack);
        stack.Controls.Add(new Label { Text = "Check each item after completing its backup.", Dock = DockStyle.Fill, Height = 35, Margin = new Padding(3,2,0,10), Tag = "muted" });
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in profile.Settings.Items)
        {
            if (!string.IsNullOrEmpty(entry.Group) && shown.Add(entry.Group))
                AddGroup(profile.Settings.Groups.Single(g => string.Equals(g.Id,entry.Group,StringComparison.OrdinalIgnoreCase)));
            AddItem(entry);
        }
        BuildMenu(); Controls.Add(menu); MainMenuStrip = menu;
        stack.SizeChanged += (_,_) => LayoutRows(); Shown += (_,_) => LayoutRows();
        bar.Maximum = boxes.Count;
        complete.Click += (_,_) => CompleteBackups();
        FormClosing += (_, e) =>
        {
            if(e.CloseReason==CloseReason.UserClosing&&VisibleDialog() is not null)
            {
                e.Cancel=true;ActivateWindow();return;
            }
            if (options.PreviewPath is not null || completed) return;
            try { profile.Save(boxes.Where(x => x.Checked).Select(x => (string)x.Tag!), profile.State.LastCompleted,checkedTimes); }
            catch (Exception error) { e.Cancel = true; ShowError("Could not save progress: " + error.Message); }
        };
        if(options.PreviewPath is null&&options.TestDirectory is null&&!options.SmokeTest&&reminderScheduler is WindowsReminderScheduler windowsScheduler)
            Shown+=(_,_)=>{try{windowsScheduler.UpgradeLegacyLaunch();}catch(Exception error){ShowError("Could not update the reminder task. Reopen Reminder schedule and save it again. "+error.Message);}};
        SetAppearance(appearance);
        HandleCreated+=(_,_)=>WindowStyle.Apply(this,dark);
    }
    Button MakeButton(string text) => new RoundedButton() { Text = text, FlatStyle = FlatStyle.Flat, Font = title, Cursor = Cursors.Hand, Size = new Size(120,36) };
    void AddGroup(ChecklistEntry entry)
    {
        var row = new Panel { Dock = DockStyle.Fill, Height = 78, Margin = new Padding(0,16,0,6), Tag = "window" };
        row.Controls.Add(new PictureBox { Image = LoadIcon(entry), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(12,9,32,32) });
        row.Controls.Add(new Label { Text = entry.Title, Font = groupFont, Bounds = new Rectangle(54,7,350,30), Tag = "name" });
        row.Controls.Add(new Label { Text = entry.Detail, Font = small, Bounds = new Rectangle(12,48,630,25), Tag = "muted" });
        if (!string.IsNullOrWhiteSpace(entry.Target)) AddLaunch(row,entry,164,7);
        stack.Controls.Add(row);
    }
    void AddItem(ChecklistEntry entry)
    {
        var row = new RoundedPanel { Dock = DockStyle.Fill, Height = 80, Margin = new Padding(0,0,0,8) };
        var check = new CheckBox { Tag = entry.Id, Checked = profile.State.Checked.Contains(entry.Id,StringComparer.OrdinalIgnoreCase), AccessibleName = entry.Title + " backup completed", Bounds = new Rectangle(14,27,25,26) };
        row.Controls.Add(check); boxes.Add(check);
        bool hasIcon = !string.IsNullOrWhiteSpace(entry.Target) || !string.IsNullOrWhiteSpace(entry.IconPath);
        int x = hasIcon ? 87 : 47;
        if (hasIcon)
        {
            int width = Math.Clamp(entry.IconWidth ?? 30,16,40), height = Math.Clamp(entry.IconHeight ?? 30,16,48);
            row.Controls.Add(new PictureBox { Image = LoadIcon(entry), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(61-width/2,39-height/2,width,height) });
        }
        row.Controls.Add(new Label { Text = entry.Title, Font = title, Bounds = new Rectangle(x,14,400,25), Tag = "name" });
        row.Controls.Add(new Label { Text = entry.Detail, Font = small, Bounds = new Rectangle(x,40,400,35), Tag = "muted" });
        var checkedLabel=new Label {Name="CheckedAt",Font=small,Bounds=new Rectangle(x,76,400,22),Tag="muted",Visible=false};row.Controls.Add(checkedLabel);
        check.CheckedChanged+=(_,_)=>
        {
            if(!reloadingChecklist)
            {
                if(check.Checked)checkedTimes[entry.Id]=DateTimeOffset.Now.ToString("o");
                else checkedTimes.Remove(entry.Id);
            }
            RefreshCheckedLabel(check);UpdateProgress();LayoutRows();
        };
        if (!string.IsNullOrWhiteSpace(entry.Target)) AddLaunch(row,entry,120,22);
        stack.Controls.Add(row);RefreshCheckedLabel(check);
    }
    void RefreshCheckedLabel(CheckBox check)
    {
        var row=(Panel)check.Parent!;var label=row.Controls.OfType<Label>().Single(x=>x.Name=="CheckedAt");
        label.Visible=check.Checked;
        label.Text=checkedTimes.TryGetValue((string)check.Tag!,out string? stamp)?"Checked "+DateTimeOffset.Parse(stamp).ToLocalTime().ToString("MMM d, yyyy, h:mm tt"):"Checked date not recorded";
        check.AccessibleDescription=check.Checked?label.Text:null;
        row.Height=(int)Math.Round((check.Checked?104:80)*DeviceDpi/96f);
    }
    void AddLaunch(Panel row, ChecklistEntry entry, int width, int top)
    {
        var button = MakeButton(entry.Button ?? "Open"); button.SetBounds(530,top,width,36);
        button.Click += (_,_) => { try { Profile.Open(entry); } catch (Exception error) { ShowError(error.Message); } };
        row.Controls.Add(button);
    }
    Image LoadIcon(ChecklistEntry entry,bool track=true)
    {
        string source = string.IsNullOrWhiteSpace(entry.IconPath)?entry.Target??"":entry.IconPath;
        try
        {
            source = string.IsNullOrWhiteSpace(entry.IconPath)?Profile.ResolveTarget(source):profile.ResolveAsset(source);
            if (File.Exists(source))
            {
                Image result;
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" }.Contains(extension))
                { using var image = Image.FromFile(source); result = new Bitmap(image); }
                else if (extension == ".ico") { using var icon = new Icon(source,64,64); result = icon.ToBitmap(); }
                else { result=entry.IconIndex is int index?ShellIcons.Read(source,index):ShellIcons.Associated(source); }
                if(track)owned.Add(result); return result;
            }
        }
        catch { /* A missing custom icon must not prevent the checklist from opening. */ }
        var bitmap = new Bitmap(40,40);
        using var graphics = Graphics.FromImage(bitmap); graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(ColorTranslator.FromHtml("#2468BB")); graphics.FillEllipse(brush,2,2,36,36);
        using var font = new Font("Segoe UI Semibold",15);
        using var glyph = new System.Drawing.Drawing2D.GraphicsPath();
        string initial=System.Globalization.StringInfo.GetNextTextElement(string.IsNullOrWhiteSpace(entry.Title)?"?":entry.Title);
        glyph.AddString(initial,font.FontFamily,(int)font.Style,20,Point.Empty,StringFormat.GenericTypographic);
        var ink=glyph.GetBounds();
        using var shift=new System.Drawing.Drawing2D.Matrix();
        shift.Translate(20-(ink.Left+ink.Width/2),20-(ink.Top+ink.Height/2));
        glyph.Transform(shift);graphics.FillPath(Brushes.White,glyph);
        if(track)owned.Add(bitmap); return bitmap;
    }
    void LayoutRows()
    {
        float scale = DeviceDpi / 96f;
        foreach (var row in stack.Controls.OfType<Panel>())
        {
            var button = row.Controls.OfType<Button>().FirstOrDefault();
            if (button is not null) button.Left = row.ClientSize.Width-button.Width-(int)(16*scale);
            foreach (var label in row.Controls.OfType<Label>())
            {
                int right = button is null || (string?)label.Tag == "muted" && row.Tag is string ? (int)(16*scale) : button.Width+(int)(32*scale);
                label.Width = Math.Max((int)(80*scale),row.ClientSize.Width-label.Left-right);
            }
        }
    }
    void UpdateProgress()
    {
        int count = boxes.Count(x => x.Checked); progress.Text = $"{count} of {boxes.Count} completed";
        complete.Enabled = count == boxes.Count; bar.Value = count;
        complete.BackColor = complete.Enabled ? Accent : DisabledColor;
        complete.ForeColor = dark ? ColorTranslator.FromHtml("#142337") : Color.White;
        if (!complete.Enabled) complete.ForeColor = TextColor;
    }
    void CompleteBackups()
    {
        if (!CanComplete) return;
        try
        {
            profile.Save([], DateTimeOffset.Now.ToString("o")); completed = true;
            MessageBox.Show(this,"Your backup checklist completion has been recorded.","Backup Reminder"); Close();
        }
        catch (Exception error) { ShowError("Could not save completion: " + error.Message); }
    }
    void ShowError(string message) => MessageBox.Show(this,message,"Backup Reminder",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    void SetAppearance(string choice)
    {
        appearance = choice; dark = choice == "Dark";
        if (choice == "System")
        {
            try { dark = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) == 0; }
            catch { dark = false; }
        }
        ApplyTheme(this); BackColor = WindowColor;
        WindowStyle.Apply(this,dark);
        foreach (ToolStripMenuItem item in appearanceMenu.DropDownItems) item.Checked = (string?)item.Tag == choice;
        RefreshMenuGlyphs();
        var renderer=new AppMenuRenderer(new MenuColors(Surface,ButtonColor){UseSystemColors=false},Accent);
        menu.Renderer=renderer;
        void ColorMenus(ToolStripItemCollection entries)
        {
            foreach (ToolStripItem item in entries)
            {
                item.BackColor=Surface;item.ForeColor=TextColor;
                if(item is ToolStripMenuItem submenu&&submenu.HasDropDownItems)
                {
                    submenu.DropDown.Renderer=renderer;submenu.DropDown.BackColor=Surface;submenu.DropDown.ForeColor=TextColor;
                    if(submenu.DropDown is ToolStripDropDownMenu dropdown){dropdown.ShowCheckMargin=submenu==appearanceMenu;dropdown.ShowImageMargin=submenu!=appearanceMenu;}
                    ColorMenus(submenu.DropDownItems);
                }
            }
        }
        ColorMenus(menu.Items); UpdateProgress(); Invalidate(true);
    }
    void ApplyTheme(Control control)
    {
        control.BackColor = (string?)control.Tag == "window" ? WindowColor : control is Label or PictureBox or CheckBox ? control.Parent?.BackColor ?? Surface : Surface;
        control.ForeColor = (string?)control.Tag == "muted" ? Muted : TextColor;
        if (control is Button button)
        { button.FlatStyle = FlatStyle.Flat; button.Font = title; button.Cursor = Cursors.Hand; button.BackColor = ButtonColor; button.ForeColor = dark ? TextColor : ColorTranslator.FromHtml("#235993"); button.FlatAppearance.BorderSize = 0; }
        if (control is LinkLabel link) { link.LinkColor = Accent; link.ActiveLinkColor = Accent; }
        if(control is RoundedPanel card) { card.BorderColor=ColorTranslator.FromHtml(dark?"#3B3B3B":"#E5E5E5");card.Invalidate(); }
        if(control is RoundedButton rounded) { rounded.BorderColor=ColorTranslator.FromHtml(dark?"#4A4A4A":"#DCDCDC");rounded.DisabledTextColor=Muted;rounded.Invalidate(); }
        if(control is EditorPicker picker) {picker.BackColor=WindowColor;picker.ForeColor=TextColor;picker.Font=body;picker.PickerRenderer=new AppMenuRenderer(new MenuColors(WindowColor,ButtonColor){UseSystemColors=false},Accent);}
        if(control is ScheduleDateTimeInput dateTime)dateTime.ApplyColors(WindowColor,ButtonColor,TextColor,Muted);
        if(control is ThemeNumberInput number)number.ApplyColors(WindowColor,ButtonColor,TextColor,Muted,Accent);
        if(control is TextBoxBase or ListBox or ComboBox or NumericUpDown) {control.BackColor=WindowColor;control.ForeColor=TextColor;}
        foreach (Control child in control.Controls) ApplyTheme(child);
    }
    internal void RenderPreview(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Show(); Application.DoEvents(); LayoutRows();
        bool[] original = boxes.Select(x=>x.Checked).ToArray();
        reloadingChecklist=true;
        SetAllChecks(true); if (!CanComplete || bar.Value != boxes.Count) throw new InvalidDataException("Completion gate failed.");
        SetFirstCheck(false); if (CanComplete) throw new InvalidDataException("Incomplete gate failed.");
        for(int i=0;i<boxes.Count;i++) boxes[i].Checked=original[i];
        reloadingChecklist=false;
        string initial=appearance; SetAppearance("Dark"); SetAppearance("Light"); SetAppearance(initial);
        foreach (var row in stack.Controls.OfType<Panel>())
            foreach(var button in row.Controls.OfType<Button>())
                if(button.Left<0 || button.Right>row.ClientSize.Width) throw new InvalidDataException("Launch button outside row.");
        RenderForm(this,path);
        foreach(int index in new[]{0,2})
        {
            var commandMenu=(ToolStripMenuItem)menu.Items[index];commandMenu.ShowDropDown();Application.DoEvents();
            RenderMenu(commandMenu.DropDown,Path.ChangeExtension(path,null)+(index==0?"-file-menu.png":"-help-menu.png"));commandMenu.HideDropDown();
        }
        var settingsMenu=(ToolStripMenuItem)menu.Items[1];settingsMenu.ShowDropDown();appearanceMenu.Select();Application.DoEvents();
        RenderMenu(settingsMenu.DropDown,Path.ChangeExtension(path,null)+"-settings-menu.png");
        appearanceMenu.ShowDropDown();appearanceMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(x=>x.Checked).Select();Application.DoEvents();
        RenderMenu(appearanceMenu.DropDown,Path.ChangeExtension(path,null)+"-appearance-menu.png");
        appearanceMenu.HideDropDown();settingsMenu.HideDropDown();
        using(var help=CreateHelp()) RenderForm(help,Path.ChangeExtension(path,null)+"-help.png");
        using(var about=CreateAbout()) RenderForm(about,Path.ChangeExtension(path,null)+"-about.png");
        using(var confirmation=CreateUnsavedPrompt()) RenderForm(confirmation,Path.ChangeExtension(path,null)+"-unsaved.png");
        using(var editor=CreateChecklistEditor())
        {
            RenderForm(editor,Path.ChangeExtension(path,null)+"-editor.png");
            var picker=Descendants<EditorPicker>(editor).Single(x=>x.Name=="OpenKind");
            picker.PerformClick();Application.DoEvents();
            using(var menuImage=new Bitmap(picker.ContextMenuStrip!.Width,picker.ContextMenuStrip.Height))
            {picker.ContextMenuStrip.DrawToBitmap(menuImage,new Rectangle(Point.Empty,menuImage.Size));menuImage.Save(Path.ChangeExtension(path,null)+"-picker.png",System.Drawing.Imaging.ImageFormat.Png);}
            picker.ContextMenuStrip.Close();
            Descendants<Button>(editor).Single(x=>x.Name=="MoreOptions").PerformClick();
            Application.DoEvents();
            var fieldPanel=Descendants<EditorScrollArea>(editor).Single(x=>x.Name=="EditorFields");
            var iconInput=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryIcon");
            fieldPanel.ScrollControlIntoView(iconInput.Parent!.Parent!);
            RenderForm(editor,Path.ChangeExtension(path,null)+"-editor-options.png");
            editor.Size=editor.MinimumSize;Application.DoEvents();fieldPanel.ScrollToEnd();
            RenderForm(editor,Path.ChangeExtension(path,null)+"-editor-bottom.png");
        }
        using(var schedule=CreateScheduleSettings())
        {
            RenderForm(schedule,Path.ChangeExtension(path,null)+"-schedule.png");
            Descendants<EditorPicker>(schedule).Single(x=>x.Name=="ScheduleFrequency").SelectedIndex=1;
            RenderForm(schedule,Path.ChangeExtension(path,null)+"-schedule-interval.png");
            Descendants<Button>(schedule).Single(x=>x.Name=="SoundOptions").PerformClick();
            Descendants<EditorPicker>(schedule).Single(x=>x.Name=="ReminderSoundKind").SelectedIndex=1;
            Application.DoEvents();Descendants<EditorScrollArea>(schedule).Single().ScrollToEnd();
            RenderForm(schedule,Path.ChangeExtension(path,null)+"-schedule-sound.png");
        }
        Size=MinimumSize; Application.DoEvents(); LayoutRows(); RenderForm(this,Path.ChangeExtension(path,null)+"-narrow.png");
        Close();
    }
    static void RenderMenu(ToolStripDropDown dropdown,string path)
    {
        using var bitmap=new Bitmap(dropdown.Width,dropdown.Height);
        dropdown.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
    static void RenderForm(Form form,string path)
    {
        form.Show(); Application.DoEvents();
        using var bitmap = new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing) foreach(var resource in owned) resource.Dispose();
    }
    sealed class MenuColors(Color surface,Color hover):ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground=>surface;
        public override Color ImageMarginGradientBegin=>surface;
        public override Color ImageMarginGradientMiddle=>surface;
        public override Color ImageMarginGradientEnd=>surface;
        public override Color MenuItemSelected=>hover;
        public override Color MenuItemSelectedGradientBegin=>hover;
        public override Color MenuItemSelectedGradientEnd=>hover;
        public override Color MenuItemPressedGradientBegin=>hover;
        public override Color MenuItemPressedGradientMiddle=>hover;
        public override Color MenuItemPressedGradientEnd=>hover;
    }
}
