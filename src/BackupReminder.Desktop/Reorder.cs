using System.Windows.Input;
using System.Windows.Media;
using Button=System.Windows.Controls.Button;
using ListBox=System.Windows.Controls.ListBox;
using ListBoxItem=System.Windows.Controls.ListBoxItem;
using ScrollViewer=System.Windows.Controls.ScrollViewer;
using Panel=System.Windows.Controls.Panel;
using ItemsControl=System.Windows.Controls.ItemsControl;
using DependencyObject=System.Windows.DependencyObject;
using Point=System.Windows.Point;
using UIElement=System.Windows.UIElement;
using LogicalTreeHelper=System.Windows.LogicalTreeHelper;
using SystemParameters=System.Windows.SystemParameters;
namespace BackupReminder;
internal sealed partial class ReviewWindow{
    static Button EditorGrip(DependencyObject source,ListBox list)
    {
        for(var element=source;element!=null&&element!=list;element=element is Visual?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element))
        {
            var button=element as Button;
            if(button!=null&&Equals(button.Tag,"editor-grip"))return button;
        }
        return null;
    }

    static void EnableEditorReorder(ListBox list,Action<int,int> move)
    {
        ScrollViewer.SetCanContentScroll(list,false);
        list.CommandBindings.Add(new CommandBinding(ComponentCommands.MoveUp,(s,e)=>MoveEditorSelection(list,move,-1)));
        list.CommandBindings.Add(new CommandBinding(ComponentCommands.MoveDown,(s,e)=>MoveEditorSelection(list,move,1)));
        list.PreviewKeyDown+=(s,e)=>{
            if(Keyboard.Modifiers!=ModifierKeys.Control||(e.Key!=Key.Up&&e.Key!=Key.Down))return;
            var grip=EditorGrip(e.OriginalSource as DependencyObject,list);
            if(grip==null)return;
            list.SelectedItem=System.Windows.Controls.ItemsControl.ContainerFromElement(list,grip);
            (e.Key==Key.Up?ComponentCommands.MoveUp:ComponentCommands.MoveDown).Execute(null,list);
            e.Handled=true;
        };

        ListBoxItem dragged=null;
        Point start=new Point();
        int from=-1,to=-1;
        var shifted=new List<ListBoxItem>();
        bool moving=false;
        ScrollViewer scroll=null;
        double startOffset=0;
        var autoScroll=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        Action clear=()=>{
            autoScroll.Stop();
            foreach(var row in shifted)row.ClearValue(UIElement.RenderTransformProperty);
            shifted.Clear();
            if(dragged!=null)dragged.ClearValue(Panel.ZIndexProperty);
            dragged=null;
            moving=false;
            if(list.IsMouseCaptured)list.ReleaseMouseCapture();
        };
        list.PreviewMouseLeftButtonDown+=(s,e)=>{
            var grip=EditorGrip(e.OriginalSource as DependencyObject,list);
            if(grip==null)return;
            dragged=ItemsControl.ContainerFromElement(list,grip) as ListBoxItem;
            if(dragged==null)return;
            from=to=list.ItemContainerGenerator.IndexFromContainer(dragged);
            start=e.GetPosition(list);
            scroll=Descendants<ScrollViewer>(list).FirstOrDefault();
            startOffset=scroll==null?0:scroll.VerticalOffset;
            grip.Focus();
            if(!list.CaptureMouse()){dragged=null;return;}
            e.Handled=true;
        };
        Action<double> preview=y=>{
            if(dragged==null||!list.IsMouseCaptured)return;
            double delta=y-start.Y+(scroll==null?0:scroll.VerticalOffset-startOffset);
            if(!moving&&Math.Abs(delta)<SystemParameters.MinimumVerticalDragDistance)return;
            moving=true;
            foreach(var row in shifted)row.ClearValue(UIElement.RenderTransformProperty);
            shifted.Clear();
            double center=dragged.TranslatePoint(new Point(0,dragged.ActualHeight/2),list).Y+delta;
            to=from;
            for(int index=0;index<list.Items.Count;index++)
            {
                var row=list.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                if(row==null||row==dragged)continue;
                double midpoint=row.TranslatePoint(new Point(0,row.ActualHeight/2),list).Y;
                if((index<from&&center<midpoint)||(index>from&&center>midpoint))
                {
                    to=index<from?Math.Min(to,index):Math.Max(to,index);
                    row.RenderTransform=new TranslateTransform(0,(index<from?1:-1)*(dragged.ActualHeight+dragged.Margin.Top+dragged.Margin.Bottom));
                    shifted.Add(row);
                }
            }
            Panel.SetZIndex(dragged,1);
            dragged.RenderTransform=new TranslateTransform(0,delta);
            shifted.Add(dragged);
            if(scroll!=null&&!autoScroll.IsEnabled)autoScroll.Start();
        };
        list.PreviewMouseMove+=(s,e)=>{
            if(dragged==null)return;
            preview(e.GetPosition(list).Y);
            e.Handled=true;
        };
        autoScroll.Tick+=(s,e)=>{
            var position=Mouse.GetPosition(scroll);
            if(position.X<0||position.X>scroll.ActualWidth)return;
            double offset=scroll.VerticalOffset;
            if(position.Y<20)scroll.ScrollToVerticalOffset(offset-16);
            else if(position.Y>scroll.ActualHeight-20)scroll.ScrollToVerticalOffset(offset+16);
            else return;
            scroll.UpdateLayout();
            preview(Mouse.GetPosition(list).Y);
        };
        list.PreviewMouseLeftButtonUp+=(s,e)=>{
            if(dragged==null)return;
            var item=list.Items[from];
            bool apply=moving&&to!=from;
            clear();
            if(apply)move(from,to);
            if(list.Items.Contains(item))list.SelectedItem=item;
            e.Handled=true;
        };
        list.LostMouseCapture+=(s,e)=>{if(dragged!=null&&!list.IsMouseCaptured)clear();};
        list.PreviewKeyDown+=(s,e)=>{
            if(e.Key!=Key.Escape||dragged==null)return;
            clear();
            e.Handled=true;
        };
    }

    static void MoveEditorSelection(ListBox list,Action<int,int> move,int offset)
    {
        int from=list.SelectedIndex,to=from+offset;
        if(from<0||to<0||to>=list.Items.Count)return;
        var item=list.SelectedItem;
        move(from,to);
        if(list.Items.Contains(item))list.SelectedItem=item;
        list.ScrollIntoView(list.SelectedItem);
        list.UpdateLayout();
        var row=list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) as ListBoxItem;
        if(row!=null)
        {
            var grip=Descendants<Button>(row).FirstOrDefault(button=>Equals(button.Tag,"editor-grip"));
            if(grip!=null)grip.Focus();
        }
    }
}
