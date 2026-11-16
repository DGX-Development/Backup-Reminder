using Microsoft.Win32;
using System.Text.Json;

namespace BackupReminder;

internal sealed record WindowPreferences(
    string Appearance = "System", bool CloseToTray = true, bool MinimizeToTray = true,
    bool StartWithWindows = false, bool StartMinimized = false,
    bool LockWindowSizes = false, Dictionary<string, double[]>? WindowSizes = null);

internal sealed partial class MainWindow
{
    readonly WindowPreferences defaults = new();
    WindowPreferences? preferences;
    NotifyIcon? trayIcon;
    bool exiting;
    string PreferencesPath => Path.Combine(profile.DirectoryPath, "window-preferences.json");
    bool UsesTray => options.PreviewPath is null && options.TestDirectory is null && !options.SmokeTest;
    internal bool StartInTray => UsesTray && !options.Scheduled && preferences?.StartMinimized == true;

    void LoadWindowPreferences()
    {
        preferences = defaults;
        try
        {
            if (File.Exists(PreferencesPath))
                preferences = JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllText(PreferencesPath), Profile.Json) ?? defaults;
        }
        catch (IOException) { }
        catch (JsonException) { }
        appearance = options.AppearanceOverride ?? preferences.Appearance;
    }

    void HideToTray()
    {
        if (trayIcon is null)
        {
            var commands = new ContextMenuStrip { Font = body, BackColor = Surface, ForeColor = TextColor };
            commands.Renderer = new AppMenuRenderer(new MenuColors(Surface, MenuHover) { UseSystemColors = false }, Accent);
            commands.Items.Add("Open Backup Reminder", null, (_, _) => ActivateWindow());
            commands.Items.Add(new ToolStripSeparator());
            commands.Items.Add("Exit", null, (_, _) => ExitApplication());
            AppMenuRenderer.ApplySpacing(commands);
            trayIcon = new NotifyIcon { Icon = Icon, Text = "Backup Reminder", ContextMenuStrip = commands };
            trayIcon.DoubleClick += (_, _) => ActivateWindow();
            owned.Add(commands);
            owned.Add(trayIcon);
        }
        trayIcon.Visible = true;
        Hide();
    }

    protected override void WndProc(ref Message message)
    {
        const int systemCommand = 0x0112;
        const long minimize = 0xf020;
        if (message.Msg == systemCommand && (message.WParam.ToInt64() & 0xfff0) == minimize
            && UsesTray && preferences?.MinimizeToTray == true && VisibleDialog() is null)
        {
            HideToTray();
            return;
        }
        base.WndProc(ref message);
    }

    void ExitApplication()
    {
        exiting = true;
        try { Close(); }
        finally { exiting = false; }
    }

    internal void StartHidden() => HideToTray();

    void SaveWindowPreferences(WindowPreferences next)
    {
        Directory.CreateDirectory(profile.DirectoryPath);
        string temporary = PreferencesPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool changeStartup = options.UsesDefaultProfile && UsesTray && next.StartWithWindows != preferences?.StartWithWindows;
        using var run = changeStartup
            ? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run") : null;
        const string name = "DGX.BackupReminder";
        object? previous = run?.GetValue(name);
        bool startupChanged = run is not null;
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(next, Profile.Json));
            if (startupChanged)
            {
                if (next.StartWithWindows) run!.SetValue(name, "\"" + Environment.ProcessPath + "\"");
                else run!.DeleteValue(name, false);
            }
            File.Move(temporary, PreferencesPath, true);
        }
        catch
        {
            if (startupChanged)
            {
                if (previous is null) run!.DeleteValue(name, false);
                else run!.SetValue(name, previous);
            }
            throw;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        preferences = next;
        SetAppearance(next.Appearance);
    }

    Form CreateSettings()
    {
        var dialog = CreateScheduleSettings();
        dialog.Text = "Backup Reminder settings";
        dialog.MinimumSize = new Size(620, 550);
        dialog.Size = new Size(740, 720);
        var pages = (TableLayoutPanel)dialog.Controls[0];
        var header = (Panel)pages.GetControlFromPosition(0, 0)!;
        var schedule = (Panel)pages.GetControlFromPosition(0, 1)!;
        var scheduleFooter = (Panel)pages.GetControlFromPosition(0, 2)!;
        foreach (Control control in header.Controls.Cast<Control>().ToArray()) control.Dispose();
        pages.RowStyles[0].Height = 72;
        var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 24, 0, 0) };
        header.Controls.Add(tabs);

        var appearancePage = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Tag = "window" };
        appearancePage.Controls.Add(new Label { Text = "Appearance", Font = groupFont, AutoSize = true, Margin = new Padding(0, 8, 0, 16) });
        var theme = new EditorPicker { Width = 240, Height = 36 };
        foreach (string choice in new[] { "System", "Light", "Dark" }) theme.AddChoice(choice, choice);
        theme.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "System", "Light", "Dark" }, preferences!.Appearance));
        appearancePage.Controls.Add(theme);

        var windowPage = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Tag = "window" };
        var windowScroll = new EditorScrollArea { Dock = DockStyle.Fill, Tag = "window" };
        windowScroll.SetContent(windowPage);
        void Heading(string label) => windowPage.Controls.Add(new Label { Text = label, Font = groupFont, AutoSize = true, Margin = new Padding(0, 12, 0, 8) });
        ThemeCheckBox Option(string label, bool value)
        {
            var box = new ThemeCheckBox { Text = label, Checked = value, Width = 440, Height = 36, Margin = Padding.Empty };
            windowPage.Controls.Add(box);
            return box;
        }
        Heading("Window & startup");
        Heading("Window buttons");
        var closeToTray = Option("Close to the system tray", preferences.CloseToTray);
        var minimizeToTray = Option("Minimize to the system tray", preferences.MinimizeToTray);
        windowPage.Controls.Add(new Label { Text = "Use File > Exit to quit Backup Reminder.", AutoSize = true, Tag = "muted" });
        Heading("Startup");
        var startWithWindows = Option("Start when I sign in to Windows", preferences.StartWithWindows);
        startWithWindows.Enabled = options.UsesDefaultProfile;
        var startMinimized = Option("Start minimized in the system tray", preferences.StartMinimized);

        var settingsFooter = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        var cancel = PageButton(settingsFooter, "Cancel", 88);
        var save = PageButton(settingsFooter, "Save", 88);
        cancel.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            try
            {
                SaveWindowPreferences(new(new[] { "System", "Light", "Dark" }[theme.SelectedIndex],
                    closeToTray.Checked, minimizeToTray.Checked, startWithWindows.Checked, startMinimized.Checked));
                dialog.Close();
            }
            catch (Exception error) { ShowNotice(dialog, error.Message, "Could not save settings"); }
        };

        var contentHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        var footerHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "window" };
        pages.Controls.Remove(schedule);
        pages.Controls.Remove(scheduleFooter);
        pages.Controls.Add(contentHost, 0, 1);
        pages.Controls.Add(footerHost, 0, 2);
        contentHost.Controls.AddRange([appearancePage, windowScroll, schedule]);
        footerHost.Controls.AddRange([settingsFooter, scheduleFooter]);
        var tabNames = new[] { "Appearance", "Window", "Reminder schedule" };
        var tabPages = new Control[] { appearancePage, windowScroll, schedule };
        var tabButtons = tabNames.Select(MakeButton).ToArray();
        void SelectPage(int index)
        {
            for (int i = 0; i < tabPages.Length; i++)
            {
                tabPages[i].Visible = i == index;
                tabButtons[i].BackColor = i == index ? MenuHover : ButtonColor;
                tabButtons[i].ForeColor = i == index ? Accent : TextColor;
                ((RoundedButton)tabButtons[i]).SelectedAccent = i == index ? Accent : null;
                tabButtons[i].Invalidate();
            }
            settingsFooter.Visible = index != 2;
            scheduleFooter.Visible = index == 2;
            dialog.CancelButton = index == 2 ? Descendants<Button>(scheduleFooter).Single(button => button.Name == "CancelSchedule") : cancel;
        }
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].Margin = new Padding(0, 0, 8, 0);
            tabButtons[i].Width = i == 2 ? 150 : 100;
            tabButtons[i].Click += (_, _) => SelectPage(index);
            tabs.Controls.Add(tabButtons[i]);
        }
        int selectedPage = 0;
        theme.SelectedIndexChanged += (_, _) =>
        {
            SetAppearance(new[] { "System", "Light", "Dark" }[theme.SelectedIndex]);
            FinishPage(dialog);
            theme.BackColor = ButtonColor;
            SelectPage(selectedPage);
        };
        dialog.FormClosed += (_, _) => SetAppearance(preferences!.Appearance);
        foreach (Control control in tabs.Controls)
        {
            int index = tabs.Controls.IndexOf(control);
            control.Click += (_, _) => selectedPage = index;
        }
        FinishPage(dialog);
        theme.BackColor = ButtonColor;
        SelectPage(0);
        return dialog;
    }
}
