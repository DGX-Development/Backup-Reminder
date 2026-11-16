using W=System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace BackupReminder;
internal sealed partial class ReviewWindow
{
 [StructLayout(LayoutKind.Sequential)]struct ReminderFlash{public uint Size;public IntPtr Window;public uint Flags,Count,Timeout;}
 [DllImport("user32.dll")]static extern bool FlashWindowEx(ref ReminderFlash info);
 internal int ReminderDeliveries {get;private set;}
 internal void HandleScheduledReminder(){
  ReminderDeliveries++;
  if(!Window.IsVisible||Window.WindowState==W.WindowState.Minimized)Reopen();
  bool foreground=Window.IsActive||Window.OwnedWindows.Cast<W.Window>().Any(x=>x.IsActive);
  if(!statusTestMode){
   if(profile.Settings.PlayReminderSound)new WindowsReminderAttention().PlaySound(string.IsNullOrWhiteSpace(profile.Settings.ReminderSoundPath)?null:profile.ResolveAsset(profile.Settings.ReminderSoundPath));
   if(!foreground){var flash=new ReminderFlash{Size=(uint)Marshal.SizeOf<ReminderFlash>(),Window=new WindowInteropHelper(Window).Handle,Flags=2,Count=3};FlashWindowEx(ref flash);}
  }
  RefreshReminderStatus();
 }
 internal void SaveSmokeProgress()=>profile.Save(profile.State.Checked,profile.State.LastCompleted,profile.State.CheckedAt);
}
