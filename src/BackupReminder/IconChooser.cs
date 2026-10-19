using System.Runtime.InteropServices;

namespace BackupReminder;

internal sealed record IconChoice(string Path,int? Index)
{
    internal string Name=>Index is int index?System.IO.Path.GetFileNameWithoutExtension(Path)+" icon "+(index+1)+".png":System.IO.Path.GetFileName(Path);
}
internal static class ShellIcons
{
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="ExtractIconExW")]
    static extern uint Extract(string file,int index,[Out] IntPtr[]? large,[Out] IntPtr[]? small,uint count);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr icon);
    internal static string DefaultLibrary=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shell32.dll");
    internal static bool IsLibrary(string file)=>Path.GetExtension(file).ToLowerInvariant() is ".exe" or ".dll" or ".ico";
    internal static int Count(string file)
    {
        uint count=Extract(file,-1,null,null,0);return count==uint.MaxValue?0:(int)Math.Min(count,1024);
    }
    internal static Bitmap Read(string file,int index)
    {
        if(index<0)throw new InvalidDataException("Choose an icon from the list.");
        var handles=new IntPtr[1];
        try
        {
            if(Extract(file,index,handles,null,1)!=1||handles[0]==IntPtr.Zero)throw new InvalidDataException("This icon could not be read. Choose another icon.");
            using var icon=Icon.FromHandle(handles[0]);return icon.ToBitmap();
        }
        finally{if(handles[0]!=IntPtr.Zero)DestroyIcon(handles[0]);}
    }
    internal static Bitmap Associated(string file)
    {
        using var icon=Icon.ExtractAssociatedIcon(file)??throw new InvalidDataException("No icon could be extracted from this program.");return icon.ToBitmap();
    }
    internal static string InitialSource(string directory,string? selected,string? target)
    {
        foreach(string? value in new[]{selected,target})
        {
            string file=PickerLocations.Resolve(directory,value);
            if(File.Exists(file))return file;
        }
        return DefaultLibrary;
    }
}
internal sealed partial class MainWindow
{
    internal Form CreateIconChooser(string selected,string? target,int? currentIndex=null)
    {
        var (dialog,area,footer)=StyledPage("Choose custom icon","Select a Windows icon, or browse for a program, icon or image.",new Size(760,620));
        var card=new RoundedPanel {Dock=DockStyle.Fill,Padding=new Padding(18)};area.Controls.Add(card);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));card.Controls.Add(layout);
        var sourceRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        sourceRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));layout.Controls.Add(sourceRow,0,0);
        var sourceHost=new RoundedPanel {Dock=DockStyle.Fill,Tag="window",Padding=new Padding(12,10,12,8),Margin=new Padding(0,0,12,10)};
        var source=new TextBox {Name="IconSource",ReadOnly=true,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,AccessibleName="Icon source file"};sourceHost.Controls.Add(source);sourceRow.Controls.Add(sourceHost,0,0);
        var browse=MakeButton("Browse...");browse.Name="BrowseIconSource";browse.Dock=DockStyle.Fill;browse.Margin=new Padding(0,0,0,10);sourceRow.Controls.Add(browse,1,0);
        var images=new ImageList {ImageSize=new Size(32,32),ColorDepth=ColorDepth.Depth32Bit};
        var viewport=new EditorScrollArea {Name="IconViewport",Dock=DockStyle.Fill};layout.Controls.Add(viewport,0,1);
        var list=new ListView {Name="IconChoices",View=View.LargeIcon,MultiSelect=false,HideSelection=false,LargeImageList=images,ShowItemToolTips=true,BorderStyle=BorderStyle.None,Scrollable=false,AccessibleName="Available icons"};viewport.SetContent(list);
        void SizeGallery()
        {
            if(list.Items.Count>0)list.Height=Math.Max(80,list.GetItemRect(list.Items.Count-1).Bottom+12);
        }
        int lastWidth=0;list.SizeChanged+=(_,_)=>{if(list.Width!=lastWidth){lastWidth=list.Width;SizeGallery();}};
        void RevealSelection()
        {
            if(list.SelectedItems.Count!=1)return;
            Rectangle bounds=list.GetItemRect(list.SelectedItems[0].Index);
            if(bounds.Top<viewport.ScrollOffset)viewport.SetOffset(bounds.Top);
            else if(bounds.Bottom>viewport.ScrollOffset+viewport.ContentViewport.Height)viewport.SetOffset(bounds.Bottom-viewport.ContentViewport.Height);
        }
        list.Enter+=(_,_)=>RevealSelection();
        list.MouseWheel+=(_,e)=>viewport.SetOffset(viewport.ScrollOffset-e.Delta/120*72);
        var hint=new Label {Name="IconChoiceStatus",Dock=DockStyle.Fill,Tag="muted",TextAlign=ContentAlignment.MiddleLeft};layout.Controls.Add(hint,0,2);
        var use=PageButton(footer,"Use icon",112,true);use.Name="UseIcon";
        var cancel=PageButton(footer,"Cancel",110);cancel.Name="CancelIcon";cancel.Click+=(_,_)=>{dialog.DialogResult=DialogResult.Cancel;dialog.Close();};
        void SelectIcon()
        {
            if(list.SelectedItems.Count!=1)return;
            dialog.Tag=(IconChoice)list.SelectedItems[0].Tag!;dialog.DialogResult=DialogResult.OK;dialog.Close();
        }
        use.Click+=(_,_)=>SelectIcon();list.DoubleClick+=(_,_)=>SelectIcon();
        list.SelectedIndexChanged+=(_,_)=>{use.Enabled=list.SelectedItems.Count==1;RevealSelection();};
        void LoadSource(string file)
        {
            list.BeginUpdate();list.Items.Clear();images.Images.Clear();use.Enabled=false;source.Text=file;
            try
            {
                bool library=ShellIcons.IsLibrary(file);int count=library?ShellIcons.Count(file):1;
                for(int index=0;index<count;index++)
                {
                    try
                    {
                        using var bitmap=library?ShellIcons.Read(file,index):Path.GetExtension(file).Equals(".lnk",StringComparison.OrdinalIgnoreCase)?ShellIcons.Associated(file):new Bitmap(file);
                        images.Images.Add(bitmap);
                        var choice=new IconChoice(file,library?index:null);
                        list.Items.Add(new ListViewItem(library?"Icon "+(index+1):Path.GetFileName(file),images.Images.Count-1){Tag=choice,ToolTipText=choice.Name});
                    }
                    catch(Exception error) when(error is ArgumentException or IOException or ExternalException){}
                }
                hint.Text=list.Items.Count>0?"Choose an icon. It is copied to your data folder only when you save the checklist.":"No icons were found. Browse for another program, icon or image.";
                if(list.Items.Count>0)
                {
                    var chosen=list.Items.Cast<ListViewItem>().FirstOrDefault(x=>((IconChoice)x.Tag!).Index==currentIndex)??list.Items[0];
                    chosen.Selected=true;chosen.Focused=true;
                }
            }
            catch(Exception error) when(error is ArgumentException or IOException or ExternalException or UnauthorizedAccessException){hint.Text="This file could not be read. Browse for another icon or image.";}
            finally{list.EndUpdate();SizeGallery();RevealSelection();}
        }
        browse.Click+=(_,_)=>
        {
            using var picker=AssetPicker.Create(profile.DirectoryPath,source.Text,false);
            if(picker.ShowDialog(dialog)==DialogResult.OK)LoadSource(picker.FileName);
        };
        dialog.Shown+=(_,_)=>{LoadSource(ShellIcons.InitialSource(profile.DirectoryPath,selected,target));list.Focus();};
        dialog.Disposed+=(_,_)=>images.Dispose();dialog.AcceptButton=use;dialog.CancelButton=cancel;FinishPage(dialog);list.BackColor=Surface;return dialog;
    }
}
