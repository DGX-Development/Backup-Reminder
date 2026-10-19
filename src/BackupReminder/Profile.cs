using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackupReminder;

internal sealed class ChecklistEntry
{
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extra { get; set; }
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Detail { get; set; }
    public string? Target { get; set; }
    public string? Button { get; set; }
    public string? IconPath { get; set; }
    public string? IconName { get; set; }
    public int? IconIndex { get; set; }
    public string? Group { get; set; }
    public int? IconWidth { get; set; }
    public int? IconHeight { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
}
internal sealed class ChecklistSettings
{
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extra { get; set; }
    public ReminderSchedule? Schedule { get; set; }
    public bool PlayReminderSound { get; set; } = true;
    public string? ReminderSoundPath { get; set; }
    public string? ReminderSoundName { get; set; }
    public List<string> ManagedFiles { get; set; } = [];
    public Dictionary<string,string> ManagedFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int Version { get; set; }
    public List<string> RootOrder { get; set; } = [];
    public List<ChecklistEntry> Groups { get; set; } = [];
    public List<ChecklistEntry> Items { get; set; } = [];
    public static ChecklistSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<ChecklistSettings>(json, Profile.Json)
            ?? throw new InvalidDataException("Settings are empty.");
        if (settings.Version != 1 || settings.Items is null || settings.Items.Count == 0 || settings.Groups is null || settings.ManagedFiles is null || settings.ManagedFileHashes is null || settings.RootOrder is null || settings.RootOrder.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Settings need version 1 and at least one item.");
        static bool Invalid(List<ChecklistEntry> entries) => entries.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title))
            || entries.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count;
        if (Invalid(settings.Items) || Invalid(settings.Groups))
            throw new InvalidDataException("Items and groups need unique IDs and titles.");
        if (settings.Items.Any(x => !string.IsNullOrEmpty(x.Group) && !settings.Groups.Any(g => string.Equals(g.Id, x.Group, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("An item refers to an unknown group.");
        settings.Schedule?.Validate();
        return settings;
    }
}
internal sealed class ReminderSchedule
{
    public bool Enabled { get; set; }
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extra { get; set; }
    public string Mode { get; set; } = "Monthly";
    public int DayOfMonth { get; set; } = 1;
    public int IntervalDays { get; set; } = 30;
    public string StartDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string TimeOfDay { get; set; } = "09:00";
    public void Validate()
    {
        if (Mode is not ("Monthly" or "Interval") || DayOfMonth is < 1 or > 31 || IntervalDays is < 1 or > 3650
            || !DateOnly.TryParseExact(StartDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var start)
            || start < new DateOnly(1753,1,1) || start > new DateOnly(9998,12,31)
            || !TimeOnly.TryParseExact(TimeOfDay,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _))
            throw new InvalidDataException("Choose a valid monthly day or interval, start date and reminder time.");
    }
}
internal sealed class ChecklistState
{
    public int Version { get; set; } = 1;
    public List<string> Checked { get; set; } = [];
    public Dictionary<string,string> CheckedAt { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastCompleted { get; set; }
}
internal sealed class Profile
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string DirectoryPath { get; }
    public ChecklistSettings Settings { get; private set; }
    public string SettingsPath { get; }
    readonly bool previewOnly;
    string loadedSettingsText;
    public ChecklistState State { get; private set; }
    public string StatePath => Path.Combine(DirectoryPath, "checklist.json");
    public static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidDataException("Missing application resource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public Profile(string directory, string? settingsPath = null, bool preview = false)
    {
        DirectoryPath = Path.GetFullPath(directory);
        previewOnly = preview;
        string config = SettingsPath = settingsPath ?? Path.Combine(DirectoryPath, "settings.json");
        string json = File.Exists(config) ? File.ReadAllText(config) : settingsPath is not null
            ? throw new FileNotFoundException("The selected settings file does not exist.") : Resource("starter.json");
        Settings = ChecklistSettings.Parse(json);
        loadedSettingsText = json;
        if (File.Exists(StatePath))
        {
            State = ParseState(File.ReadAllText(StatePath));
        }
        else State = new();
        if (!preview && !File.Exists(config))
        {
            Directory.CreateDirectory(DirectoryPath);
            // CreateNew preserves a profile created by another process.
            using var file = new FileStream(config, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(file, new UTF8Encoding(false));
            writer.Write(json);
        }
    }
    internal static ChecklistState ParseState(string json)
    {
        var state=JsonSerializer.Deserialize<ChecklistState>(json,Json)??throw new InvalidDataException("Saved progress is empty.");
        if(string.IsNullOrWhiteSpace(state.LastCompleted))state.LastCompleted=null;
        if(state.Version!=1||state.Checked is null||state.CheckedAt is null||state.CheckedAt.Any(x=>string.IsNullOrWhiteSpace(x.Key)||!DateTimeOffset.TryParse(x.Value,out _))||(state.LastCompleted is not null&&!DateTimeOffset.TryParse(state.LastCompleted,out _)))
            throw new InvalidDataException("Saved progress is invalid. It has not been changed.");
        state.CheckedAt=new Dictionary<string,string>(state.CheckedAt,StringComparer.OrdinalIgnoreCase);
        return state;
    }
    public void Save(IEnumerable<string> checkedIds, string? completed,IReadOnlyDictionary<string,string>? checkedAt=null)
    {
        var ids=checkedIds.ToList();var selected=ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var timestamps=(checkedAt??State.CheckedAt).Where(x=>selected.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
        if(timestamps.Any(x=>!DateTimeOffset.TryParse(x.Value,out _)))throw new InvalidDataException("A checked timestamp is invalid. Progress was not changed.");
        var next = new ChecklistState { Checked = ids, LastCompleted = completed, CheckedAt=timestamps };
        Directory.CreateDirectory(DirectoryPath);
        string pending = Path.Combine(DirectoryPath, "checklist." + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(next, Json);
                file.Write(bytes);
                file.Flush(true);
            }
            File.Move(pending, StatePath, true);
            State = next;
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
    public ChecklistSettings CopySettings() => ChecklistSettings.Parse(JsonSerializer.Serialize(Settings,Json));
    internal void EnsureSettingsUnchanged()
    {
        if (!File.Exists(SettingsPath) || File.ReadAllText(SettingsPath) != loadedSettingsText)
            throw new IOException("The configuration changed outside the app. Close and reopen Backup Reminder before saving.");
    }
    public void SaveSettings(ChecklistSettings draft)
    {
        if (previewOnly) throw new InvalidOperationException("Preview mode cannot save configuration.");
        EnsureSettingsUnchanged();
        using var assets=new ProfileAssets(this);
        var prepared=ChecklistSettings.Parse(JsonSerializer.Serialize(draft,Json));
        assets.Prepare(prepared);
        string json = JsonSerializer.Serialize(prepared,Json);
        var validated = ChecklistSettings.Parse(json);
        EnsureSettingsUnchanged();
        string directory = Path.GetDirectoryName(Path.GetFullPath(SettingsPath))!;
        string pending = Path.Combine(directory,"settings."+Guid.NewGuid().ToString("N")+".pending");
        try
        {
            using (var file = new FileStream(pending,FileMode.CreateNew,FileAccess.Write))
            { file.Write(Encoding.UTF8.GetBytes(json)); file.Flush(true); }
            File.Move(pending,SettingsPath,true);
            var previous=Settings;
            Settings = validated;
            assets.Commit(previous,validated);
            loadedSettingsText = json;
        }
        finally { if(File.Exists(pending)) File.Delete(pending); }
    }
    internal bool NeedsAssetMigration=>ProfileAssets.References(Settings).Any(x=>ProfileAssets.IsLegacyName(x)&&File.Exists(ResolveAsset(x)));
    internal string ResolveAsset(string path)=>ProfileAssets.Resolve(DirectoryPath,path);
    internal void ReloadState()
    {
        var reopened=new Profile(DirectoryPath,SettingsPath);
        State=reopened.State;
    }
    public static string InstanceName(string directory) => @"Local\BackupReminder-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).TrimEnd('\\').ToUpperInvariant())));
    public static string ResolveTarget(string target) => target.ToLowerInvariant() switch
    {
        "knownfolder:documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "knownfolder:pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        _ when target.StartsWith("knownfolder:", StringComparison.OrdinalIgnoreCase) => throw new InvalidDataException("Unknown Windows folder target."),
        _ => Environment.ExpandEnvironmentVariables(target)
    };
    public static void Open(ChecklistEntry entry)
    {
        string target = ResolveTarget(entry.Target ?? "");
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return;
        }
        if (!File.Exists(target) && !Directory.Exists(target)) throw new FileNotFoundException("Target not found: " + target);
        var start = new ProcessStartInfo(target) { UseShellExecute = true };
        if (File.Exists(target))
        {
            start.Arguments = entry.Arguments ?? "";
            start.WorkingDirectory = string.IsNullOrWhiteSpace(entry.WorkingDirectory) ? Path.GetDirectoryName(target)! : ResolveTarget(entry.WorkingDirectory);
        }
        Process.Start(start);
    }
}
