using System.Drawing.Drawing2D;

namespace BackupReminder;

// Original line artwork; shared command shapes match BroadcastForge.
internal static class MenuGlyphs
{
    internal static Bitmap Create(string glyph,Color color)
    {
        var image=new Bitmap(64,64);
        using var graphics=Graphics.FromImage(image);graphics.ScaleTransform(4,4);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(color,1.35f) {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        using var fill=new SolidBrush(color);
        void Line(float x1,float y1,float x2,float y2)=>graphics.DrawLine(pen,x1,y1,x2,y2);
        void Lines(params PointF[] points)=>graphics.DrawLines(pen,points);
        void Circle()=>graphics.DrawEllipse(pen,1,1,14,14);
        switch(glyph)
        {
            case "checklist":
                using(var border=WindowStyle.Round(new RectangleF(2,1,12,14),1.5f))graphics.DrawPath(pen,border);
                Lines(new(4,5),new(5,6),new(7,4));Line(10,5,12,5);
                Lines(new(4,11),new(5,12),new(7,10));Line(10,11,12,11);break;
            case "exit":
                Lines(new(6,1),new(1,1),new(1,15),new(6,15));
                Line(6,8,15,8);Lines(new(11,4),new(15,8),new(11,12));break;
            case "clock":
                Circle();Lines(new(8,4),new(8,8),new(11,10));break;
            case "appearance":
                Circle();graphics.FillPie(fill,1,1,14,14,270,180);break;
            case "backup":
            case "restore":
                Lines(new(2,9),new(2,15),new(14,15),new(14,9));Line(8,1,8,11);
                if(glyph=="backup")Lines(new(4,7),new(8,11),new(12,7));
                else Lines(new(4,5),new(8,1),new(12,5));break;
            case "folder":
                Lines(new(1,4),new(1,2),new(6,2),new(8,4),new(15,4),new(15,14),new(1,14),new(1,4));break;
            case "help":
                Circle();graphics.DrawBezier(pen,new(5,5),new(5,1),new(13,3),new(9,7));Line(9,7,8,9);Line(8,12,8,12.2f);break;
            case "report":
                Lines(new(1,2),new(15,2),new(15,11),new(7,11),new(3,15),new(3,11),new(1,11),new(1,2));
                Line(8,5,8,7);Line(8,9,8,9.2f);break;
            case "about":
                Circle();Line(8,7,8,12);Line(8,4,8,4.2f);break;
            default:throw new ArgumentException("Unknown menu glyph",nameof(glyph));
        }
        return image;
    }
}
