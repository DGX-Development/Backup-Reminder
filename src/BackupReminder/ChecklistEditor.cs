using System.Drawing.Drawing2D;

namespace BackupReminder;

internal sealed partial class MainWindow
{
    Form CreateChecklistEditor()
    {
        var draft = profile.CopySettings();
        ChecklistEntry? selected=null;
        Action? refreshSelected=null;
        var iconSelection=new IconSelection(null,null);
        NormalizeGroups(draft);
        var dialog = NewDialog("Edit checklist", new Size(1060,820));
        dialog.MinimumSize = new Size(940,720);
        dialog.Tag = "window";
        var entryImages=new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
        Image EntryImage(ChecklistEntry entry)
        {
            string source=string.IsNullOrWhiteSpace(entry.IconPath)?entry.Target??"":entry.IconPath;
            string initial=System.Globalization.StringInfo.GetNextTextElement(string.IsNullOrWhiteSpace(entry.Title)?"?":entry.Title);
            string key=source+"\n"+entry.IconIndex+"\n"+initial;
            if(!entryImages.TryGetValue(key,out var image)){image=LoadIcon(entry,false);entryImages.Add(key,image);}
            return image;
        }
        dialog.Disposed+=(_,_)=>{foreach(var image in entryImages.Values)image.Dispose();};
        var page = new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=3, ColumnCount=1, Padding=new Padding(28,0,28,0), Tag="window" };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,104));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
        dialog.Controls.Add(page);
        var header = new Panel { Dock=DockStyle.Fill, Tag="window", Margin=Padding.Empty };
        header.Controls.Add(new Label { Text="Edit your checklist", Font=heading, Bounds=new Rectangle(0,20,800,38), Tag="window" });
        header.Controls.Add(new Label { Text="Arrange your backup routine, then choose what each step opens.", Bounds=new Rectangle(2,65,850,25), Tag="muted" });
        page.Controls.Add(header,0,0);
        var columns = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Tag="window", Margin=Padding.Empty };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        page.Controls.Add(columns,0,1);
        var navigation = new Panel { Dock=DockStyle.Fill, Padding=new Padding(0,0,22,0), Tag="window", Margin=Padding.Empty };
        columns.Controls.Add(navigation,0,0);
        var navHeader = new Panel { Dock=DockStyle.Top, Height=97, Tag="window" };
        navHeader.Controls.Add(new Label { Text="YOUR CHECKLIST", Font=small, Tag="muted", Bounds=new Rectangle(2,0,280,22) });
        var addTask = MakeButton("+ Add task"); addTask.Name="AddTask"; addTask.SetBounds(0,35,146,38);
        var addCategory = MakeButton("+ Add group"); addCategory.Name="AddCategory"; addCategory.SetBounds(154,35,150,38);
        navHeader.Controls.Add(addTask); navHeader.Controls.Add(addCategory);
        var tree = new ChecklistTree {
            Name="ChecklistOutline", Dock=DockStyle.Fill, BorderStyle=BorderStyle.None, HideSelection=false,
            DrawMode=TreeViewDrawMode.OwnerDrawAll, ShowLines=false, ShowPlusMinus=false, ShowRootLines=false,
            FullRowSelect=true, ItemHeight=57, Indent=20, Tag="window", AccessibleName="Checklist tasks and groups"
        };
        tree.BeforeCollapse+=(_,e)=>e.Cancel=true;
        var controls = new Panel { Dock=DockStyle.Bottom, Height=61, Tag="window" };
        var up = MakeButton("Move up"); up.Name="MoveUp"; up.SetBounds(0,15,96,36);
        var down = MakeButton("Move down"); down.Name="MoveDown"; down.SetBounds(104,15,96,36);
        var remove = MakeButton("Remove"); remove.Name="RemoveEntry"; remove.SetBounds(208,15,96,36);
        controls.Controls.AddRange([up,down,remove]);
        navigation.Controls.Add(tree); navigation.Controls.Add(navHeader); navigation.Controls.Add(controls);
        var details = new RoundedPanel { Dock=DockStyle.Fill, Padding=new Padding(24,20,24,16), Margin=Padding.Empty };
        columns.Controls.Add(details,1,0);
        var scrollFields = new EditorScrollArea { Name="EditorFields", Dock=DockStyle.Fill };
        var fields = new TableLayoutPanel { Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, ColumnCount=1, Margin=Padding.Empty };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        scrollFields.SetContent(fields); details.Controls.Add(scrollFields);
        var detailHeading = new Label { Text="Task details", Font=groupFont, Height=36, Dock=DockStyle.Fill, Margin=Padding.Empty };
        fields.Controls.Add(detailHeading);
        var detailHint = new Label { Text="Give this step a name and choose what it opens.", Tag="muted", Dock=DockStyle.Fill, Height=38, Margin=Padding.Empty };
        fields.Controls.Add(detailHint);
        TextBox Field(TableLayoutPanel parent, string label, string name, int height=42, string? browse=null)
        {
            var row = new Panel { Dock=DockStyle.Fill, Height=height+45, Margin=Padding.Empty };
            row.Controls.Add(new Label { Text=label, Bounds=new Rectangle(0,0,500,24) });
            var host = new RoundedPanel { Tag="window", Bounds=new Rectangle(0,28,300,height), Padding=new Padding(12,10,12,8) };
            var input = new TextBox { Name=name, AccessibleName=label, Dock=DockStyle.Fill, BorderStyle=BorderStyle.None, Multiline=height>42, ScrollBars=ScrollBars.None };
            host.Controls.Add(input); row.Controls.Add(host);
            if(name=="EntryIcon")
            {
                host.Left=42;
                row.Controls.Add(new PictureBox {Name="EntryIconPreview",AccessibleName="Current task icon",SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(0,33,32,32)});
            }
            Button? choose=null;
            if(browse is not null)
            {
                choose=MakeButton("Browse..."); choose.Width=90;choose.Name=browse=="icon"?"BrowseIcon":"BrowseWorkingDirectory";
                choose.Click+=(_,_)=>
                {
                    if(browse=="folder")
                    { using var picker=PickerLocations.WorkingDirectory(profile.DirectoryPath,input.Text,selected?.Target); if(picker.ShowDialog(dialog)==DialogResult.OK)input.Text=picker.SelectedPath; }
                    else
                    {
                        using var picker=CreateIconChooser(iconSelection.SelectedPath(input.Text),selected?.Target,selected?.IconIndex);
                        picker.Owner=null;
                        if(picker.ShowDialog(dialog)==DialogResult.OK&&picker.Tag is IconChoice choice&&selected is not null)
                        {
                            selected.IconPath=choice.Path;selected.IconIndex=choice.Index;selected.IconName=choice.Name;
                            iconSelection=new(choice.Path,choice.Name,choice.Index);input.Text=iconSelection.DisplayText;
                            refreshSelected?.Invoke();
                        }
                    }
                };
                row.Controls.Add(choose);
            }
            row.SizeChanged+=(_,_)=>
            {
                foreach(var labelControl in row.Controls.OfType<Label>())labelControl.Width=row.ClientSize.Width;
                host.Width=Math.Max(100,row.ClientSize.Width-host.Left-(choose is null?2:102));
                if(choose is not null)choose.SetBounds(row.ClientSize.Width-92,28,90,42);
            };
            parent.Controls.Add(row); return input;
        }
        var name = Field(fields,"Name","EntryName");
        var detail = Field(fields,"Description","EntryDescription",82);
        var launch = new Panel { Dock=DockStyle.Fill, Height=118, Margin=Padding.Empty };
        launch.Controls.Add(new Label { Text="What to open", Bounds=new Rectangle(0,0,400,24) });
        var kind = new EditorPicker { Name="OpenKind", AccessibleName="What to open", Bounds=new Rectangle(0,27,145,42) };
        foreach(string choice in new[]{"Nothing","Program or file","Folder","Website"})kind.AddChoice(choice,choice);
        var targetHost = new RoundedPanel { Tag="window", Bounds=new Rectangle(155,27,210,42), Padding=new Padding(12,10,12,8) };
        var target = new TextBox { Name="EntryTarget", BorderStyle=BorderStyle.None, Dock=DockStyle.Fill };
        targetHost.Controls.Add(target);
        var browseTarget=MakeButton("Browse...");browseTarget.Name="BrowseTarget";
        var launchHint=new Label { Text="A launcher is optional. You can also use this as a simple checklist step.", Tag="muted", Bounds=new Rectangle(0,80,480,32), Font=small };
        launch.Controls.AddRange([kind,targetHost,browseTarget,launchHint]);fields.Controls.Add(launch);
        void LaunchLayout()
        {
            bool browse=kind.SelectedIndex is 1 or 2;
            browseTarget.Visible=browse;
            int width=launch.ClientSize.Width;
            foreach(var labelControl in launch.Controls.OfType<Label>())labelControl.Width=width;
            int targetX=kind.Right+10;
            targetHost.SetBounds(targetX,27,Math.Max(80,width-targetX-(browse?102:2)),42);
            browseTarget.SetBounds(width-92,27,90,42);launchHint.Width=width;
        }
        launch.SizeChanged+=(_,_)=>LaunchLayout();
        var more = MakeButton("More options"); more.Name="MoreOptions";more.Width=150;more.Height=36;
        more.AccessibleDescription="Show or hide group, custom icon and program launch options.";
        var moreRow=new Panel { Dock=DockStyle.Fill, Height=54, Margin=Padding.Empty };moreRow.Controls.Add(more);fields.Controls.Add(moreRow);
        var advanced = new TableLayoutPanel { Name="AdvancedOptions", Dock=DockStyle.Fill, AutoSize=true, ColumnCount=1, Margin=Padding.Empty, Visible=false };
        advanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.Controls.Add(advanced);
        var categoryRow = new Panel { Dock=DockStyle.Fill, Height=112, Margin=Padding.Empty };
        categoryRow.Controls.Add(new Label { Text="Group", Bounds=new Rectangle(0,0,400,24) });
        var category = new EditorPicker {Name="EntryCategory",AccessibleName="Task group",Bounds=new Rectangle(0,28,400,42)};
        categoryRow.Controls.Add(new Label {Text="Tasks in a group appear together under one heading.",Font=small,Tag="muted",Bounds=new Rectangle(0,76,500,32),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right});
        categoryRow.Controls.Add(category);categoryRow.SizeChanged+=(_,_)=>category.Width=Math.Max(100,categoryRow.ClientSize.Width-2);advanced.Controls.Add(categoryRow);
        var buttonText=Field(advanced,"Open button label","EntryButton");
        var icon=Field(advanced,"Custom icon","EntryIcon",browse:"icon");
        var iconPreview=Descendants<PictureBox>(dialog).Single(x=>x.Name=="EntryIconPreview");
        var iconHint=new Label {Text="Save copies custom icons into your data folder. Blank uses the program's icon.",Font=small,Tag="muted",Height=48,Dock=DockStyle.Fill,Margin=Padding.Empty};advanced.Controls.Add(iconHint);
        void UpdateIconHint()=>iconHint.Text=!string.IsNullOrWhiteSpace(icon.Text)&&!File.Exists(profile.ResolveAsset(iconSelection.SelectedPath(icon.Text)))?"Custom icon file is unavailable. The checklist will use a fallback icon.":"Save copies custom icons into your data folder. Blank uses the program's icon.";
        icon.TextChanged+=(_,_)=>UpdateIconHint();
        var arguments=Field(advanced,"Program arguments","EntryArguments");
        var workingDirectory=Field(advanced,"Working directory","EntryWorkingDirectory",browse:"folder");
        more.Click+=(_,_)=>{advanced.Visible=!advanced.Visible;more.Text=advanced.Visible?"Fewer options":"More options";fields.PerformLayout();};
        var footer = new Panel { Dock=DockStyle.Fill, Tag="window", Margin=Padding.Empty };
        var summary = new Label { Tag="muted", AutoSize=true, Location=new Point(2,28) };
        var cancel=MakeButton("Cancel");cancel.Name="CancelEditor";cancel.SetBounds(0,20,110,38);cancel.Click+=(_,_)=>dialog.Close();
        var save=MakeButton("Save changes");save.Name="SaveChecklist";save.Tag="primary";save.SetBounds(0,20,142,38);
        footer.Controls.AddRange([summary,cancel,save]);
        footer.SizeChanged+=(_,_)=>{save.Left=footer.ClientSize.Width-save.Width;cancel.Left=save.Left-cancel.Width-10;};
        page.Controls.Add(footer,0,2);dialog.CancelButton=cancel;
        EditSession? edits=null;
        bool isCategory=false,loading=false;
        void UpdateCounts()
        {
            summary.Text=$"{draft.Items.Count} {(draft.Items.Count==1?"task":"tasks")} / {draft.Groups.Count} {(draft.Groups.Count==1?"group":"groups")}";
            if(edits is null)save.Enabled=draft.Items.Count>0&&draft.Items.Concat(draft.Groups).All(x=>!string.IsNullOrWhiteSpace(x.Title));
            else edits.Refresh();
        }
        void LoadEntry()
        {
            loading=true;
            var choice=tree.SelectedNode?.Tag as EditorNode;
            selected=choice?.Entry;isCategory=choice?.IsCategory??false;
            detailHeading.Text=selected is null?"Add your first task":isCategory?"Group details":"Task details";
            detailHint.Text=selected is null?"Use Add task to start building your routine.":isCategory?"One launcher for the tasks in this group.":"Give this step a name and choose what it opens.";
            name.Text=selected?.Title??"";detail.Text=selected?.Detail??"";target.Text=selected?.Target??"";
            iconSelection=new(selected?.IconPath,selected?.IconName,selected?.IconIndex);
            buttonText.Text=selected?.Button??"";icon.Text=iconSelection.DisplayText;arguments.Text=selected?.Arguments??"";workingDirectory.Text=selected?.WorkingDirectory??"";
            iconPreview.Image=selected is null?null:EntryImage(selected);
            foreach(var input in new Control[]{name,detail,target,kind,more,buttonText,icon,arguments,workingDirectory})input.Enabled=selected is not null;
            categoryRow.Visible=!isCategory;
            category.ClearChoices();category.AddChoice("None (show as a separate task)","");
            foreach(var group in draft.Groups)category.AddChoice(group.Title,group.Id);
            category.SelectedIndex=0;
            for(int i=1;i<category.ChoiceCount;i++)if(string.Equals(category.ChoiceValue(i),selected?.Group,StringComparison.OrdinalIgnoreCase))category.SelectedIndex=i;
            category.Enabled=selected is not null;
            string current=selected?.Target??"";
            if(string.IsNullOrWhiteSpace(current))kind.SelectedIndex=0;
            else if(current.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||current.StartsWith("http://",StringComparison.OrdinalIgnoreCase))kind.SelectedIndex=3;
            else if(current.StartsWith("knownfolder:",StringComparison.OrdinalIgnoreCase)||Directory.Exists(Environment.ExpandEnvironmentVariables(current)))kind.SelectedIndex=2;
            else kind.SelectedIndex=1;
            target.Enabled=selected is not null&&kind.SelectedIndex!=0;browseTarget.Enabled=target.Enabled;
            remove.Enabled=selected is not null;
            int first=selected is null?-1:isCategory?draft.Items.FindIndex(x=>string.Equals(x.Group,selected.Id,StringComparison.OrdinalIgnoreCase)):draft.Items.IndexOf(selected);
            int last=selected is null?-1:isCategory?draft.Items.FindLastIndex(x=>string.Equals(x.Group,selected.Id,StringComparison.OrdinalIgnoreCase)):first;
            var roots=RootEntries(draft);int rootIndex=roots.FindIndex(x=>string.Equals(x.Id,isCategory?selected?.Id:selected?.Group??selected?.Id,StringComparison.OrdinalIgnoreCase));
            up.Enabled=selected is not null&&(rootIndex>0||(!isCategory&&first>0&&string.Equals(draft.Items[first-1].Group,selected.Group,StringComparison.OrdinalIgnoreCase)));
            down.Enabled=selected is not null&&(rootIndex>=0&&rootIndex<roots.Count-1||(!isCategory&&last>=0&&last<draft.Items.Count-1&&string.Equals(draft.Items[last+1].Group,selected.Group,StringComparison.OrdinalIgnoreCase)));
            LaunchLayout();UpdateCounts();loading=false;
        }
        void RefreshTree(string? selectId=null)
        {
            loading=true;tree.BeginUpdate();tree.Nodes.Clear();
            var groups=new Dictionary<string,TreeNode>(StringComparer.OrdinalIgnoreCase);
            TreeNode GroupNode(ChecklistEntry group)
            {
                if(groups.TryGetValue(group.Id,out var existing))return existing;
                var node=new TreeNode(group.Title){Name=group.Id,Tag=new EditorNode(group,true)};groups.Add(group.Id,node);tree.Nodes.Add(node);return node;
            }
            foreach(var entry in RootEntries(draft))
            {
                if(draft.Groups.Contains(entry))
                {
                    var node=GroupNode(entry);
                    foreach(var item in draft.Items.Where(x=>string.Equals(x.Group,entry.Id,StringComparison.OrdinalIgnoreCase)))
                        node.Nodes.Add(new TreeNode(item.Title){Name=item.Id,Tag=new EditorNode(item,false)});
                }
                else tree.Nodes.Add(new TreeNode(entry.Title){Name=entry.Id,Tag=new EditorNode(entry,false)});
            }
            tree.ExpandAll();
            var match=selectId is null?null:tree.Nodes.Find(selectId,true).FirstOrDefault();
            tree.SelectedNode=match??tree.Nodes.Cast<TreeNode>().FirstOrDefault();tree.SelectedNode?.EnsureVisible();
            tree.EndUpdate();loading=false;LoadEntry();
        }
        refreshSelected=()=>RefreshTree(selected?.Id);
        void Bind(TextBox input,Action<ChecklistEntry,string> assign)=>input.TextChanged+=(_,_)=>
        {
            if(loading||selected is null)return;
            assign(selected,input.Text);if(input==name&&tree.SelectedNode is not null)tree.SelectedNode.Text=input.Text;
            if(input==icon||input==target||input==name)iconPreview.Image=EntryImage(selected);
            tree.Invalidate();UpdateCounts();
        };
        Bind(name,(entry,value)=>entry.Title=value);Bind(detail,(entry,value)=>entry.Detail=value);Bind(target,(entry,value)=>entry.Target=value);
        Bind(buttonText,(entry,value)=>entry.Button=value);Bind(icon,(entry,value)=>{entry.IconPath=iconSelection.SelectedPath(value);entry.IconName=iconSelection.SelectedName(value);entry.IconIndex=iconSelection.SelectedIndex(value);});Bind(arguments,(entry,value)=>entry.Arguments=value);Bind(workingDirectory,(entry,value)=>entry.WorkingDirectory=value);
        tree.AfterSelect+=(_,_)=>{if(!loading)LoadEntry();};
        category.SelectedIndexChanged+=(_,_)=>
        {
            if(loading||selected is null||isCategory)return;
            selected.Group=category.SelectedValue.Length==0?null:category.SelectedValue;NormalizeGroups(draft);RefreshTree(selected.Id);
        };
        kind.SelectedIndexChanged+=(_,_)=>
        {
            LaunchLayout();if(loading)return;
            target.Enabled=selected is not null&&kind.SelectedIndex!=0;browseTarget.Enabled=target.Enabled;
            if(kind.SelectedIndex==0)target.Text="";
            target.PlaceholderText=kind.SelectedIndex==3?"https://example.com":kind.SelectedIndex==2?"Choose a folder":"Choose a program or file";
        };
        browseTarget.Click+=(_,_)=>
        {
            if(kind.SelectedIndex==2)
            {using var picker=CreateTargetFolderPicker(target.Text);if(picker.ShowDialog(dialog)==DialogResult.OK)target.Text=picker.SelectedPath;}
            else
            {using var picker=PickerLocations.Program(profile.DirectoryPath,target.Text);if(picker.ShowDialog(dialog)==DialogResult.OK)target.Text=picker.FileName;}
        };
        addTask.Click+=(_,_)=>
        {
            string? group=selected is null?null:isCategory?selected.Id:selected.Group;
            var item=new ChecklistEntry {Id=Guid.NewGuid().ToString("N"),Title="New task",Group=group};
            int last=string.IsNullOrEmpty(group)?-1:draft.Items.FindLastIndex(x=>string.Equals(x.Group,group,StringComparison.OrdinalIgnoreCase));
            if(last>=0)draft.Items.Insert(last+1,item);else draft.Items.Add(item);
            RefreshTree(item.Id);name.Focus();name.SelectAll();
        };
        addCategory.Click+=(_,_)=>
        {
            var group=new ChecklistEntry {Id=Guid.NewGuid().ToString("N"),Title="New group"};draft.Groups.Add(group);RefreshTree(group.Id);name.Focus();name.SelectAll();
        };
        remove.Click+=(_,_)=>
        {
            if(selected is null)return;
            if(isCategory)
            {
                if(draft.Items.Any(x=>string.Equals(x.Group,selected.Id,StringComparison.OrdinalIgnoreCase)))
                {
                    if(MessageBox.Show(dialog,"Remove this group? Its tasks will stay in the checklist as individual tasks.","Remove group",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                    foreach(var item in draft.Items.Where(x=>string.Equals(x.Group,selected.Id,StringComparison.OrdinalIgnoreCase)))item.Group=null;
                }
                draft.Groups.Remove(selected);
            }
            else draft.Items.Remove(selected);
            RefreshTree();
        };
        void Move(int direction)
        {
            if(selected is null)return;
            string id=selected.Id;
            if(isCategory)MoveRoot(draft,id,direction);
            else MoveTask(draft,draft.Items.IndexOf(selected),direction);
            RefreshTree(id);
        }
        up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);
        tree.KeyDown+=(_,e)=>
        {
            if(e.KeyCode==Keys.Delete&&remove.Enabled){remove.PerformClick();e.Handled=true;}
            else if(e.Alt&&e.KeyCode==Keys.Up&&up.Enabled){Move(-1);e.Handled=true;}
            else if(e.Alt&&e.KeyCode==Keys.Down&&down.Enabled){Move(1);e.Handled=true;}
        };
        bool SaveChecklist()
        {
            try
            {
                string? selectedId=selected?.Id;NormalizeGroups(draft);profile.SaveSettings(draft);
                draft=profile.CopySettings();RefreshTree(selectedId);ReloadChecklist();return true;
            }
            catch(Exception error){if(options.TestDirectory is not null)throw;MessageBox.Show(dialog,error.Message,"Could not save checklist",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}
        }
        tree.DrawNode+=(_,e)=>
        {
            if(e.Node?.Tag is not EditorNode node)return;
            var graphics=e.Graphics;graphics.SmoothingMode=SmoothingMode.AntiAlias;
            bool active=tree.SelectedNode==e.Node;
            var bounds=new Rectangle(4,e.Bounds.Top+2,tree.ClientSize.Width-8,tree.ItemHeight-4);
            using(var back=new SolidBrush(tree.BackColor))graphics.FillRectangle(back,new Rectangle(0,e.Bounds.Top,tree.ClientSize.Width,tree.ItemHeight));
            if(active)
            {
                using var shape=WindowStyle.Round(bounds,6);using var fill=new SolidBrush(dark?ColorTranslator.FromHtml("#263B50"):ColorTranslator.FromHtml("#E3EFFB"));graphics.FillPath(fill,shape);
                using var marker=new SolidBrush(Accent);graphics.FillRectangle(marker,5,e.Bounds.Top+15,3,26);
            }
            int left=14+e.Node.Level*20,top=e.Bounds.Top;
            var image=EntryImage(node.Entry);
            float ratio=Math.Min(24f/image.Width,28f/image.Height);
            int imageWidth=Math.Max(1,(int)(image.Width*ratio)),imageHeight=Math.Max(1,(int)(image.Height*ratio));
            graphics.DrawImage(image,new Rectangle(left+(24-imageWidth)/2,top+(tree.ItemHeight-imageHeight)/2,imageWidth,imageHeight));
            int textX=left+34,width=tree.ClientSize.Width-textX-12;
            TextRenderer.DrawText(graphics,node.Entry.Title.Length==0?"Untitled":node.Entry.Title,node.IsCategory?title:body,new Rectangle(textX,top+8,width,23),TextColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            string caption=node.IsCategory?$"{e.Node.Nodes.Count} tasks":string.IsNullOrWhiteSpace(node.Entry.Target)?"Checklist task":node.Entry.Button??"Open tool";
            TextRenderer.DrawText(graphics,caption,small,new Rectangle(textX,top+31,width,19),Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(active&&tree.KeyboardFocusVisible)ControlPaint.DrawFocusRectangle(graphics,Rectangle.Inflate(bounds,-3,-3),TextColor,tree.BackColor);
        };
        RefreshTree();ApplyTheme(dialog);
        edits=new EditSession(dialog,save,"Save changes",()=>System.Text.Json.JsonSerializer.Serialize(draft,Profile.Json),SaveChecklist,
            ()=>draft.Items.Count>0&&draft.Items.Concat(draft.Groups).All(x=>!string.IsNullOrWhiteSpace(x.Title)),()=>ConfirmUnsaved(dialog),requireApply:profile.NeedsAssetMigration,refreshStyle:()=>StyleSaveButton(save));
        StyleSaveButton(save);
        dialog.HandleCreated+=(_,_)=>WindowStyle.Apply(dialog,dark);
        return dialog;
    }
    sealed record EditorNode(ChecklistEntry Entry,bool IsCategory);
}

internal sealed class ChecklistTree:TreeView
{
    internal bool KeyboardFocusVisible=>Focused&&ShowFocusCues;
}

internal sealed class EditorPicker:RoundedButton
{
    readonly List<(string Label,string Value)> choices=[];
    readonly ContextMenuStrip menu=new() {ShowCheckMargin=true,ShowImageMargin=false};
    int selection=-1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ToolStripRenderer? PickerRenderer {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get=>selection;
        set
        {
            if(value < -1 || value >= choices.Count)throw new ArgumentOutOfRangeException(nameof(value));
            if(selection==value)return;
            selection=value;Text=value<0?"":choices[value].Label;
            for(int i=0;i<menu.Items.Count;i++)if(menu.Items[i] is ToolStripMenuItem item)item.Checked=i==value;
            SelectedIndexChanged?.Invoke(this,EventArgs.Empty);Invalidate();
        }
    }
    internal string SelectedValue=>selection<0?"":choices[selection].Value;
    internal int ChoiceCount=>choices.Count;
    internal event EventHandler? SelectedIndexChanged;
    public EditorPicker(){TextAlign=ContentAlignment.MiddleLeft;Padding=new Padding(10,0,25,0);}
    internal void ClearChoices(){selection=-1;choices.Clear();Text="";}
    internal void AddChoice(string label,string value)=>choices.Add((label,value));
    internal string ChoiceValue(int index)=>choices[index].Value;
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);menu.Items.Clear();menu.BackColor=BackColor;menu.ForeColor=ForeColor;
        if(PickerRenderer is not null)menu.Renderer=PickerRenderer;
        menu.Font=Font;
        for(int i=0;i<choices.Count;i++)
        {
            int index=i;
            var item=new ToolStripMenuItem(choices[i].Label){Checked=i==selection,BackColor=BackColor,ForeColor=ForeColor};
            item.Click+=(_,_)=>SelectedIndex=index;menu.Items.Add(item);
        }
        ContextMenuStrip=menu;menu.Show(this,new Point(0,Height));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Alt&&e.KeyCode==Keys.Down){PerformClick();e.Handled=true;}
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var pen=new Pen(Enabled?ForeColor:DisabledTextColor,1.3f);
        int x=Width-18,y=Height/2-2;e.Graphics.DrawLines(pen,[new Point(x,y),new Point(x+4,y+4),new Point(x+8,y)]);
    }
    protected override void Dispose(bool disposing){if(disposing)menu.Dispose();base.Dispose(disposing);}
}
