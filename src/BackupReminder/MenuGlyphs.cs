using System.Drawing.Drawing2D;

namespace BackupReminder;

// Original line artwork, rendered locally in the current menu text color.
internal static class MenuGlyphs
{
    internal static Bitmap Create(string glyph,Color color)
    {
        var image=new Bitmap(32,32);
        using var graphics=Graphics.FromImage(image);graphics.ScaleTransform(2,2);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(color,1.25f) {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        using var fill=new SolidBrush(color);
        void Line(float x1,float y1,float x2,float y2)=>graphics.DrawLine(pen,x1,y1,x2,y2);
        void Circle()=>graphics.DrawEllipse(pen,1.75f,1.75f,12.5f,12.5f);
        switch(glyph)
        {
            case "checklist":
                using(var border=WindowStyle.Round(new RectangleF(2,1.75f,12,12.5f),1.5f))graphics.DrawPath(pen,border);
                graphics.DrawLines(pen,[new PointF(4,5),new PointF(5,6),new PointF(7,4)]);Line(9,5,12,5);
                graphics.DrawLines(pen,[new PointF(4,10),new PointF(5,11),new PointF(7,9)]);Line(9,10,12,10);break;
            case "exit":
                graphics.DrawLines(pen,[new PointF(7,2),new PointF(2,2),new PointF(2,14),new PointF(7,14)]);
                Line(6,8,14,8);graphics.DrawLines(pen,[new PointF(11,5),new PointF(14,8),new PointF(11,11)]);break;
            case "clock":
                Circle();graphics.DrawLines(pen,[new PointF(8,4),new PointF(8,8),new PointF(11,10)]);break;
            case "appearance":
                Circle();graphics.FillPie(fill,3.5f,3.5f,9,9,270,180);Line(8,3.5f,8,12.5f);break;
            case "backup":
            case "restore":
                graphics.DrawLines(pen,[new PointF(2,6),new PointF(2,14),new PointF(14,14),new PointF(14,6)]);
                Line(8,2,8,10);
                graphics.DrawLines(pen,glyph=="backup"?[new PointF(5,7),new PointF(8,10),new PointF(11,7)]:[new PointF(5,5),new PointF(8,2),new PointF(11,5)]);break;
            case "folder":
                graphics.DrawLines(pen,[new PointF(2,5),new PointF(2,3),new PointF(6,3),new PointF(8,5),new PointF(14,5),new PointF(14,13),new PointF(2,13),new PointF(2,5),new PointF(14,5)]);break;
            case "help":
                Circle();graphics.DrawArc(pen,5.5f,4,5,4,180,250);Line(8,8,8,9);graphics.FillEllipse(fill,7.4f,11,1.2f,1.2f);break;
            case "report":
                graphics.DrawPolygon(pen,[new PointF(2,2),new PointF(14,2),new PointF(14,11),new PointF(7,11),new PointF(3,14),new PointF(3,11),new PointF(2,11)]);
                Line(8,4.5f,8,7);graphics.FillEllipse(fill,7.4f,8.5f,1.2f,1.2f);break;
            case "about":
                Circle();graphics.FillEllipse(fill,7.35f,4,1.3f,1.3f);Line(8,7,8,11.5f);break;
            default:throw new ArgumentException("Unknown menu glyph",nameof(glyph));
        }
        return image;
    }
}
