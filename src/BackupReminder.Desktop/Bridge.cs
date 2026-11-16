global using System.IO;
namespace BackupReminder;
internal sealed partial class MainWindow
{
 internal void ReviewDialog(string kind,IntPtr owner)
 {
  using var dialog=kind switch {"editor"=>CreateChecklistEditor(),"settings"=>CreateSettings(),"help"=>CreateHelp(),_=>CreateAbout()};
  dialog.Owner=null;dialog.ShowDialog(new ReviewOwner(owner));
 }
 sealed class ReviewOwner(IntPtr handle):IWin32Window {public IntPtr Handle=>handle;}
 internal static IReadOnlyDictionary<string,string> ReviewTopics=>Topics;
 internal System.Drawing.Image ReviewIcon(ChecklistEntry item)=>LoadIcon(item,false);
 internal void ReviewAppearance(string theme)=>SetAppearance(theme);
}
