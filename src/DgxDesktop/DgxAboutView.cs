using CheckBox=System.Windows.Controls.CheckBox;
using Orientation=System.Windows.Controls.Orientation;
using Brushes=System.Windows.Media.Brushes;
using Control=System.Windows.Controls.Control;
using ComboBox=System.Windows.Controls.ComboBox;
using Button=System.Windows.Controls.Button;
using Image=System.Windows.Controls.Image;
using HorizontalAlignment=System.Windows.HorizontalAlignment;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

internal static class DgxAboutView {
 internal const double Width=560, Height=580;
 internal static DockPanel Create(string name,string version,string description,string legal,ImageSource brand,FrameworkElement publisher,Action website,Action license) {
  var root=new DockPanel{Margin=new Thickness(28)};TextOptions.SetTextFormattingMode(root,TextFormattingMode.Display);TextElement.SetFontFamily(root,new System.Windows.Media.FontFamily("Segoe UI Variable Text"));
  var close=new Button{Content="Close",IsCancel=true,IsDefault=true,MinWidth=88,Height=36,Padding=new Thickness(12,6,12,6),HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
  DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
  var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  var header=new DockPanel{Height=64,Margin=new Thickness(0,0,0,24)};
  header.Children.Add(new Image{Source=brand,Width=64,Height=64,Margin=new Thickness(0,0,18,0)});
  var identity=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
  identity.Children.Add(new TextBlock{Text=name,FontSize=25,FontWeight=FontWeights.SemiBold});
  identity.Children.Add(new TextBlock{Text="Version "+version,FontSize=14,Margin=new Thickness(0,4,0,0)});header.Children.Add(identity);body.Children.Add(header);
  var card=new StackPanel{Margin=new Thickness(18)};
  card.Children.Add(new TextBlock{Text=description,FontSize=14,TextWrapping=TextWrapping.Wrap,Height=40});
  card.Children.Add(new TextBlock{Text="Developed by",FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,16,0,4)});
  publisher.Width=140;publisher.Height=140*655.0/1726;publisher.HorizontalAlignment=HorizontalAlignment.Left;publisher.Margin=new Thickness(0,0,0,12);card.Children.Add(publisher);
  var link=new Hyperlink(new Run("dgx.digitalguy.me")){TextDecorations=new TextDecorationCollection()};link.SetResourceReference(TextElement.ForegroundProperty,"DashboardAccent");link.Click+=(s,e)=>website();card.Children.Add(new TextBlock(link){FontSize=14});
  var frame=new Border{Height=216,CornerRadius=new CornerRadius(9),Child=card};frame.SetResourceReference(Border.BackgroundProperty,"Surface");body.Children.Add(frame);
  body.Children.Add(new TextBlock{Text=legal,FontSize=14,TextWrapping=TextWrapping.Wrap,Height=60,Margin=new Thickness(0,8,0,4)});
  var button=new Button{Content="View icon license",Height=36,MinWidth=88,Padding=new Thickness(12,6,12,6),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};button.Click+=(s,e)=>license();body.Children.Add(button);return root;
 }
}

