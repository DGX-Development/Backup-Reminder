using W=System.Windows;
namespace BackupReminder;
internal sealed partial class ReviewWindow
{
 readonly Dictionary<W.Window,W.ResizeMode> resizableDialogs=new();
 void ApplyWindowSizeLock()
 {
  if(preferences.LockWindowSizes&&Window.WindowState==W.WindowState.Maximized)Window.WindowState=W.WindowState.Normal;
  Window.ResizeMode=preferences.LockWindowSizes?W.ResizeMode.CanMinimize:W.ResizeMode.CanResize;
  foreach(var pair in resizableDialogs)
   pair.Key.ResizeMode=preferences.LockWindowSizes?W.ResizeMode.NoResize:pair.Value;
 }
 void ResetWindowSizes()
 {
  SavePreferences(preferences with{LockWindowSizes=false,WindowSizes=null});
  Window.WindowState=W.WindowState.Normal;Window.Width=Math.Min(640,W.SystemParameters.WorkArea.Width);Window.Height=Math.Min(860,W.SystemParameters.WorkArea.Height);
  Window.Left=W.SystemParameters.WorkArea.Left+Math.Max(0,(W.SystemParameters.WorkArea.Width-Window.Width)/2);Window.Top=W.SystemParameters.WorkArea.Top+Math.Max(0,(W.SystemParameters.WorkArea.Height-Window.Height)/2);SaveBounds();
 }
 void RememberDialogSize(W.Window dialog)
 {
  dialog.Loaded+=(_,_)=>
  {
   if(dialog.SizeToContent!=W.SizeToContent.Manual||dialog.ResizeMode==W.ResizeMode.NoResize||dialog.Title=="Backup Reminder settings"||dialog.Title.Contains("setup",StringComparison.OrdinalIgnoreCase)||dialog.Title.StartsWith("Set up ",StringComparison.OrdinalIgnoreCase))return;
   resizableDialogs[dialog]=dialog.ResizeMode;
   if(preferences.WindowSizes?.TryGetValue(dialog.Title,out var size)==true&&size.Length==2&&size.All(double.IsFinite))
   {
    dialog.Width=Math.Clamp(size[0],dialog.MinWidth,Math.Max(dialog.MinWidth,W.SystemParameters.WorkArea.Width));
    dialog.Height=Math.Clamp(size[1],dialog.MinHeight,Math.Max(dialog.MinHeight,W.SystemParameters.WorkArea.Height));
   }
   if(preferences.LockWindowSizes)dialog.ResizeMode=W.ResizeMode.NoResize;
  };
  dialog.Closed+=(_,_)=>
  {
   if(!resizableDialogs.Remove(dialog)||preferences.LockWindowSizes)return;
   var sizes=preferences.WindowSizes==null?new Dictionary<string,double[]>():new(preferences.WindowSizes);
   var bounds=dialog.WindowState==W.WindowState.Normal?new[]{dialog.Width,dialog.Height}:new[]{dialog.RestoreBounds.Width,dialog.RestoreBounds.Height};
   if(bounds.All(double.IsFinite)){sizes[dialog.Title]=bounds;SavePreferences(preferences with{WindowSizes=sizes});}
  };
 }
}
