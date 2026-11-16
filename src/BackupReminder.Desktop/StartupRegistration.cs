using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
namespace BackupReminder;
internal static class StartupRegistration
{
 internal const string RunPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
 internal static string Name(string directory)=>"DGX.BackupReminder."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())))[..16];
 internal static string Command(string executable,string directory,string settings)=>Quote(executable)+" --data-directory "+Quote(directory)+" --settings "+Quote(settings);
 static string Quote(string value)=>WindowsReminderScheduler.Quote(value);
 internal static void Save(RegistryKey key,string name,string? command,Action persist)
 {
  var previous=key.GetValue(name);var kind=previous==null?RegistryValueKind.String:key.GetValueKind(name);
  try{if(command==null)key.DeleteValue(name,false);else key.SetValue(name,command,RegistryValueKind.String);persist();}
  catch{if(previous==null)key.DeleteValue(name,false);else key.SetValue(name,previous,kind);throw;}
 }
}
