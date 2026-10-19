using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace BackupReminder;

internal sealed record ReminderTask(string? Xml=null,bool Enabled=false,DateTime? NextRun=null,string? Executable=null,string? Name=null);
internal interface IReminderScheduler
{
    ReminderTask Read();
    void Apply(ReminderSchedule schedule);
    void Restore(ReminderTask snapshot);
}
internal sealed class WindowsReminderScheduler(string directory,string settingsPath,string executable):IReminderScheduler
{
    const string Source="DGX.BackupReminder/v1";
    readonly string sid=WindowsIdentity.GetCurrent().User?.Value??throw new InvalidOperationException("Windows user identity is unavailable.");
    internal string LegacyTaskName=>"Backup Reminder - "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid+"|"+Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)).ToUpperInvariant()+"|"+Path.GetFullPath(settingsPath).ToUpperInvariant())))[..20];
    string Description=>"Opens the manual backup checklist. Profile: "+Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    string ProfileDescription=>Description+"\nSettings: "+Path.GetFullPath(settingsPath);
    internal static string FriendlyName(string user,string directory,string settingsPath)
    {
        static string Clean(string text)=>new string(text.Select(ch=>char.IsControl(ch)||"\\/:*?\"<>|".Contains(ch)?'_':ch).ToArray()).Trim();
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Backup Reminder");
        bool standard=string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),root,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(Path.GetFullPath(settingsPath),Path.Combine(root,"settings.json"),StringComparison.OrdinalIgnoreCase);
        string label=Clean(Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)));
        if(label.Length>60)label=label[..60];
        return "Backup Reminder - "+Clean(user)+(standard?"":" - "+(label.Length==0?"Profile":label));
    }
    internal string TaskName {get{using var connection=new Connection();return ResolveName(connection);}}
    internal static (int Months,int Day,bool Last)[] MonthlyParts(int day)=>day switch
    {
        <=28=>[(4095,day,false)],
        29 or 30=>[(4093,day,false),(2,0,true)],
        31=>[(1|4|16|64|128|512|2048,31,false),(2|8|32|256|1024,0,true)],
        _=>throw new ArgumentOutOfRangeException(nameof(day))
    };
    internal static string Quote(string path)
    {
        // CommandLineToArgvW quoting, including paths ending with a backslash.
        var value=new System.Text.StringBuilder("\"");int slashes=0;
        foreach(char ch in path)
        {
            if(ch=='\\'){slashes++;continue;}
            if(ch=='\"')value.Append('\\',slashes*2+1).Append(ch);
            else value.Append('\\',slashes).Append(ch);
            slashes=0;
        }
        return value.Append('\\',slashes*2).Append('\"').ToString();
    }
    internal string Arguments=>"--data-directory "+Quote(Path.GetFullPath(directory))+" --settings "+Quote(Path.GetFullPath(settingsPath));
    static bool Missing(Exception error)=>error.HResult==unchecked((int)0x80070002);
    sealed class Connection:IDisposable
    {
        readonly List<object> objects=[];
        internal dynamic Keep(object value){if(!objects.Any(item=>ReferenceEquals(item,value)))objects.Add(value);return value;}
        internal dynamic Folder {get;}
        internal dynamic Service {get;}
        internal Connection()
        {
            Service=Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")??throw new InvalidOperationException("Windows Task Scheduler is unavailable."))!);
            try{Service.Connect();Folder=Keep(Service.GetFolder("\\"));}catch{Dispose();throw;}
        }
        public void Dispose(){foreach(var value in objects.AsEnumerable().Reverse())if(Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
    }
    dynamic? GetTask(Connection connection,string name)
    {
        try{return connection.Keep(connection.Folder.GetTask(name));}catch(Exception error) when(Missing(error)){return null;}
    }
    bool IsOwned(Connection connection,dynamic task,bool legacy=false)
    {
        dynamic definition=connection.Keep(task.Definition);dynamic registration=connection.Keep(definition.RegistrationInfo);dynamic principal=connection.Keep(definition.Principal);
        if((string)registration.Source!=Source)return false;
        string owner=(string)principal.UserId;
        if(!owner.StartsWith("S-1-",StringComparison.OrdinalIgnoreCase))owner=((SecurityIdentifier)new NTAccount(owner).Translate(typeof(SecurityIdentifier))).Value;
        return owner==sid&&(string.Equals(((string)registration.Description).Replace("\r",""),ProfileDescription,StringComparison.OrdinalIgnoreCase)
            ||legacy&&string.Equals((string)registration.Description,Description,StringComparison.OrdinalIgnoreCase));
    }
    dynamic? Find(Connection connection,string name)
    {
        var task=GetTask(connection,name);
        if(task is not null&&!IsOwned(connection,task,name==LegacyTaskName))throw new InvalidOperationException("A different Windows task has this reminder's name. It has not been changed.");
        return task;
    }
    string ResolveName(Connection connection)
    {
        // Metadata retains full profile identity; the visible name contains readable labels only.
        dynamic tasks=connection.Keep(connection.Folder.GetTasks(1));
        for(int index=1;index<=(int)tasks.Count;index++)
        {
            dynamic task=connection.Keep(tasks.Item(index));string name=(string)task.Name;
            if(name.StartsWith("Backup Reminder - ",StringComparison.OrdinalIgnoreCase)&&name!=LegacyTaskName&&IsOwned(connection,task))return name;
        }
        string preferred=FriendlyName(Environment.UserName,directory,settingsPath),candidate=preferred;
        for(int number=2;GetTask(connection,candidate) is not null;number++)candidate=preferred+" ("+number+")";
        return candidate;
    }
    public ReminderTask Read()
    {
        using var connection=new Connection();string name=ResolveName(connection);dynamic? task=Find(connection,name);
        if(task is null){name=LegacyTaskName;task=Find(connection,name);}
        if(task is null)return new();
        dynamic definition=connection.Keep(task.Definition);dynamic actions=connection.Keep(definition.Actions);dynamic action=connection.Keep(actions.Item(1));
        DateTime next=task.NextRunTime;
        return new((string)task.Xml,(bool)task.Enabled,next.Year>1900?next:null,(string)action.Path,name);
    }
    internal string BuildXml(ReminderSchedule schedule,DateTime? testAt=null,bool smoke=false)
    {
        schedule.Validate();
        if(schedule.Mode=="Interval"&&schedule.IntervalDays>365)throw new InvalidDataException("Windows reminders support intervals from 1 to 365 days. Choose a shorter interval before enabling reminders.");
        if(!File.Exists(executable)||Path.GetFileName(executable).Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Run the published Backup-Reminder.exe from its permanent folder before enabling reminders.");
        using var connection=new Connection();dynamic definition=connection.Keep(connection.Service.NewTask(0));
        dynamic registration=connection.Keep(definition.RegistrationInfo);registration.Source=Source;registration.Author="DGX Development";
        registration.Description=ProfileDescription;
        dynamic principal=connection.Keep(definition.Principal);principal.UserId=sid;principal.LogonType=3;principal.RunLevel=0;
        dynamic settings=connection.Keep(definition.Settings);
        settings.Enabled=true;settings.StartWhenAvailable=true;settings.DisallowStartIfOnBatteries=false;settings.StopIfGoingOnBatteries=false;
        settings.MultipleInstances=0;settings.ExecutionTimeLimit="PT0S";settings.AllowDemandStart=true;settings.WakeToRun=false;
        dynamic triggers=connection.Keep(definition.Triggers);
        void Trigger(int type,DateTime start,Action<dynamic> configure)
        {
            dynamic trigger=connection.Keep(triggers.Create(type));trigger.Enabled=true;
            trigger.StartBoundary=start.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture);configure(trigger);
        }
        var time=TimeOnly.ParseExact(schedule.TimeOfDay,"HH:mm",CultureInfo.InvariantCulture);
        if(testAt is DateTime when)Trigger(1,when,_=>{});
        else if(schedule.Mode=="Monthly")
        {
            foreach(var part in MonthlyParts(schedule.DayOfMonth))
                Trigger(4,DateTime.Today.Add(time.ToTimeSpan()),trigger=>{trigger.MonthsOfYear=(short)part.Months;trigger.DaysOfMonth=part.Last?0:1<<(part.Day-1);trigger.RunOnLastDayOfMonth=part.Last;});
        }
        else Trigger(2,DateOnly.ParseExact(schedule.StartDate,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToDateTime(time),trigger=>trigger.DaysInterval=(short)schedule.IntervalDays);
        dynamic actions=connection.Keep(definition.Actions);dynamic action=connection.Keep(actions.Create(0));
        action.Path=Path.GetFullPath(executable);action.Arguments=Arguments+" --scheduled"+(smoke?" --smoke-test":"");action.WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable));
        return (string)definition.XmlText;
    }
    internal void RegisterAt(string xml,string name)
    {
        using var connection=new Connection();bool exists=Find(connection,name) is not null;
        connection.Keep(connection.Folder.RegisterTask(name,xml,exists?6:2,sid,null,3,null));
    }
    internal void Register(string xml)
    {
        using var connection=new Connection();string name=ResolveName(connection);bool exists=Find(connection,name) is not null;
        var legacy=Find(connection,LegacyTaskName);
        var document=System.Xml.Linq.XDocument.Parse(xml);System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        document.Descendants(ns+"Description").Single().Value=ProfileDescription;
        // Register first; remove the old name only after the replacement succeeds.
        connection.Keep(connection.Folder.RegisterTask(name,document.ToString(),exists?6:2,sid,null,3,null));
        if(legacy is not null)
        {
            try{connection.Folder.DeleteTask(LegacyTaskName,0);}
            catch{connection.Folder.DeleteTask(name,0);throw;}
        }
    }
    internal bool UpgradeLegacyLaunch()
    {
        var task=Read();if(task.Xml is null)return false;
        var document=System.Xml.Linq.XDocument.Parse(task.Xml);
        System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
        var actions=document.Descendants(ns+"Actions").Single();
        if(actions.Elements().Count()!=1)return false;
        var action=actions.Element(ns+"Exec");var arguments=action?.Element(ns+"Arguments");
        string scheduledArguments=Arguments+" --scheduled";
        if((arguments?.Value!=Arguments&&arguments?.Value!=scheduledArguments)||!string.Equals(action?.Element(ns+"Command")?.Value,Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase))return false;
        var policy=document.Descendants(ns+"MultipleInstancesPolicy").SingleOrDefault();
        if(arguments.Value==scheduledArguments&&policy?.Value=="Parallel"&&task.Name!=LegacyTaskName)return false;
        arguments.Value=scheduledArguments;
        if(policy is not null)policy.Value="Parallel";
        else document.Descendants(ns+"Settings").Single().AddFirst(new System.Xml.Linq.XElement(ns+"MultipleInstancesPolicy","Parallel"));
        Register(document.ToString());return true;
    }
    public void Apply(ReminderSchedule schedule)
    {
        if(!schedule.Enabled){Remove();return;}
        Register(BuildXml(schedule));
        if(!Read().Enabled)throw new InvalidOperationException("Windows did not enable the reminder task.");
    }
    internal void Remove()
    {
        using var connection=new Connection();string name=ResolveName(connection);
        if(Find(connection,name) is not null)connection.Folder.DeleteTask(name,0);
        if(Find(connection,LegacyTaskName) is not null)connection.Folder.DeleteTask(LegacyTaskName,0);
    }
    public void Restore(ReminderTask snapshot)
    {
        if(snapshot.Xml is null){Remove();return;}
        string name=snapshot.Name??TaskName;
        using var connection=new Connection();string current=ResolveName(connection);
        RegisterAt(snapshot.Xml,name);
        if(current!=name&&Find(connection,current) is not null)connection.Folder.DeleteTask(current,0);
        if(name!=LegacyTaskName&&Find(connection,LegacyTaskName) is not null)connection.Folder.DeleteTask(LegacyTaskName,0);
    }

}
internal static class ReminderSettings
{
    internal static void Save(Profile profile,ChecklistSettings draft,IReminderScheduler scheduler)
    {
        draft.Schedule?.Validate();profile.EnsureSettingsUnchanged();
        var before=scheduler.Read();
        try{scheduler.Apply(draft.Schedule??new());profile.SaveSettings(draft);}
        catch(Exception original)
        {
            try{scheduler.Restore(before);}catch(Exception rollback){throw new IOException("The schedule could not be saved, and the previous Windows task could not be restored. Reopen Reminder schedule to check its status. "+rollback.Message,original);}
            throw;
        }
    }
}
internal sealed class MemoryReminderScheduler:IReminderScheduler
{
    ReminderTask task=new();
    public ReminderTask Read()=>task;
    public void Apply(ReminderSchedule schedule)=>task=schedule.Enabled?new("test",true,DateTime.Today.AddDays(1)):new();
    public void Restore(ReminderTask snapshot)=>task=snapshot;
}
