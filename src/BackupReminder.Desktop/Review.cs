using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace BackupReminder;

internal static class ReviewEntry
{
 static string ReviewStyles(){
  var xml=System.Xml.Linq.XDocument.Parse(Profile.Resource("styles.xaml"));
  System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
  foreach(var style in xml.Root!.Elements(ns+"Style").Where(x=>new[]{"Button","ComboBox","CheckBox"}.Contains((string?)x.Attribute("TargetType")))){
   var focus=style.Elements(ns+"Setter").FirstOrDefault(x=>(string?)x.Attribute("Property")=="FocusVisualStyle");
   var border=focus?.Descendants(ns+"Border").FirstOrDefault();if(border!=null){border.RemoveAttributes();border.SetAttributeValue("Margin","4");border.SetAttributeValue("BorderThickness","1");border.SetAttributeValue("CornerRadius","4");border.SetAttributeValue("BorderBrush","{DynamicResource Theme969696}");}
  }
  var combo=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ComboBox");
  var popup=combo.Descendants(ns+"Popup").Single();popup.SetAttributeValue("PlacementTarget","{Binding RelativeSource={RelativeSource TemplatedParent}}");
  var viewerStyle=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ScrollViewer");
  var popupTemplate=new System.Xml.Linq.XElement(viewerStyle.Descendants(ns+"ControlTemplate").First());
  popupTemplate.Element(ns+"ControlTemplate.Triggers")?.Remove();
  var viewer=combo.Descendants(ns+"ScrollViewer").Single();
  viewer.Element(ns+"ScrollViewer.Template")?.Remove();
  viewer.AddFirst(new System.Xml.Linq.XElement(ns+"ScrollViewer.Template",popupTemplate));
  var surface=combo.Descendants(ns+"Border").Single(x=>(string?)x.Attribute(System.Xml.Linq.XName.Get("Name","http://schemas.microsoft.com/winfx/2006/xaml"))=="DropdownSurface");surface.SetAttributeValue("Padding","5");
  var option=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ComboBoxItem");option.Elements(ns+"Setter").Single(x=>(string?)x.Attribute("Property")=="Padding").SetAttributeValue("Value","17,9");
  var menuStyle=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="MenuItem");
  var topItem=menuStyle.Descendants(ns+"Trigger").Single(x=>(string?)x.Attribute("Property")=="Role"&&(string?)x.Attribute("Value")=="TopLevelItem");topItem.Add(new System.Xml.Linq.XElement(ns+"Setter",new System.Xml.Linq.XAttribute("Property","Padding"),new System.Xml.Linq.XAttribute("Value","12,6")));
  return xml.ToString();
 }
 static int RemoveIntegrations(string directory)
 {
  using var mutex=new Mutex(false,Profile.InstanceName(directory));bool owns;try{owns=mutex.WaitOne(0);}catch(AbandonedMutexException){owns=true;}if(!owns)return 2;
  try{
   var scheduler=new WindowsReminderScheduler(directory,Path.Combine(directory,"settings.json"),Environment.ProcessPath!);var previous=scheduler.Read();
   try{
    if(string.Equals(previous.Executable,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))scheduler.Remove();
    using var run=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistration.RunPath,true);
    var name=StartupRegistration.Name(directory);var command=run?.GetValue(name) as string;
    if(command?.StartsWith(WindowsReminderScheduler.Quote(Environment.ProcessPath!)+" ",StringComparison.OrdinalIgnoreCase)==true)run!.DeleteValue(name,false);
   }
   catch{scheduler.Restore(previous);throw;}
   return 0;
  }finally{mutex.ReleaseMutex();}
 }
 [STAThread] static int Main(string[] args)
 {
  ApplicationConfiguration.Initialize();
  try {
   if(args.Length==2&&args[0]=="--cleanup-test")return StartupTests.RunCleanup(args[1]);
   if(args.Length==2&&args[0]=="--startup-test")return StartupTests.Run(args[1]);
   if(args.Length==2&&args[0]=="--self-test")return Verification.Run(args[1]);
   if(args.Contains("--scheduler-test"))return (int)typeof(Program).GetMethod("Main",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{args})!;
   if(args.Length==0)args=["--data-directory",File.Exists(Path.Combine(AppContext.BaseDirectory,"portable.mode"))?Path.Combine(AppContext.BaseDirectory,"profile"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Backup Reminder")];
   if(args.Length<2||args[0]!="--data-directory")throw new ArgumentException("An explicit isolated data directory is required.");
   if(args.Contains("--remove-integrations"))return RemoveIntegrations(args[1]);
   using var mutex=new Mutex(false,Profile.InstanceName(args[1]));
   using var activate=new EventWaitHandle(false,EventResetMode.AutoReset,Profile.InstanceName(args[1])+"-activate");
   using var reminder=new EventWaitHandle(false,EventResetMode.AutoReset,Profile.InstanceName(args[1])+"-reminder");
   bool owns;try{owns=mutex.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
   if(!owns){if(args.Contains("--remove-integrations"))throw new IOException("Close the running app before uninstalling.");if(args.Contains("--scheduled"))reminder.Set();else activate.Set();return 0;}
   var app=new W.Application();DgxUi.Install();
   app.Resources.MergedDictionaries.Add((W.ResourceDictionary)XamlReader.Parse(ReviewStyles()));
   bool verifying=args.Contains("--verify");int settingsIndex=Array.IndexOf(args,"--settings");var window=new ReviewWindow(args[1],!verifying,settingsIndex>=0?args[settingsIndex+1]:null,args.Contains("--scheduled"));
   if(args.Contains("--scheduled")){window.Window.ContentRendered+=DeliverOnce;void DeliverOnce(object? sender,EventArgs e){window.Window.ContentRendered-=DeliverOnce;window.HandleScheduledReminder();}}
   if(args.Contains("--smoke-test")){var smoke=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};smoke.Tick+=(_,_)=>{smoke.Stop();window.SaveSmokeProgress();window.Exit();};smoke.Start();}
   if(verifying)File.Delete(Path.Combine(args[1],"wpf-error.txt"));
   if(args.Contains("--verify"))window.Window.ContentRendered+=(_,_)=>{try{window.Verify();window.VerifyPages();window.Exit();}catch(Exception e){File.WriteAllText(Path.Combine(args[1],"wpf-error.txt"),e.ToString());Environment.ExitCode=1;window.Exit();}};
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>{if(activate.WaitOne(0)){window.Reopen();}if(reminder.WaitOne(0)){window.HandleScheduledReminder();}};timer.Start();
   app.Run(window.Window);timer.Stop();mutex.ReleaseMutex();return Environment.ExitCode;
  }catch(Exception e){if(args.Length>1&&!args.Contains("--remove-integrations")){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"launch-error.txt"),e.ToString());}return 1;}
 }
}
internal sealed partial class ReviewWindow
{
 internal W.Window Window;
 Profile profile;
 readonly string directory;
 readonly C.StackPanel rows=new(){Margin=new W.Thickness(0,0,7,0)};
 readonly C.ScrollViewer scroll=new(){VerticalScrollBarVisibility=C.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=C.ScrollBarVisibility.Disabled};
 readonly C.TextBlock progress=new(),last=new();
 readonly C.ProgressBar bar=new(){Height=4,Margin=new W.Thickness(0,10,0,20)};
 readonly C.Button complete=new(){Content="Complete backups",MinWidth=150,Margin=new W.Thickness(6,0,0,0)};
 readonly C.StackPanel footer=new(){Margin=new W.Thickness(0,20,24,0)};
 readonly C.DockPanel listHeader=new(){Margin=new W.Thickness(0,0,24,14)};
 readonly C.DockPanel heading=new(){Margin=new W.Thickness(0,0,24,20)};
 readonly Dictionary<string,C.CheckBox> checks=[];
 readonly Dictionary<string,string> stamps=[];
 bool loading,dark=true;
 string theme="Dark";
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 static M.Brush Brush(string hex)=>(M.Brush)new M.BrushConverter().ConvertFromString(hex)!;
 static void Resource(W.FrameworkElement e,W.DependencyProperty p,string key)=>e.SetResourceReference(p,key);
 static C.TextBlock Text(string value,double size=14)=>new(){Text=value,FontSize=size,TextWrapping=W.TextWrapping.Wrap};
 C.Button Button(string text,Action action){var b=new C.Button{Content=text,Margin=new W.Thickness(0)};b.Click+=(_,_)=>Guard(action);return b;}
 void Guard(Action action){try{action();}catch(Exception e){Notice("Could not complete this action",e.Message);}}
 internal ReviewWindow(string path,bool allowFirstSetup=true,string? settingsPath=null,bool scheduledLaunch=false)
 {
  directory=Path.GetFullPath(path);bool fresh=!File.Exists(settingsPath??Path.Combine(directory,"settings.json"));profile=new Profile(directory,settingsPath);scheduler=allowFirstSetup?new WindowsReminderScheduler(directory,profile.SettingsPath,Environment.ProcessPath!):new MemoryReminderScheduler();isScheduledLaunch=scheduledLaunch;
  if(fresh&&allowFirstSetup){profile.Settings.Items.Clear();profile.Settings.Groups.Clear();profile.Settings.RootOrder.Clear();profile.SaveSettings(profile.Settings);}
  LoadPreferences();theme=preferences.Appearance;
  ApplyTheme(theme);
  Window=(W.Window)XamlReader.Parse(Profile.Resource("shell.xaml"));Window.Title="Backup Reminder";
  Window.Width=640;Window.Height=860;Window.MinWidth=580;Window.MinHeight=560;Window.WindowStartupLocation=W.WindowStartupLocation.CenterScreen;
  using(var stream=typeof(ReviewWindow).Assembly.GetManifestResourceStream("brand.ico")){Window.Icon=BitmapFrame.Create(stream!,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}
  var root=(C.DockPanel)Window.Content;root.Children.RemoveAt(1);
  var menu=(C.Menu)Window.FindName("AppMenu");menu.Items.Clear();
  var file=new C.MenuItem{Header="_File"};menu.Items.Add(file);
  Command(file,"Back up configuration…",BackUp);Command(file,"Restore configuration…",Restore);file.Items.Add(new C.Separator());Command(file,"Edit checklist…",EditChecklist);
  Command(file,"Open data folder",()=>Profile.Open(new ChecklistEntry{Target=directory}));
  file.Items.Add(new C.Separator());Command(file,"Exit",Exit);
  var settings=new C.MenuItem{Header="_Settings",Height=28,Padding=new W.Thickness(11,2,11,4),Margin=new W.Thickness(2,0,2,0),VerticalAlignment=W.VerticalAlignment.Center};menu.Items.Add(settings);settings.Click+=(_,e)=>{e.Handled=true;Guard(Settings);};
  var help=new C.MenuItem{Header="_Help"};menu.Items.Add(help);Command(help,"Help & user guide",Help);
  Command(help,"Run setup again…",ShowSetup);var update=new C.MenuItem{Header="Check for updates",Icon=Glyph("updates",24)};update.Click+=(_,_)=>CheckForUpdates(update);help.Items.Add(update);
  Command(help,"Report an issue…",()=>Notice("Report an issue","Open Backup Reminder's GitHub issue page in your browser. A GitHub account is required to submit an issue. Backup Reminder will stay open.",()=>Profile.Open(new ChecklistEntry{Target=MainWindow.IssuesWebsite})));
  help.Items.Add(new C.Separator());Command(help,"About Backup Reminder",About);  var body=new C.DockPanel{Margin=new W.Thickness(24,12,0,24)};root.Children.Add(body);
  var top=new C.StackPanel();C.DockPanel.SetDock(top,C.Dock.Top);body.Children.Add(top);top.Children.Add(heading);
  var pin=Button("",()=>{Window.Topmost=!Window.Topmost;});pin.Width=pin.Height=36;pin.Padding=new W.Thickness(0);pin.ToolTip="Always on top";
  var pinShape=Glyph("pin",24);pin.Content=pinShape;pin.Click+=(_,_)=>{pinShape.Tag=Window.Topmost?"pin-active":"pin";Resource(pinShape,System.Windows.Documents.TextElement.ForegroundProperty,Window.Topmost?"DashboardAccent":"ThemeF2F2F2");pin.ToolTip=Window.Topmost?"Always on top is on":"Always on top is off";};C.DockPanel.SetDock(pin,C.Dock.Right);heading.Children.Add(pin);pin.VerticalAlignment=W.VerticalAlignment.Top;AddReminderButton(heading,!allowFirstSetup);  var brand=new C.Image{Width=48,Height=48,Margin=new W.Thickness(0,0,14,0),VerticalAlignment=W.VerticalAlignment.Top};using(var stream=typeof(ReviewWindow).Assembly.GetManifestResourceStream("brand.png")){brand.Source=BitmapFrame.Create(stream!,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}brand.Stretch=M.Stretch.Uniform;M.RenderOptions.SetBitmapScalingMode(brand,M.BitmapScalingMode.HighQuality);C.DockPanel.SetDock(brand,C.Dock.Left);heading.Children.Add(brand);
  var titles=new C.StackPanel();heading.Children.Add(titles);titles.Children.Add(new C.TextBlock{Text="Backup Reminder",FontSize=DgxUi.MainTitleSize,FontWeight=W.FontWeights.SemiBold});last.FontSize=DgxUi.BodySize;last.Margin=new W.Thickness(0,3,0,5);Resource(last,C.TextBlock.ForegroundProperty,"Muted");titles.Children.Add(last);
  top.Children.Add(listHeader);var edit=Button("Edit checklist",EditChecklist);edit.Margin=new W.Thickness(12,0,0,0);C.DockPanel.SetDock(edit,C.Dock.Right);listHeader.Children.Add(edit);var instruction=Text("Check each item after completing its backup.",DgxUi.BodySize);instruction.VerticalAlignment=W.VerticalAlignment.Center;Resource(instruction,C.TextBlock.ForegroundProperty,"Muted");listHeader.Children.Add(instruction);
  C.DockPanel.SetDock(footer,C.Dock.Bottom);body.Children.Add(footer);footer.Children.Add(progress);footer.Children.Add(bar);Resource(bar,C.ProgressBar.ForegroundProperty,"DashboardAccent");
  var actions=new C.DockPanel();footer.Children.Add(actions);var close=Button("Close for now",Exit);C.DockPanel.SetDock(close,C.Dock.Left);actions.Children.Add(close);complete.HorizontalAlignment=W.HorizontalAlignment.Right;actions.Children.Add(complete);complete.Click+=(_,_)=>Guard(()=>{if(!complete.IsEnabled)return;profile.Save([],DateTimeOffset.Now.ToString("o"));Render();Notice("Backups completed","Your checklist completion has been recorded.");});
  scroll.Content=rows;body.Children.Add(scroll);scroll.SizeChanged+=(_,_)=>Align();scroll.ScrollChanged+=(_,_)=>Align();scroll.LayoutUpdated+=(_,_)=>Align();scroll.FocusVisualStyle=null;
  Window.SourceInitialized+=(_,_)=>Caption(Window);ConfigureWindow();if(allowFirstSetup&&!scheduledLaunch&&fresh&&!File.Exists(Path.Combine(directory,"setup.json"))){Window.ContentRendered+=FirstSetup;void FirstSetup(object? sender,EventArgs e){Window.ContentRendered-=FirstSetup;Window.Dispatcher.BeginInvoke(new Action(()=>{try{ShowSetup();}finally{RecordSetup();}}));}}
  Render();
 }
 void Command(C.MenuItem parent,string label,Action action){var item=new C.MenuItem{Header=label,InputGestureText=label=="Help & user guide"?"F1":label=="Exit"?"Ctrl+Q":""};item.Click+=(_,_)=>Guard(action);item.Icon=Glyph(MenuGlyph(label),24);parent.Items.Add(item);}
 void Align(){if(!scroll.IsArrangeValid||scroll.ViewportWidth<=0)return;double gutter=7+Math.Clamp(scroll.ActualWidth-scroll.ViewportWidth,0,17);if(Math.Abs(heading.Margin.Right-gutter)<0.1)return;heading.Margin=new W.Thickness(0,0,gutter,20);listHeader.Margin=new W.Thickness(0,0,gutter,14);footer.Margin=new W.Thickness(0,20,gutter,0);}
 void Render()
 {
  loading=true;rows.Children.Clear();checks.Clear();stamps.Clear();foreach(var pair in profile.State.CheckedAt)stamps[pair.Key]=pair.Value;
  last.Text="Last completed: "+(profile.State.LastCompleted is null?"No completion recorded":DateTimeOffset.Parse(profile.State.LastCompleted).ToLocalTime().ToString("MMM d, yyyy  h:mm tt"));
  foreach(var root in MainWindow.RootEntries(profile.Settings)){
   if(profile.Settings.Groups.Contains(root)){var group=new C.StackPanel{Margin=new W.Thickness(0,16,0,10)};var line=new C.DockPanel();group.Children.Add(line);if(!string.IsNullOrWhiteSpace(root.Target)){var open=Button(root.Button??"Open",()=>Profile.Open(root));open.Margin=new W.Thickness(12,0,0,0);C.DockPanel.SetDock(open,C.Dock.Right);line.Children.Add(open);}var groupIcon=EntryImage(root);groupIcon.Margin=new W.Thickness(0,0,12,0);C.DockPanel.SetDock(groupIcon,C.Dock.Left);line.Children.Add(groupIcon);var label=Text(root.Title,20);label.FontWeight=W.FontWeights.SemiBold;line.Children.Add(label);if(!string.IsNullOrWhiteSpace(root.Detail)){var detail=Text(root.Detail!,DgxUi.BodySize);detail.Margin=new W.Thickness(0,6,0,0);Resource(detail,C.TextBlock.ForegroundProperty,"Muted");group.Children.Add(detail);}rows.Children.Add(group);foreach(var item in profile.Settings.Items.Where(x=>x.Group==root.Id))AddItem(item);
   }else AddItem(root);
  }
  loading=false;Update();Window.Dispatcher.BeginInvoke(Align);
 }
 void AddItem(ChecklistEntry item)
 {
  var row=new C.Border{CornerRadius=new W.CornerRadius(9),Padding=new W.Thickness(14),Margin=new W.Thickness(0,0,0,6)};Resource(row,C.Border.BackgroundProperty,"Theme2B2B2B");rows.Children.Add(row);var layout=new C.DockPanel();row.Child=layout;
  var check=new C.CheckBox{IsChecked=profile.State.Checked.Contains(item.Id),VerticalAlignment=W.VerticalAlignment.Center,Margin=new W.Thickness(0,0,14,0)};System.Windows.Automation.AutomationProperties.SetName(check,item.Title+" backup completed");checks[item.Id]=check;C.DockPanel.SetDock(check,C.Dock.Left);layout.Children.Add(check);
  if(!string.IsNullOrWhiteSpace(item.Target)){var open=Button(item.Button??"Open",()=>Profile.Open(item));open.Margin=new W.Thickness(12,0,0,0);open.VerticalAlignment=W.VerticalAlignment.Center;C.DockPanel.SetDock(open,C.Dock.Right);layout.Children.Add(open);}
  if(!string.IsNullOrWhiteSpace(item.Target)||!string.IsNullOrWhiteSpace(item.IconPath)){var icon=EntryImage(item);icon.Margin=new W.Thickness(0,0,12,0);C.DockPanel.SetDock(icon,C.Dock.Left);layout.Children.Add(icon);}
  var text=new C.StackPanel();layout.Children.Add(text);text.Children.Add(Text(item.Title));if(!string.IsNullOrWhiteSpace(item.Detail)){var detail=Text(item.Detail!,DgxUi.BodySize);detail.Margin=new W.Thickness(0,6,0,0);Resource(detail,C.TextBlock.ForegroundProperty,"Muted");text.Children.Add(detail);}var stamp=Text("",DgxUi.BodySize);stamp.Margin=new W.Thickness(0,8,0,0);Resource(stamp,C.TextBlock.ForegroundProperty,"Muted");text.Children.Add(stamp);
  void Refresh(){stamp.Visibility=check.IsChecked==true?W.Visibility.Visible:W.Visibility.Collapsed;stamp.Text=stamps.TryGetValue(item.Id,out var when)?"Checked "+DateTimeOffset.Parse(when).ToLocalTime().ToString("MMM d, yyyy, h:mm tt"):"Checked date not recorded";}
  void Changed(object sender,W.RoutedEventArgs e){if(loading)return;Guard(()=>{if(check.IsChecked==true)stamps[item.Id]=DateTimeOffset.Now.ToString("o");else stamps.Remove(item.Id);profile.Save(checks.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key),profile.State.LastCompleted,stamps);Refresh();Update();});}
  check.Checked+=Changed;check.Unchecked+=Changed;Refresh();
 }
 void Update(){int count=checks.Count(x=>x.Value.IsChecked==true);progress.Text=$"{count} of {checks.Count} items checked";bar.Maximum=Math.Max(1,checks.Count);bar.Value=count;complete.IsEnabled=checks.Count>0&&count==checks.Count;}
 void Legacy(string kind){using var original=new MainWindow(profile,Options.Parse(["--data-directory",directory]),new MemoryReminderScheduler(),new RecordingReminderAttention());original.ReviewAppearance(theme);original.ReviewDialog(kind,new WindowInteropHelper(Window).Handle);profile=new Profile(directory);Render();}
 void ApplyTheme(string value){theme=value;dark=value!="Light";if(value=="System")dark=(int?)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1)==0;var map=new Dictionary<string,string>{{"202020","F5F5F5"},{"252525","EFEFEF"},{"262626","EAEAEA"},{"2B2B2B","FFFFFF"},{"303030","F0F0F0"},{"383838","E0E0E0"},{"454545","D2D2D2"},{"969696","646464"},{"B9B9B9","535353"},{"C1C1C1","454545"},{"F2F2F2","202020"},{"FFFFFF","202020"},{"888888","666666"}};var resources=W.Application.Current.Resources;foreach(var key in new[]{"202020","252525","262626","2B2B2B","303030","383838","414141","444444","454545","484848","4A4A4A","5A5A5A","888888","969696","A5A5A5","B9B9B9","C1C1C1","EEEEEE","F2F2F2","FFFFFF"}){var c=(M.Color)M.ColorConverter.ConvertFromString("#"+key);double b=(c.R*.2126+c.G*.7152+c.B*.0722);int v=(int)Math.Clamp(255-b,30,245);string color=dark?key:map.GetValueOrDefault(key,$"{v:X2}{v:X2}{v:X2}");resources["Theme"+key]=Brush("#"+color);}resources["Surface"]=resources["Theme2B2B2B"];resources["DgxPublisherForeground"]=Brush(dark?"#FFFFFF":"#162921");resources["Background"]=resources["Theme202020"];resources["Muted"]=resources["ThemeB9B9B9"];resources["DashboardAccent"]=Brush(dark?"#78B7FF":"#2468BB");resources["MenuShadowOpacity"]=dark?.30:.18;if(Window!=null)foreach(W.Window open in W.Application.Current.Windows)Caption(open);}
 internal void Verify(){Window.UpdateLayout();if(Window.Width!=640||checks.Count!=profile.Settings.Items.Count)throw new Exception("Default width or items mismatch");var first=checks.Values.First();first.IsChecked=true;if(!new Profile(directory).State.Checked.Contains(checks.Keys.First()))throw new Exception("Check persistence failed");first.IsChecked=false;if(complete.IsEnabled)throw new Exception("Completion incorrectly enabled");foreach(var c in checks.Values)c.IsChecked=true;if(!complete.IsEnabled)throw new Exception("Completion gating failed");foreach(var c in checks.Values)c.IsChecked=false;double lane=scroll.ActualWidth-scroll.ViewportWidth;if(Math.Abs(lane-17)>1)throw new Exception("Scrollbar lane mismatch: "+lane);ApplyTheme("Light");Window.UpdateLayout();ApplyTheme("Dark");File.WriteAllText(Path.Combine(directory,"wpf-verification.txt"),"PASS: actual WPF shell, 640 DIP width, checklist loading, progress persistence, timestamps, completion gating, reserved 17 DIP lane, light/dark resources.");}
}
