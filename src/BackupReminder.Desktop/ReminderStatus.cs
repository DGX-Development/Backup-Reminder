using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
namespace BackupReminder;
internal sealed partial class ReviewWindow
{
 C.Button? reminderButton;
 C.Image? reminderGlyph;
 bool statusTestMode;
 void AddReminderButton(C.DockPanel parent,bool testMode){
  statusTestMode=testMode;
  reminderButton=Button("",()=>{var dialog=SettingsWindow(2);dialog.ShowDialog();RefreshReminderStatus();});
  reminderButton.Width=reminderButton.Height=36;reminderButton.Padding=new W.Thickness(0);reminderButton.Margin=new W.Thickness(0,0,8,0);reminderButton.VerticalAlignment=W.VerticalAlignment.Top;
  reminderGlyph=Glyph("alert_off",24);reminderButton.Content=reminderGlyph;
  C.DockPanel.SetDock(reminderButton,C.Dock.Right);parent.Children.Add(reminderButton);
  SetReminderStatus(null,"Checking reminder status. Click to open Reminder schedule.");
  Window.Loaded+=(_,_)=>RefreshReminderStatus();reminderButton.ToolTipOpening+=(_,_)=>RefreshReminderStatus();
 }
 async void RefreshReminderStatus(){
  try{var task=statusTestMode?scheduler.Read():await Task.Run(()=>scheduler.Read());
   bool active=task.Enabled&&task.Xml!=null;
   string text=active?(task.NextRun is DateTime next?"Reminders are on. Next: "+next.ToString("MM/dd/yyyy h:mm tt")+".":"Reminders are on. Windows has not supplied a next run time."):"Reminders are off.";
   if(!active&&profile.Settings.Schedule?.Enabled==true)text+=" Timing is saved, but no active Windows reminder is registered.";
   SetReminderStatus(active,text+" Click to open Reminder schedule.");
  }catch{SetReminderStatus(null,"Windows reminder status is unavailable. Click to open Reminder schedule.");}
 }
 void SetReminderStatus(bool? enabled,string text){if(reminderButton==null)return;reminderGlyph=Glyph(enabled==true?"alert_on":"alert_off",24);reminderButton.Content=reminderGlyph;Resource(reminderGlyph!,System.Windows.Documents.TextElement.ForegroundProperty,enabled==true?"DashboardAccent":"ThemeF2F2F2");reminderButton.ToolTip=text;System.Windows.Automation.AutomationProperties.SetName(reminderButton,"Reminder schedule");System.Windows.Automation.AutomationProperties.SetItemStatus(reminderButton,enabled==true?"On":enabled==false?"Off":"Unknown");}
}
