using Microsoft.Win32;
namespace BackupReminder;
internal static class StartupTests
{
 internal static int RunCleanup(string directory)
 {
  Directory.CreateDirectory(directory);var profile=new Profile(directory);var schedule=profile.CopySettings();schedule.Schedule??=new();schedule.Schedule.Enabled=true;var scheduler=new WindowsReminderScheduler(directory,profile.SettingsPath,Environment.ProcessPath!);using var run=Registry.CurrentUser.CreateSubKey(StartupRegistration.RunPath);string name=StartupRegistration.Name(directory);
  try{
   scheduler.Apply(schedule.Schedule);StartupRegistration.Save(run,name,StartupRegistration.Command(Environment.ProcessPath!,directory,profile.SettingsPath),()=>{});
   var original=File.ReadAllBytes(profile.SettingsPath);var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,ArgumentList={"--data-directory",directory,"--remove-integrations"}})!;
   if(!p.WaitForExit(15000)||p.ExitCode!=0)throw new Exception("Uninstall cleanup did not complete");
   if(scheduler.Read().Xml!=null||run.GetValue(name)!=null)throw new Exception("Windows registration remains");
   if(!original.SequenceEqual(File.ReadAllBytes(profile.SettingsPath)))throw new Exception("Kept configuration changed");
   string foreignExecutable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
   var foreignScheduler=new WindowsReminderScheduler(directory,profile.SettingsPath,foreignExecutable);foreignScheduler.Apply(schedule.Schedule);
   string foreignCommand=StartupRegistration.Command(foreignExecutable,directory,profile.SettingsPath);run.SetValue(name,foreignCommand);
   var other=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,ArgumentList={"--data-directory",directory,"--remove-integrations"}})!;
   if(!other.WaitForExit(15000)||other.ExitCode!=0)throw new Exception("Ownership cleanup did not complete");
   if(!string.Equals(scheduler.Read().Executable,foreignExecutable,StringComparison.OrdinalIgnoreCase)||!Equals(run.GetValue(name),foreignCommand))throw new Exception("Another executable's registrations were removed");
   File.WriteAllLines(Path.Combine(directory,"cleanup-verification.txt"),new[]{"PASS: Uninstall command removes real scheduled task","PASS: Uninstall command removes real sign-in entry","PASS: Keep settings preserves configuration bytes","PASS: Cleanup preserves registrations belonging to another executable"});return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(directory,"cleanup-verification.txt"),e.ToString());return 1;}finally{scheduler.Remove();run.DeleteValue(name,false);}
 }
 internal static int Run(string directory)
 {
  Directory.CreateDirectory(directory);var results=new List<string>();var name="DGX.BackupReminder.Test."+Guid.NewGuid().ToString("N");
  using var run=Registry.CurrentUser.CreateSubKey(StartupRegistration.RunPath);
  void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
  try{
   string command=StartupRegistration.Command(Environment.ProcessPath!,directory,Path.Combine(directory,"settings.json"));
   Check(command.Contains("\" --data-directory \"")&&command.Contains("\" --settings \""),"Startup command quotes executable, profile and settings");
   Check(StartupRegistration.Name(directory)==StartupRegistration.Name(directory.ToUpperInvariant())&&StartupRegistration.Name(directory)!=StartupRegistration.Name(directory+"-other"),"Startup ownership is stable and per profile");
   bool saved=false;StartupRegistration.Save(run,name,command,()=>saved=true);Check(saved&&Equals(run.GetValue(name),command),"Windows startup entry created with exact launch command");
   try{StartupRegistration.Save(run,name,"replacement",()=>throw new IOException("test"));}catch(IOException){}Check(Equals(run.GetValue(name),command),"Failed preference save restores previous startup command");
   StartupRegistration.Save(run,name,null,()=>{});Check(run.GetValue(name)==null,"Disable removes only owned startup entry");
   try{StartupRegistration.Save(run,name,command,()=>throw new IOException("test"));}catch(IOException){}Check(run.GetValue(name)==null,"Failed initial save removes temporary startup entry");
   File.WriteAllLines(Path.Combine(directory,"startup-verification.txt"),results);return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(directory,"startup-verification.txt"),e.ToString());return 1;}finally{run.DeleteValue(name,false);}
 }
}