internal static class DgxSetupWelcome {
 internal const double Height=700;
 internal static StackPanel Create(string name,string purpose,Button restore,UIElement extra=null){
  var p=new StackPanel();TextOptions.SetTextFormattingMode(p,TextFormattingMode.Display);TextElement.SetFontFamily(p,new System.Windows.Media.FontFamily("Segoe UI Variable Text"));
  p.Children.Add(new TextBlock{Text="Welcome to "+name,FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
  p.Children.Add(new TextBlock{Text="Choose Next to set appearance, window behavior and "+purpose+". Finish saves your choices; Skip setup leaves them unchanged.",FontSize=14,TextWrapping=TextWrapping.Wrap,Height=60});
  var line=new Border{Height=1,Margin=new Thickness(0,16,0,16)};line.SetResourceReference(Border.BackgroundProperty,"Theme454545");p.Children.Add(line);
  p.Children.Add(new TextBlock{Text="Returning to "+name+"?",FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
  p.Children.Add(new TextBlock{Text="Restore your configuration backup to bring back your saved settings and content. Your current configuration is preserved before restoring.",FontSize=14,TextWrapping=TextWrapping.Wrap,Height=48});
  restore.Height=36;restore.MinWidth=88;restore.Padding=new Thickness(12,6,12,6);restore.HorizontalAlignment=HorizontalAlignment.Left;restore.Margin=new Thickness(0);p.Children.Add(restore);
  if(extra!=null){var frame=new Border{Child=extra,Margin=new Thickness(0,20,0,0)};p.Children.Add(frame);}return p;
 }
}

internal static class DgxHelpTypography {
 internal static void Apply(FrameworkElement root) { DgxUi.ApplyTypography(root); }
}

internal static class DgxSampleContent {
 internal static StackPanel Create(Func<bool,string> apply) {
  var panel=new StackPanel{Margin=new Thickness(0,16,0,0)};
  panel.Children.Add(new TextBlock{Text="Optional sample content",FontSize=DgxUi.SubheadingSize,FontWeight=FontWeights.SemiBold});
  panel.Children.Add(new TextBlock{Text="Load examples without replacing your content. Remove samples keeps examples you have edited. These actions apply immediately.",TextWrapping=TextWrapping.Wrap,FontSize=DgxUi.BodySize,Margin=new Thickness(0,6,0,10)});
  var actions=new WrapPanel();var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
  foreach(bool remove in new[]{false,true}){bool action=remove;var button=new Button{Content=remove?"Remove samples":"Load sample content",Height=36,MinWidth=88,Padding=new Thickness(12,0,12,0),Margin=new Thickness(0,0,8,0)};button.Click+=(s,e)=>{try{status.Text=apply(action);}catch(Exception error){status.Text="Could not change samples: "+error.Message;}};actions.Children.Add(button);}
  panel.Children.Add(actions);panel.Children.Add(status);return panel;
 }
 internal static int Merge<T>(System.Collections.Generic.List<T> target,System.Collections.Generic.IEnumerable<T> samples,Func<T,string> key,Func<T,T,bool> unchanged,bool remove){
  int count=0;
  foreach(var sample in samples){var existing=target.FindIndex(item=>string.Equals(key(item),key(sample),StringComparison.Ordinal));
   if(!remove&&existing<0){target.Add(sample);count++;}
   else if(remove&&existing>=0&&unchanged(target[existing],sample)){target.RemoveAt(existing);count++;}
  }
  return count;
 }
 internal static string Result(int count,bool remove){return count+" sample entries "+(remove?"removed. Edited or completed samples and your own content were kept.":"added. Existing entries were kept; no reminders or startup settings were changed.");}
}

// Article structure is shared; applications supply only the topic text.
internal sealed class DgxHelpArticle : StackPanel {
 string value="";
 internal string Text {
  get{return value;}
  set{
   this.value=value??"";Children.Clear();DgxUi.ApplyTypography(this);
   var lines=this.value.Replace("\r\n","\n").Split('\n');bool first=true;
   foreach(var raw in lines){
    var line=raw.Trim();if(line.Length==0)continue;
    bool heading=first||line.StartsWith("## ");
    string content=line.StartsWith("## ")?line.Substring(3):line;
    bool bullet=content.StartsWith("- ");
    var text=new TextBlock{Text=bullet?content.Substring(2):content,TextWrapping=TextWrapping.Wrap,
     FontSize=first?DgxUi.SectionSize:heading?DgxUi.SubheadingSize:DgxUi.BodySize,
     FontWeight=heading?FontWeights.SemiBold:FontWeights.Normal,
     LineHeight=heading?double.NaN:21,Margin=new Thickness(0,heading&&!first?8:0,0,heading?12:10)};
    if(bullet){var row=new DockPanel();var marker=new TextBlock{Text="•",Width=20,FontSize=DgxUi.BodySize};DockPanel.SetDock(marker,Dock.Left);row.Children.Add(marker);row.Children.Add(text);Children.Add(row);}
    else Children.Add(text);
    first=false;
   }
  }
 }
}

// Compiled into all three apps. Every app-owned WPF window uses this contract.
internal static class DgxUi {
 internal const double BodySize=14, SectionSize=22, MainTitleSize=29, EditorTitleSize=27, SubheadingSize=16;
 static bool installed;
 internal static void Install() {
  if(installed)return;installed=true;
  EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,
   new RoutedEventHandler((sender,e)=>{if(object.ReferenceEquals(sender,e.OriginalSource))ApplyWindow((Window)sender);}));
 }
 internal static void ApplyTypography(FrameworkElement root) {
  TextElement.SetFontFamily(root,new System.Windows.Media.FontFamily("Segoe UI Variable Text"));
  TextElement.SetFontSize(root,BodySize);
  TextElement.SetFontWeight(root,FontWeights.Normal);
  TextElement.SetFontStyle(root,FontStyles.Normal);
  TextOptions.SetTextFormattingMode(root,TextFormattingMode.Display);
  TextOptions.SetTextRenderingMode(root,TextRenderingMode.Auto);
  root.UseLayoutRounding=true;root.SnapsToDevicePixels=true;
 }
 internal static void ApplyWindow(Window window) { ApplyTypography(window); }
 internal static DockPanel NoticeBody(string title,string message,out WrapPanel actions) {
  var body=new DockPanel{Margin=new Thickness(28)};ApplyTypography(body);
  var heading=new TextBlock{Text=title,FontSize=SectionSize,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
  DockPanel.SetDock(heading,Dock.Top);body.Children.Add(heading);
  actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
  DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);
  heading.Measure(new System.Windows.Size(464,double.PositiveInfinity));
  var text=new TextBlock{Text=message,FontSize=BodySize,FontWeight=FontWeights.Normal,TextWrapping=TextWrapping.Wrap};text.SetResourceReference(TextBlock.ForegroundProperty,"ThemeB9B9B9");
  body.Children.Add(new ScrollViewer{Content=text,MaxHeight=Math.Max(80,SystemParameters.WorkArea.Height-heading.DesiredSize.Height-180),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
  return body;
 }
 internal static void ConfigureNotice(Window window) {
  ApplyWindow(window);window.MinHeight=0;window.SizeToContent=SizeToContent.Height;
  window.MaxHeight=SystemParameters.WorkArea.Height;window.ResizeMode=ResizeMode.NoResize;
 }
}

internal sealed class DgxSetupAppearance {
 internal const double Width=760, PageHeight=700;
 internal readonly StackPanel Panel=new StackPanel();
 internal readonly ComboBox Appearance;
 internal readonly CheckBox CloseToTray, MinimizeToTray, Startup, StartMinimized;
 internal DgxSetupAppearance(string appearance,bool close,bool minimize,bool startup,bool startMinimized) {
  DgxUi.ApplyTypography(Panel);
  Panel.Children.Add(new TextBlock{Text="Appearance and window behavior",FontSize=DgxUi.SectionSize,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
  var label=new TextBlock{Text="Appearance",FontSize=DgxUi.BodySize,Margin=new Thickness(0,3,0,5)};label.SetResourceReference(TextBlock.ForegroundProperty,"ThemeB9B9B9");Panel.Children.Add(label);
  Appearance=new ComboBox{ItemsSource=new[]{"System","Light","Dark"},SelectedItem=appearance,Width=240,HorizontalAlignment=HorizontalAlignment.Left};System.Windows.Automation.AutomationProperties.SetName(Appearance,"Setup appearance");Panel.Children.Add(Appearance);
  CloseToTray=Option("Close to the system tray",close);MinimizeToTray=Option("Minimize to the system tray",minimize);
  Startup=Option("Start when I sign in to Windows",startup);StartMinimized=Option("Start minimized in the system tray",startMinimized);
 }
 CheckBox Option(string label,bool value) {var c=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,8,0,10)};System.Windows.Automation.AutomationProperties.SetName(c,label);Panel.Children.Add(c);return c;}
}

internal sealed class DgxSetupFrame {
 internal const double Width=760, Height=700;
 internal readonly DockPanel Root=new DockPanel{Margin=new Thickness(28)};
 internal readonly TextBlock Progress=new TextBlock{FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,3,0,12)};
 internal readonly ScrollViewer Host=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
 internal readonly Button Skip=Action("Skip setup"),Back=Action("Back"),Next=Action("Next");
 internal DgxSetupFrame(){
  DgxUi.ApplyTypography(Root);DockPanel.SetDock(Progress,Dock.Top);Root.Children.Add(Progress);
  var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
  DockPanel.SetDock(footer,Dock.Bottom);Root.Children.Add(footer);Root.Children.Add(Host);
  Skip.IsCancel=true;Next.IsDefault=true;footer.Children.Add(Skip);footer.Children.Add(Back);footer.Children.Add(Next);
 }
 static Button Action(string text){return new Button{Content=text,Height=36,MinWidth=88,Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)};}
 internal void Configure(Window window){window.SizeToContent=SizeToContent.Manual;window.Width=Math.Min(Width,SystemParameters.WorkArea.Width);window.Height=Math.Min(Height,SystemParameters.WorkArea.Height);window.MinHeight=0;window.ResizeMode=ResizeMode.NoResize;}
 internal void Show(int index,FrameworkElement[] pages){Progress.Text="Step "+(index+1)+" of "+pages.Length;Host.Content=pages[index];Host.ScrollToTop();Back.Visibility=index==0?Visibility.Collapsed:Visibility.Visible;Next.Content=index==pages.Length-1?"Finish":"Next";}
 internal static StackPanel Finish(string name,string detail){var p=new StackPanel();p.Children.Add(new TextBlock{Text="Ready to use "+name,FontSize=DgxUi.SectionSize,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});p.Children.Add(new TextBlock{Text="Finish saves your choices. You can change them later in Settings.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,16)});p.Children.Add(new TextBlock{Text=detail,TextWrapping=TextWrapping.Wrap});return p;}
}

internal static class DgxUnsavedChanges {
 internal static bool Confirm(Window owner,Func<UIElement,Window> create,Action save){
  WrapPanel actions;var root=DgxUi.NoticeBody("Unsaved changes","Keep your changes before closing?",out actions);
  var dialog=create(root);dialog.Owner=owner;dialog.Width=560;DgxUi.ConfigureNotice(dialog);bool accepted=false;
  var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};
  DockPanel.SetDock(error,Dock.Top);root.Children.Insert(1,error);
  foreach(var label in new[]{"Save changes","Discard changes","Keep editing"}){
   var button=new Button{Content=label,Height=36,MinWidth=88,Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)};actions.Children.Add(button);
   if(label=="Keep editing"){button.IsCancel=true;button.IsDefault=true;continue;}
   button.Click+=(s,e)=>{try{if(label=="Save changes")save();accepted=true;dialog.Close();}catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}};
  }
  dialog.ShowDialog();return accepted;
 }
}

internal sealed class DgxDetailRow {
 internal readonly Grid Root=new Grid{Margin=new Thickness(0,8,0,8)};
 internal readonly StackPanel Text=new StackPanel();
 internal readonly WrapPanel Actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
 internal readonly TextBlock Heading;
 internal DgxDetailRow(string title){
  Root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  Root.Children.Add(Text);Grid.SetRow(Actions,1);Root.Children.Add(Actions);
  Heading=new TextBlock{Text=title,FontSize=DgxUi.SubheadingSize,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};Text.Children.Add(Heading);
 }
 internal void AddAction(Button button){button.Height=36;button.MinWidth=88;button.Padding=new Thickness(12,6,12,6);button.Margin=new Thickness(Actions.Children.Count==0?0:8,0,0,0);button.VerticalAlignment=VerticalAlignment.Center;Actions.Children.Add(button);}
}

internal sealed class DgxWindowSettingsPage {
 internal readonly StackPanel Panel=new StackPanel{Margin=new Thickness(0,0,14,0)};
 internal readonly CheckBox CloseToTray,MinimizeToTray,Startup,StartMinimized,LockSizes;
 internal readonly Button Reset;
 internal bool ResetRequested;
 internal DgxWindowSettingsPage(string name,bool close,bool minimize,bool startup,bool startMinimized,bool locked){
  DgxUi.ApplyTypography(Panel);
  Panel.Children.Add(new TextBlock{Text="Window & startup",FontSize=DgxUi.SectionSize,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
  Label("Window buttons",true);CloseToTray=Option("Close to the system tray",close);MinimizeToTray=Option("Minimize to the system tray",minimize);
  Label("Use File > Exit to quit "+name+".");Rule();Label("Startup",true);Startup=Option("Start when I sign in to Windows",startup);StartMinimized=Option("Start minimized in the system tray",startMinimized);
  Rule();Label("Window sizes",true);LockSizes=Option("Lock window sizes",locked);Label("Keep your chosen sizes. Windows can still be moved or minimized. Settings stays resizable.");
  var row=new Grid{Height=36,Margin=new Thickness(0,8,0,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition());
  Reset=new Button{Content="Reset window sizes",HorizontalAlignment=HorizontalAlignment.Left,Height=36,MinWidth=88,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0)};row.Children.Add(Reset);
  var note=new TextBlock{Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};note.SetResourceReference(TextBlock.ForegroundProperty,"ThemeB9B9B9");Grid.SetColumn(note,1);row.Children.Add(note);Panel.Children.Add(row);
  Reset.Click+=(s,e)=>{ResetRequested=true;LockSizes.IsChecked=false;note.Text="Choose Save to apply the reset.";note.ToolTip=note.Text;};
 }
 void Label(string text,bool heading=false){var t=new TextBlock{Text=text,FontSize=heading?DgxUi.SubheadingSize:DgxUi.BodySize,FontWeight=heading?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,5)};if(!heading)t.SetResourceReference(TextBlock.ForegroundProperty,"ThemeB9B9B9");Panel.Children.Add(t);}
 CheckBox Option(string text,bool selected){var box=new CheckBox{Content=text,IsChecked=selected,Margin=new Thickness(0,8,0,10)};Panel.Children.Add(box);return box;}
 void Rule(){var line=new Border{Height=1,Margin=new Thickness(0,16,0,16)};line.SetResourceReference(Border.BackgroundProperty,"Theme454545");Panel.Children.Add(line);}
}

internal static class DgxEditorLayout {
 internal static void Configure(Window window){window.MinWidth=Math.Min(940,SystemParameters.WorkArea.Width);window.MinHeight=Math.Min(700,SystemParameters.WorkArea.Height);window.Width=Math.Max(window.MinWidth,window.Width);window.Height=Math.Max(window.MinHeight,window.Height);}
}

internal static class DgxSegmentedFilter {
 internal static FrameworkElement Create(string[] labels,int selected,Action<int> select){
  var items=new StackPanel{Orientation=Orientation.Horizontal};
  var root=new Border{Child=items,Padding=new Thickness(3),CornerRadius=new CornerRadius(7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8)};root.SetResourceReference(Border.BackgroundProperty,"Theme383838");
  var template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToggleButton'><Border x:Name='Frame' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' CornerRadius='4' Padding='{TemplateBinding Padding}'><ContentPresenter x:Name='Label' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource Theme454545}'/><Setter TargetName='Label' Property='TextElement.Foreground' Value='{DynamicResource DashboardAccent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderThickness' Value='1'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource DashboardAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  for(int i=0;i<labels.Length;i++){int index=i;var b=new System.Windows.Controls.Primitives.ToggleButton{Content=labels[i],IsChecked=i==selected,Height=30,MinWidth=88,Padding=new Thickness(12,4,12,4),Margin=new Thickness(0),Background=Brushes.Transparent,Template=template};b.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");System.Windows.Automation.AutomationProperties.SetName(b,labels[i]);b.Click+=(s,e)=>select(index);items.Children.Add(b);}return root;
 }
}
