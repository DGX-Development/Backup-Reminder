using W=System.Windows;
using C=System.Windows.Controls;
using System.Text.Json;
namespace BackupReminder;
internal sealed partial class ReviewWindow
{
 const string ReleasesWebsite="https://github.com/DGX-Development/Backup-Reminder/releases";
 static string ReviewVersion()=>typeof(ReviewWindow).Assembly.GetName().Version!.ToString(3);
 internal static (string Heading,string Message,bool Available) ReleaseStatus(string json,Version installed){using var data=JsonDocument.Parse(json);string? tag=data.RootElement.GetProperty("tag_name").GetString();if(tag==null||!System.Text.RegularExpressions.Regex.IsMatch(tag,@"^v?[0-9]+\.[0-9]+\.[0-9]+$")||!Version.TryParse(tag.TrimStart('v'),out var latest))throw new InvalidDataException("Unexpected release version.");var current=new Version(installed.Major,installed.Minor,installed.Build);return latest>current?("Update available",$"Backup Reminder {latest} is available.",true):current>latest?("You're running a newer version",$"Installed: {current}.\nLatest published stable: {latest}.\nNo update is needed.",false):("You're up to date",$"Latest stable version: {latest}.",false);}
 async void CheckForUpdates(C.MenuItem command){command.IsEnabled=false;try{using var client=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(10)};client.DefaultRequestHeaders.UserAgent.ParseAdd("BackupReminder/"+ReviewVersion());var json=await client.GetStringAsync("https://api.github.com/repos/DGX-Development/Backup-Reminder/releases/latest");var result=ReleaseStatus(json,typeof(ReviewWindow).Assembly.GetName().Version!);var notice=NoticeWindow(result.Heading,result.Message,result.Available?()=>Profile.Open(new ChecklistEntry{Target=ReleasesWebsite}):null,"View release");notice.Title="Check for updates";notice.ShowDialog();}catch(Exception){Notice("Check for updates","Couldn't check for updates. GitHub's release information is unavailable. Try again in a moment.");}finally{command.IsEnabled=true;}}
 void ShowSetup()=>SetupWindow().ShowDialog();
 W.Window SetupWindow(){var frame=new DgxSetupFrame();var root=frame.Root;var dialog=Dialog("Set up Backup Reminder",DgxSetupFrame.Width,DgxSetupFrame.Height,root);frame.Configure(dialog);
  var restore=Standard(Button("Restore from a backup",()=>{var picker=new Microsoft.Win32.OpenFileDialog{Title="Restore configuration",Filter="Backup Reminder configuration (*.zip)|*.zip"};if(picker.ShowDialog(dialog)!=true)return;var incoming=ProfileBackup.Read(picker.FileName);var confirm=NoticeWindow("Restore configuration",$"Restore {incoming.Settings.Items.Count} checklist items? Your current configuration will be saved in recovery\\before-restore.zip. Reminders will be turned off. Review Settings and enable them again when ready.",()=>{ProfileBackup.Restore(profile,picker.FileName,scheduler);profile=new Profile(directory);LoadPreferences();ApplyTheme(preferences.Appearance);Render();RecordSetup();dialog.DialogResult=true;},"Restore");confirm.Owner=dialog;confirm.ShowDialog();}));var welcome=DgxSetupWelcome.Create("Backup Reminder","reminder timing",restore);welcome.Children.Add(DgxSampleContent.Create(ChangeSamples));
  var commonOptions=new DgxSetupAppearance(preferences.Appearance,preferences.CloseToTray,preferences.MinimizeToTray,preferences.StartWithWindows,preferences.StartMinimized);var options=commonOptions.Panel;var appearance=commonOptions.Appearance;var close=commonOptions.CloseToTray;var minimize=commonOptions.MinimizeToTray;var startup=commonOptions.Startup;var startMin=commonOptions.StartMinimized;
  var reminder=BuildReminderPage(dialog,out var readReminder,"Finish");var finish=DgxSetupFrame.Finish("Backup Reminder","Use Edit checklist to add your backup tools and organize tasks. Complete the backups yourself, then check off your progress.");
  var pages=new W.FrameworkElement[]{welcome,options,reminder,finish};int page=0;var back=frame.Back;var next=frame.Next;
  void RenderPage(){frame.Show(page,pages);}

  back.Click+=(_,_)=>{if(page>0){page--;RenderPage();}};next.Click+=(_,_)=>Guard(()=>{if(page==2)_=readReminder();if(page<pages.Length-1){page++;RenderPage();return;}var selected=readReminder();ReminderSettings.Save(profile,selected,scheduler);SavePreferences(new WindowPreferences((string)appearance.SelectedItem,close.IsChecked==true,minimize.IsChecked==true,startup.IsChecked==true,startMin.IsChecked==true,preferences.LockWindowSizes,preferences.WindowSizes),true);ApplyTheme(preferences.Appearance);RecordSetup();Render();dialog.DialogResult=true;});RenderPage();return dialog;
 }
 string ChangeSamples(bool remove){
  var draft=profile.CopySettings();
  var samples=new[]{new ChecklistEntry{Id="dgx-sample-notes",Title="Sample: Review backup notes",Detail="List the files and settings you want to preserve. This example does not run a backup."},new ChecklistEntry{Id="dgx-sample-files",Title="Sample: Back up documents",Detail="Use your preferred backup tool, then review its result.",Group="dgx-sample-group"},new ChecklistEntry{Id="dgx-sample-restore",Title="Sample: Check a restored file",Detail="Restore a representative file to a separate location and check its contents.",Group="dgx-sample-group"}};
  var group=new ChecklistEntry{Id="dgx-sample-group",Title="Sample: File backup checks",Detail="These are manual steps. No backup jobs or reminders are enabled."};
  bool Same(ChecklistEntry a,ChecklistEntry b)=>JsonSerializer.Serialize(a,Profile.Json)==JsonSerializer.Serialize(b,Profile.Json)&&!profile.State.Checked.Contains(a.Id);
  int changed=DgxSampleContent.Merge(draft.Items,samples,x=>x.Id,Same,remove);
  if(!remove||!draft.Items.Any(x=>x.Group==group.Id))changed+=DgxSampleContent.Merge(draft.Groups,new[]{group},x=>x.Id,Same,remove);
  if(changed>0){profile.SaveSettings(draft);Render();}return DgxSampleContent.Result(changed,remove);
 }
 void RecordSetup()=>File.WriteAllText(Path.Combine(directory,"setup.json"),"{\"visited\":true}");
}
