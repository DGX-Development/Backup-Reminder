namespace BackupReminder;

internal sealed class Options
{
    public string DataDirectory { get; private set; } = "";
    public string? SettingsPath { get; private set; }
    public string? PreviewPath { get; private set; }
    public string Appearance { get; private set; } = "System";
    public bool Development { get; private set; }
    public string? TestDirectory { get; private set; }
    public bool SmokeTest { get; private set; }
    public bool Scheduled { get; private set; }
    public string? SchedulerTestDirectory { get; private set; }
    public static Options Parse(string[] args)
    {
        var result = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing value for " + args[i - 1]);
            switch (args[i].ToLowerInvariant())
            {
                case "--data-directory": result.DataDirectory = Path.GetFullPath(Value()); break;
                case "--settings": result.SettingsPath = Path.GetFullPath(Value()); break;
                case "--preview": result.PreviewPath = Path.GetFullPath(Value()); break;
                case "--appearance": result.Appearance = Value(); break;
                case "--development": result.Development = true; break;
                case "--scheduler-test": result.SchedulerTestDirectory=Path.GetFullPath(Value()); break;
                case "--self-test": result.TestDirectory = Path.GetFullPath(Value()); break;
                case "--scheduled": result.Scheduled=true; break;
                case "--smoke-test": result.SmokeTest = true; break;
                default: throw new ArgumentException("Unknown option: " + args[i]);
            }
        }
        if (!new[] { "System", "Light", "Dark" }.Contains(result.Appearance)) throw new ArgumentException("Appearance must be System, Light or Dark.");
        if (result.DataDirectory.Length == 0)
            result.DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Backup Reminder", result.Development ? "Development" : "");
        return result;
    }
}
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var options = Options.Parse(args);
            if (options.TestDirectory is not null) return Verification.Run(options.TestDirectory);
            if(options.SchedulerTestDirectory is not null)return SchedulerVerification.Run(options.SchedulerTestDirectory);
            bool preview = options.PreviewPath is not null;
            string name = Profile.InstanceName(options.DataDirectory);
            using var mutex = new Mutex(false, name);
            using var activate = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-activate");
            using var reminder=new EventWaitHandle(false,EventResetMode.AutoReset,name+"-reminder");
            bool owns = false;
            try
            {
                if (!preview)
                {
                    try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
                    if (!owns) { if(options.Scheduled)reminder.Set();else activate.Set(); return 0; }
                }
                var profile = new Profile(options.DataDirectory, options.SettingsPath, preview);
                using var form = new MainWindow(profile, options);
                using var timer = new System.Windows.Forms.Timer { Interval = 200 };
                timer.Tick += (_, _) =>
                {
                    if(activate.WaitOne(0))form.ActivateWindow();
                    if(reminder.WaitOne(0))form.HandleScheduledReminder();
                };
                if(options.Scheduled&&!preview)form.Shown+=(_,_)=>form.HandleScheduledReminder();
                if (!preview) timer.Start();
                if (preview) form.RenderPreview(options.PreviewPath!);
                else if (options.SmokeTest)
                {
                    using var closeTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                    closeTimer.Tick += (_, _) => { closeTimer.Stop(); form.Close(); };
                    closeTimer.Start(); Application.Run(form);
                }
                else Application.Run(form);
            }
            finally { if (owns) mutex.ReleaseMutex(); }
            return 0;
        }
        catch (Exception error)
        {
            // Automated checks return failure without blocking on an unattended dialog.
            int previewIndex = Array.IndexOf(args, "--preview");
            if (previewIndex >= 0 && previewIndex + 1 < args.Length)
            {
                try { File.WriteAllText(Path.GetFullPath(args[previewIndex + 1]) + ".error.txt", error.ToString()); } catch { }
            }
            if (!args.Contains("--preview") && !args.Contains("--self-test") && !args.Contains("--smoke-test") && !args.Contains("--scheduler-test"))
                MessageBox.Show(error.Message, "Backup Reminder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
