using System.Diagnostics;
using System.Xml.Linq;

namespace BackupReminder;
internal static class SchedulerVerification
{
    internal static int Run(string root)
    {
        Directory.CreateDirectory(root);var results=new List<string>();
        var profile=new Profile(Path.Combine(root,"scheduled profile "+Guid.NewGuid().ToString("N")));
        var scheduler=new WindowsReminderScheduler(profile.DirectoryPath,profile.SettingsPath,Environment.ProcessPath!);
        try
        {
            void Check(bool value,string text){if(!value)throw new InvalidDataException(text);results.Add("PASS: "+text);}
            Check(scheduler.Read().Xml is null,"Isolated test starts without a registered task");
            var schedule=new ReminderSchedule {Enabled=true,DayOfMonth=DateTime.Today.Day,TimeOfDay=DateTime.Now.AddMinutes(5).ToString("HH:mm")};
            scheduler.Apply(schedule);var monthly=scheduler.Read();
            Check(monthly.Enabled&&monthly.NextRun is not null,"Monthly reminder is registered with an actual next run");
            Check(monthly.Name==scheduler.TaskName&&scheduler.TaskName.StartsWith("Backup Reminder - "+Environment.UserName+" - ")&&scheduler.TaskName!=scheduler.LegacyTaskName,"Windows task uses readable account and profile labels");
            var xml=XDocument.Parse(monthly.Xml!);XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";
            Check(xml.Descendants(ns+"LogonType").Single().Value=="InteractiveToken"&&(xml.Descendants(ns+"RunLevel").SingleOrDefault()?.Value??"LeastPrivilege")=="LeastPrivilege","Task uses the current interactive user without elevation or a saved password");
            Check(xml.Descendants(ns+"MultipleInstancesPolicy").Single().Value=="Parallel","Windows can notify an existing window even if a prior scheduled session is still open");
            Check(xml.Descendants(ns+"Arguments").Single().Value==scheduler.Arguments+" --scheduled"&&xml.Descendants(ns+"Command").Single().Value==Environment.ProcessPath,"Action launches the exact executable and isolated profile, including spaces");
            var legacyXml=XDocument.Parse(monthly.Xml!);legacyXml.Descendants(ns+"Arguments").Single().Value=scheduler.Arguments;legacyXml.Descendants(ns+"MultipleInstancesPolicy").Single().Value="IgnoreNew";
            legacyXml.Descendants(ns+"Description").Single().Value="Opens the manual backup checklist. Profile: "+profile.DirectoryPath;
            scheduler.Remove();scheduler.RegisterAt(legacyXml.ToString(),scheduler.LegacyTaskName);var nextBefore=scheduler.Read().NextRun;
            Check(scheduler.Read().Name==scheduler.LegacyTaskName,"Legacy hashed task is recognized before migration");
            Check(scheduler.UpgradeLegacyLaunch()&&scheduler.Read().NextRun==nextBefore&&XDocument.Parse(scheduler.Read().Xml!).Descendants(ns+"MultipleInstancesPolicy").Single().Value=="Parallel"&&XDocument.Parse(scheduler.Read().Xml!).Descendants(ns+"Arguments").Single().Value==scheduler.Arguments+" --scheduled","Legacy task gains scheduled attention without changing its next run");
            Check(scheduler.Read().Name==scheduler.TaskName&&scheduler.Read().Name!=scheduler.LegacyTaskName,"Legacy hashed task migrates to a readable name");
            Check(!scheduler.UpgradeLegacyLaunch(),"Existing scheduled launch arguments are not rewritten repeatedly");
            scheduler.Remove();scheduler.RegisterAt(monthly.Xml!,scheduler.LegacyTaskName);
            bool rolledBack=false;var renameDraft=profile.CopySettings();renameDraft.Schedule=schedule;
            using(var locked=new FileStream(profile.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {try{ReminderSettings.Save(profile,renameDraft,scheduler);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){rolledBack=true;}}
            Check(rolledBack&&scheduler.Read().Name==scheduler.LegacyTaskName,"Failed settings save restores the original hashed task name");
            scheduler.UpgradeLegacyLaunch();
            var disabled=XDocument.Parse(scheduler.Read().Xml!);disabled.Descendants(ns+"Settings").Single().SetElementValue(ns+"Enabled","false");
            scheduler.Remove();scheduler.RegisterAt(disabled.ToString(),scheduler.LegacyTaskName);
            Check(scheduler.UpgradeLegacyLaunch()&&!scheduler.Read().Enabled&&scheduler.Read().Name==scheduler.TaskName,"Readable-name migration preserves a disabled task");
            scheduler.Restore(monthly);
            var sameProfile=new WindowsReminderScheduler(profile.DirectoryPath.ToUpperInvariant()+Path.DirectorySeparatorChar,profile.SettingsPath.ToUpperInvariant(),Environment.ProcessPath!);
            Check(sameProfile.TaskName==scheduler.TaskName&&sameProfile.Read().Enabled,"Profile path casing and a trailing slash keep the same owned task");
            var otherDirectory=Path.Combine(root,"other",Path.GetFileName(profile.DirectoryPath));
            var other=new WindowsReminderScheduler(otherDirectory,Path.Combine(otherDirectory,"settings.json"),Environment.ProcessPath!);
            try
            {
                other.Apply(schedule);
                Check(other.TaskName!=scheduler.TaskName&&other.TaskName.EndsWith(" (2)")&&scheduler.Read().Enabled,"Same-label profiles receive separate readable names without replacing each other");
                other.Remove();Check(scheduler.Read().Enabled,"Removing a second profile preserves the first profile task");
            }
            finally{other.Remove();}
            var invalidAction=new WindowsReminderScheduler(profile.DirectoryPath,profile.SettingsPath,Path.Combine(root,"missing.exe"));
            var originalSettings=File.ReadAllBytes(profile.SettingsPath);var invalidDraft=profile.CopySettings();invalidDraft.Schedule=schedule;bool failed=false;
            try{ReminderSettings.Save(profile,invalidDraft,invalidAction);}catch(InvalidOperationException){failed=true;}
            Check(failed&&originalSettings.SequenceEqual(File.ReadAllBytes(profile.SettingsPath))&&scheduler.Read().Enabled,"A registration failure preserves settings and restores the actual Windows task");
            foreach(int day in new[]{29,30,31})
            {
                schedule.DayOfMonth=day;scheduler.Apply(schedule);
                Check(scheduler.Read().Enabled&&XDocument.Parse(scheduler.Read().Xml!).Descendants(ns+"CalendarTrigger").Count()==2,"Windows accepts month-end fallback triggers for day "+day);
            }
            schedule.Mode="Interval";schedule.IntervalDays=30; schedule.StartDate=DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");scheduler.Apply(schedule);
            Check(XDocument.Parse(scheduler.Read().Xml!).Descendants(ns+"DaysInterval").Single().Value=="30","Updating the same task installs the daily interval trigger");
            schedule.Enabled=false;scheduler.Apply(schedule);Check(scheduler.Read().Xml is null,"Disabling reminders removes the test task");
            scheduler.Restore(monthly);Check(scheduler.Read().Enabled,"Captured task can be restored after a failed save");
            var watch=Stopwatch.StartNew();
            scheduler.Register(scheduler.BuildXml(new ReminderSchedule(),DateTime.Now.AddSeconds(12),smoke:true));
            while(watch.Elapsed<TimeSpan.FromSeconds(50)&&!File.Exists(profile.StatePath))Thread.Sleep(250);
            Check(File.Exists(profile.StatePath),"A real clock trigger opened the app and the smoke-test window closed cleanly");
            Check(File.ReadAllText(profile.SettingsPath)==Profile.Resource("starter.json"),"Scheduled launch preserved the existing checklist configuration");
            scheduler.Remove();Check(scheduler.Read().Xml is null,"Scheduled test task was removed after verification");
            File.WriteAllLines(Path.Combine(root,"scheduler-verification.txt"),results);return 0;
        }
        catch(Exception error){results.Add("FAIL: "+error);File.WriteAllLines(Path.Combine(root,"scheduler-verification.txt"),results);return 1;}
        finally{scheduler.Remove();}
    }
}
