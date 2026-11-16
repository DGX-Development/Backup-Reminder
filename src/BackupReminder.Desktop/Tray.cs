using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace BackupReminder;
internal sealed class DashboardTrayMenu:ContextMenuStrip {
    protected override void OnSizeChanged(EventArgs e){
        base.OnSizeChanged(e);
        var previous=Region;
        if(Width>0&&Height>0){
            float radius=Math.Min(10f*DeviceDpi/96f,Math.Min(Width,Height)/2f);
            using(var shape=DashboardMenuRenderer.Round(new RectangleF(0,0,Width,Height),radius))Region=new Region(shape);
        }else Region=null;
        if(previous!=null)previous.Dispose();
    }
}
internal sealed class DashboardMenuRenderer:ToolStripProfessionalRenderer {
    static readonly Dictionary<string,Bitmap> glyphs=new Dictionary<string,Bitmap>();
    readonly Func<string,Color> palette;
    internal DashboardMenuRenderer(Func<string,Color> palette=null){this.palette=palette;}
    Color MenuColor(string key,Color fallback){return palette==null?fallback:palette(key);}
    internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(e.ToolStrip.BackColor);}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){
        if(!(e.ToolStrip is ToolStripDropDown))return;
        using(var pen=new Pen(MenuColor("454545",Color.FromArgb(69,69,69)))){
            var bounds=new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
            if(e.ToolStrip is DashboardTrayMenu){
                float radius=Math.Min(10f*e.ToolStrip.DeviceDpi/96f,Math.Min(bounds.Width,bounds.Height)/2f);
                if(radius>0)using(var shape=Round(bounds,radius))e.Graphics.DrawPath(pen,shape);
            }else e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
        }
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Selected||e.Item.Pressed){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(SystemInformation.HighContrast?SystemColors.Highlight:MenuColor("383838",Color.FromArgb(56,56,56))))using(var shape=Round(new RectangleF(2,1,e.Item.Width-4,e.Item.Height-2),6))e.Graphics.FillPath(brush,shape);}}
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(MenuColor("454545",Color.FromArgb(69,69,69))))e.Graphics.DrawLine(pen,10,e.Item.Height/2,e.Item.Width-10,e.Item.Height/2);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=SystemInformation.HighContrast&&e.Item.Selected?SystemColors.HighlightText:MenuColor("F2F2F2",Color.FromArgb(242,242,242));if(e.ToolStrip is ToolStripDropDown){var menu=e.ToolStrip as ContextMenuStrip;int inset=menu!=null&&!menu.ShowImageMargin?12:46;e.TextRectangle=new Rectangle(inset,0,Math.Max(1,e.Item.Width-inset-12),e.Item.Height);}else e.TextRectangle=new Rectangle(e.TextRectangle.X,0,e.TextRectangle.Width,e.Item.Height);e.TextFormat|=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine;base.OnRenderItemText(e);}
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){float y=e.Item.Height/2f;using(var pen=new Pen(SystemInformation.HighContrast?(e.Item.Selected?SystemColors.HighlightText:SystemColors.MenuText):Color.FromArgb(120,183,255),1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawLines(pen,new[]{new PointF(16,y),new PointF(20,y+4),new PointF(27,y-4)});}}
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){if(e.Image==null||(e.Item is ToolStripMenuItem&&((ToolStripMenuItem)e.Item).Checked))return;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(e.Image,new Rectangle(14,(e.Item.Height-16)/2,16,16));}
    internal static Bitmap ExitGlyph(){return Glyph("exit");}
    internal static Bitmap Glyph(string kind){if(glyphs.ContainsKey(kind))return new Bitmap(glyphs[kind]);var image=new Bitmap(64,64);using(var g=Graphics.FromImage(image))using(var pen=new Pen(Color.FromArgb(210,210,210),1.35f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){g.ScaleTransform(4,4);g.SmoothingMode=SmoothingMode.AntiAlias;switch(kind){
        case "exit":g.DrawLines(pen,new[]{new PointF(6,1),new PointF(1,1),new PointF(1,15),new PointF(6,15)});g.DrawLine(pen,6,8,15,8);g.DrawLines(pen,new[]{new PointF(11,4),new PointF(15,8),new PointF(11,12)});break;
        case "settings":
            var gear=new[]{new PointF(6,1),new PointF(10,1),new PointF(10,3),new PointF(12,4),
                new PointF(14,3),new PointF(16,6),new PointF(14,8),new PointF(14,10),
                new PointF(16,12),new PointF(14,15),new PointF(12,14),new PointF(10,15),
                new PointF(10,17),new PointF(6,17),new PointF(6,15),new PointF(4,14),
                new PointF(2,15),new PointF(0,12),new PointF(2,10),new PointF(2,8),
                new PointF(0,6),new PointF(2,3),new PointF(4,4),new PointF(6,3)};
            for(int i=0;i<gear.Length;i++)gear[i]=new PointF(1+gear[i].X*.875f,.125f+gear[i].Y*.875f);
            g.DrawPolygon(pen,gear);
            g.DrawEllipse(pen,5.375f,5.375f,5.25f,5.25f);
            break;
        case "hide":
        case "restore":
            g.DrawLines(pen,new[]{new PointF(2,9),new PointF(2,15),new PointF(14,15),new PointF(14,9)});
            g.DrawLine(pen,8,1,8,11);
            g.DrawLines(pen,kind=="hide"
                ?new[]{new PointF(4,7),new PointF(8,11),new PointF(12,7)}
                :new[]{new PointF(4,5),new PointF(8,1),new PointF(12,5)});
            break;
        case "folder":g.DrawLines(pen,new[]{new PointF(1,4),new PointF(1,2),new PointF(6,2),new PointF(8,4),new PointF(15,4),new PointF(15,14),new PointF(1,14),new PointF(1,4)});break;
        case "dashboard":g.DrawRectangle(pen,1,2,14,12);g.DrawLine(pen,1,6,15,6);g.DrawLine(pen,7,6,7,14);g.DrawLine(pen,7,10,15,10);break;
        case "checklist":g.DrawRectangle(pen,2,1,12,14);g.DrawLines(pen,new[]{new PointF(4,5),new PointF(5,6),new PointF(7,4)});g.DrawLine(pen,10,5,12,5);g.DrawLines(pen,new[]{new PointF(4,11),new PointF(5,12),new PointF(7,10)});g.DrawLine(pen,10,11,12,11);break;
        case "reports":g.DrawLines(pen,new[]{new PointF(1,1),new PointF(1,15),new PointF(15,15)});g.DrawLine(pen,5,11,5,8);g.DrawLine(pen,9,11,9,3);g.DrawLine(pen,13,11,13,6);break;
        case "help":g.DrawEllipse(pen,1,1,14,14);g.DrawBezier(pen,new PointF(5,5),new PointF(5,1),new PointF(13,3),new PointF(9,7));g.DrawLine(pen,9,7,8,9);g.DrawLine(pen,8,12,8,12.2f);break;
        case "info":g.DrawEllipse(pen,1,1,14,14);g.DrawLine(pen,8,7,8,12);g.DrawLine(pen,8,4,8,4.2f);break;
        case "categories":foreach(var p in new[]{new Point(1,1),new Point(10,1),new Point(1,10),new Point(10,10)})g.DrawRectangle(pen,p.X,p.Y,5,5);break;
        case "dev-guide":
            pen.Color=Color.FromArgb(242,242,242);
            g.DrawLines(pen,new[]{new PointF(8,3),new PointF(2,2),new PointF(2,13),new PointF(8,14),new PointF(14,13),new PointF(14,2),new PointF(8,3),new PointF(8,14)});
            g.DrawLine(pen,4,6,6,6);
            g.DrawLine(pen,10,6,12,6);
            g.DrawLine(pen,4,9,6,9);
            g.DrawLine(pen,10,9,12,9);
            break;
        case "copy":g.DrawRectangle(pen,5,1,9,11);g.DrawLines(pen,new[]{new PointF(2,4),new PointF(2,15),new PointF(11,15)});break;
        case "globe":
            g.DrawEllipse(pen,1,1,14,14);
            g.DrawEllipse(pen,5,1,6,14);
            g.DrawLine(pen,1,8,15,8);
            break;
        case "workflow":
            g.DrawRectangle(pen,5,1,6,4);
            g.DrawLine(pen,8,5,8,8);
            g.DrawLine(pen,4,8,12,8);
            g.DrawLine(pen,4,8,4,11);
            g.DrawLine(pen,12,8,12,11);
            g.DrawRectangle(pen,1,11,6,4);
            g.DrawRectangle(pen,9,11,6,4);
            break;
        case "switch":g.DrawLines(pen,new[]{new PointF(2,4),new PointF(14,4),new PointF(11,1)});g.DrawLine(pen,14,4,11,7);g.DrawLines(pen,new[]{new PointF(14,12),new PointF(2,12),new PointF(5,9)});g.DrawLine(pen,2,12,5,15);break;
        case "schedule":g.DrawRectangle(pen,2,3,12,12);g.DrawLine(pen,2,6,14,6);g.DrawLine(pen,5,1,5,5);g.DrawLine(pen,11,1,11,5);g.DrawLine(pen,5,9,7,9);g.DrawLine(pen,9,9,11,9);break;
        case "updates":g.DrawArc(pen,2,1,12,12,200,270);g.DrawLines(pen,new[]{new PointF(1,1),new PointF(1,5),new PointF(5,5)});break;
        case "pin":g.DrawLines(pen,new[]{new PointF(5,1),new PointF(11,1),new PointF(10,7),new PointF(13,10),new PointF(3,10),new PointF(6,7),new PointF(5,1)});g.DrawLine(pen,8,10,8,15);break;
        default:g.DrawEllipse(pen,1,1,14,14);if(kind=="recording")g.DrawPolygon(pen,new[]{new PointF(6,3),new PointF(13,8),new PointF(6,13)});else{g.DrawLine(pen,8,7,8,12);g.DrawLine(pen,8,4,8,4.2f);}break;
    }}glyphs[kind]=image;return new Bitmap(image);}
}




