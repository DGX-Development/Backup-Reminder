using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BackupReminder;

internal static class WindowStyle
{
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowEnabled(nint window);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(nint window,int attribute,ref int value,int size);
    internal static void Apply(Form form,bool dark)
    {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)||!form.IsHandleCreated)return;
        int theme=dark?1:0,corner=2;
        _=DwmSetWindowAttribute(form.Handle,20,ref theme,sizeof(int));
        _=DwmSetWindowAttribute(form.Handle,33,ref corner,sizeof(int));
    }
    internal static GraphicsPath Round(RectangleF bounds,float radius)
    {
        var path=new GraphicsPath();float diameter=radius*2;
        path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
        path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
        path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();return path;
    }
}
// Use explicit high-quality resampling for the brand artwork at every DPI.
internal sealed class BrandPictureBox:PictureBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Image is null)return;
        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality=CompositingQuality.HighQuality;
        float scale=Math.Min((float)ClientSize.Width/Image.Width,(float)ClientSize.Height/Image.Height);
        var size=new SizeF(Image.Width*scale,Image.Height*scale);
        e.Graphics.DrawImage(Image,new RectangleF((ClientSize.Width-size.Width)/2,(ClientSize.Height-size.Height)/2,size.Width,size.Height));
    }
}
internal sealed class RoundedPanel:Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color BorderColor {get;set;}=Color.FromArgb(225,228,233);
    public RoundedPanel(){DoubleBuffered=true;ResizeRedraw=true;}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??BackColor);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=WindowStyle.Round(new RectangleF(.5f,.5f,Width-1,Height-1),8*DeviceDpi/96f);
        using var fill=new SolidBrush(BackColor);e.Graphics.FillPath(fill,shape);
        using var line=new Pen(BorderColor);e.Graphics.DrawPath(line,shape);
    }
}
internal class RoundedButton:Button
{
    bool hovering,pressed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color BorderColor {get;set;}=Color.FromArgb(210,215,222);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color DisabledTextColor {get;set;}=Color.Gray;
    public RoundedButton(){FlatStyle=FlatStyle.Flat;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovering=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??SystemColors.Control);
        Color fillColor=SystemInformation.HighContrast?SystemColors.Control:BackColor;
        Color textColor=Enabled?ForeColor:DisabledTextColor;
        if(Enabled&&hovering)fillColor=pressed?ControlPaint.Dark(fillColor,.08f):ControlPaint.Light(fillColor,.12f);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=WindowStyle.Round(new RectangleF(.5f,.5f,Width-1,Height-1),4*DeviceDpi/96f);
        using var brush=new SolidBrush(fillColor);e.Graphics.FillPath(brush,shape);
        using var line=new Pen(SystemInformation.HighContrast?SystemColors.WindowText:BorderColor);e.Graphics.DrawPath(line,shape);
        var textBounds=Rectangle.FromLTRB(Padding.Left,Padding.Top,Width-Padding.Right,Height-Padding.Bottom);
        var alignment=TextAlign is ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,textColor,alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),textColor,fillColor);
    }
}

// A dedicated gutter keeps the scroll thumb outside the editable fields.
internal sealed class EditorScrollArea:Panel
{
    readonly Panel viewport=new() {Dock=DockStyle.Fill};
    readonly EditorScrollThumb scroll=new() {Dock=DockStyle.Right,Width=18};
    Control? content;bool arranging;int offset;
    internal int ScrollOffset=>offset;
    internal int MaximumOffset=>Math.Max(0,(content?.Height??0)-viewport.ClientSize.Height);
    internal Rectangle ContentViewport=>viewport.Bounds;
    internal Rectangle ScrollGutter=>scroll.Bounds;
    public EditorScrollArea()
    {
        Controls.Add(viewport);Controls.Add(scroll);
        scroll.RequestScroll+=value=>SetOffset(value);
        viewport.SizeChanged+=(_,_)=>ArrangeContent();
    }
    internal void SetContent(Control value)
    {
        content=value;value.Dock=DockStyle.None;viewport.Controls.Add(value);
        value.SizeChanged+=(_,_)=>ArrangeContent();HookFocus(value);ArrangeContent();
    }
    void HookFocus(Control control)
    {
        if(control is not Panel)control.Enter+=(_,_)=>ScrollControlIntoView(control);
        control.ControlAdded+=(_,e)=>{if(e.Control is not null)HookFocus(e.Control);};
        foreach(Control child in control.Controls)HookFocus(child);
    }
    internal new void ScrollControlIntoView(Control control)
    {
        if(content is null||!control.Visible)return;
        int y=0;Control? current=control;
        while(current is not null&&current!=content){y+=current.Top;current=current.Parent;}
        if(current!=content)return;
        if(y<offset)SetOffset(y);
        else if(y+control.Height>offset+viewport.Height)SetOffset(y+control.Height-viewport.Height);
    }
    void ArrangeContent()
    {
        if(arranging||content is null)return;arranging=true;
        try
        {
            // Always reserve a gutter, so expanding More options never shifts buttons under it.
            content.Width=Math.Max(1,viewport.ClientSize.Width-12*DeviceDpi/96);
            SetOffset(offset);scroll.Enabled=MaximumOffset>0;scroll.Invalidate();
        }
        finally{arranging=false;}
    }
    internal void SetOffset(int value)
    {
        offset=Math.Clamp(value,0,MaximumOffset);
        if(content is not null)content.Location=new Point(0,-offset);
        scroll.SetRange(offset,MaximumOffset,viewport.Height);
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs handled)handled.Handled=true;
        int lines=SystemInformation.MouseWheelScrollLines;
        SetOffset(offset-e.Delta/120*(lines<0?viewport.Height:Math.Max(1,lines)*24*DeviceDpi/96));
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ArrangeContent();}
    internal void ScrollToEnd()=>SetOffset(MaximumOffset);
}
internal sealed class EditorScrollThumb:Control
{
    int value,maximum,page=1,dragY,dragValue;bool dragging,hover;
    internal event Action<int>? RequestScroll;
    internal string PositionText=>value.ToString();
    public EditorScrollThumb()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        TabStop=true;AccessibleRole=AccessibleRole.ScrollBar;AccessibleName="Scroll task details";
    }
    internal void SetRange(int position,int limit,int visible){value=position;maximum=limit;page=Math.Max(1,visible);Invalidate();}
    Rectangle Thumb
    {
        get
        {
            int height=Math.Min(Height,Math.Max(32*DeviceDpi/96,(int)((long)Height*page/Math.Max(1,page+maximum))));
            int top=maximum==0?0:(int)((long)(Height-height)*value/maximum);
            return new Rectangle(5*DeviceDpi/96,top,Math.Max(4,Width-10*DeviceDpi/96),height);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);if(maximum<=0)return;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var thumb=Thumb;using var shape=WindowStyle.Round(thumb,Math.Max(1,thumb.Width/2f));
        using var brush=new SolidBrush(SystemInformation.HighContrast?SystemColors.WindowText:dragging||hover?ForeColor:Color.FromArgb(150,ForeColor));e.Graphics.FillPath(brush,shape);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle,ForeColor,BackColor);
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();
        if(Thumb.Contains(e.Location)){dragging=true;dragY=e.Y;dragValue=value;Capture=true;}
        else RequestScroll?.Invoke(value+(e.Y<Thumb.Top?-page:page));Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {base.OnMouseMove(e);if(dragging)RequestScroll?.Invoke(dragValue+(int)((long)(e.Y-dragY)*maximum/Math.Max(1,Height-Thumb.Height)));}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Invalidate();}}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int? next=e.KeyCode switch{Keys.Up=>value-24,Keys.Down=>value+24,Keys.PageUp=>value-page,Keys.PageDown=>value+page,Keys.Home=>0,Keys.End=>maximum,_=>null};
        if(next is int position){RequestScroll?.Invoke(position);e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);
    }
    protected override AccessibleObject CreateAccessibilityInstance()=>new ScrollAccessibility(this);
    sealed class ScrollAccessibility(EditorScrollThumb owner):ControlAccessibleObject(owner)
    {
        public override string? Value {get=>owner.PositionText;set{if(int.TryParse(value,out int position))owner.RequestScroll?.Invoke(position);}}
    }
}
internal sealed class ThemeNumberInput:Panel
{
    readonly TextBox input=new() {BorderStyle=BorderStyle.None};
    readonly RoundedButton minus=new() {Text="-",AccessibleName="Decrease value",TabStop=false};
    readonly RoundedButton plus=new() {Text="+",AccessibleName="Increase value",TabStop=false};
    readonly int minimum,maximum;int value;
    internal event EventHandler? ValueChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Value {get=>value;set{int previous=this.value;this.value=Math.Clamp(value,minimum,maximum);input.Text=this.value.ToString();UpdateButtons();if(previous!=this.value)ValueChanged?.Invoke(this,EventArgs.Empty);}}
    public ThemeNumberInput(int minimum,int maximum,int initial)
    {
        this.minimum=minimum;this.maximum=maximum;
        Controls.AddRange([input,minus,plus]);Value=initial;
        minus.Click+=(_,_)=>Value--;plus.Click+=(_,_)=>Value++;
        input.TextChanged+=(_,_)=>{if(int.TryParse(input.Text,out int number)&&number>=minimum&&number<=maximum){int previous=value;value=number;UpdateButtons();if(previous!=value)ValueChanged?.Invoke(this,EventArgs.Empty);}};
        input.Leave+=(_,_)=>Value=int.TryParse(input.Text,out int number)?number:value;
        input.KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Up or Keys.Down){Value+=e.KeyCode==Keys.Up?1:-1;e.Handled=true;e.SuppressKeyPress=true;}};
    }
    void UpdateButtons(){minus.Enabled=value>minimum;plus.Enabled=value<maximum;}
    internal void ApplyColors(Color background,Color button,Color text,Color muted,Color accent)
    {
        BackColor=background;input.BackColor=background;input.ForeColor=text;
        foreach(var item in new[]{minus,plus}){item.BackColor=button;item.ForeColor=text;item.DisabledTextColor=muted;item.BorderColor=muted;}
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);int side=30*DeviceDpi/96,gap=6*DeviceDpi/96;
        plus.SetBounds(Width-side,0,side,Height);minus.SetBounds(plus.Left-gap-side,0,side,Height);
        input.SetBounds(0,Math.Max(0,(Height-input.PreferredHeight)/2),Math.Max(20,minus.Left-gap),input.PreferredHeight);
        input.AccessibleName=AccessibleName;
    }
}
internal sealed class CalendarLaunchButton:RoundedButton
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);float scale=DeviceDpi/96f,size=15*scale,x=(Width-size)/2,y=(Height-size)/2;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(ForeColor,1.3f*scale){StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var outline=WindowStyle.Round(new RectangleF(x,y+2*scale,size,13*scale),2*scale);e.Graphics.DrawPath(pen,outline);
        e.Graphics.DrawLine(pen,x,y+6*scale,x+size,y+6*scale);
        foreach(float column in new[]{4f,11f})e.Graphics.DrawLine(pen,x+column*scale,y,x+column*scale,y+4*scale);
        using var fill=new SolidBrush(ForeColor);
        foreach(float row in new[]{8f,11f})foreach(float column in new[]{4f,7f,10f})e.Graphics.FillRectangle(fill,x+column*scale,y+row*scale,scale,scale);
    }
}

// Regional text entry with themed buttons; the date popup uses Windows' calendar.
internal sealed class ScheduleDateTimeInput:Panel
{
    readonly TextBox input=new() {BorderStyle=BorderStyle.None};
    readonly RoundedButton previous=new() {Text="-",AccessibleName="One minute earlier"};
    readonly RoundedButton next=new() {Text="+",AccessibleName="One minute later"};
    readonly RoundedButton calendarButton=new CalendarLaunchButton() {AccessibleName="Choose start date"};
    readonly System.Globalization.CultureInfo culture=System.Globalization.CultureInfo.CurrentCulture;
    readonly bool timeOnly;DateTime value;ToolStripDropDown? calendarPopup;
    internal event EventHandler? ValueChanged;
    internal string DisplayFormat=>timeOnly?culture.DateTimeFormat.ShortTimePattern:culture.DateTimeFormat.ShortDatePattern;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal DateTime Value
    {
        get
        {
            if(!TryRead(out var parsed))throw new InvalidDataException($"Enter a valid {(timeOnly?"time":"start date")}, for example {value.ToString(DisplayFormat,culture)}.");
            return parsed;
        }
        set
        {
            var normalized=timeOnly?DateTime.Today.AddHours(value.Hour).AddMinutes(value.Minute):value.Date;
            bool changed=this.value!=normalized;this.value=normalized;input.Text=normalized.ToString(DisplayFormat,culture);
            if(changed)ValueChanged?.Invoke(this,EventArgs.Empty);
        }
    }
    internal ScheduleDateTimeInput(bool timeOnly,DateTime initial)
    {
        this.timeOnly=timeOnly;Controls.Add(input);
        if(timeOnly){Controls.AddRange([previous,next]);previous.Click+=(_,_)=>Adjust(-1);next.Click+=(_,_)=>Adjust(1);}
        else{Controls.Add(calendarButton);calendarButton.Click+=(_,_)=>OpenCalendar();}
        Value=initial;
        input.TextChanged+=(_,_)=>{if(TryRead(out var parsed))value=parsed;ValueChanged?.Invoke(this,EventArgs.Empty);};
        input.Leave+=(_,_)=>{if(TryRead(out var parsed))Value=parsed;};
        input.KeyDown+=(_,e)=>
        {
            if(timeOnly&&e.KeyCode is Keys.Up or Keys.Down){Adjust(e.KeyCode==Keys.Up?1:-1);e.Handled=true;e.SuppressKeyPress=true;}
            else if(!timeOnly&&e.Alt&&e.KeyCode==Keys.Down){OpenCalendar();e.Handled=true;e.SuppressKeyPress=true;}
        };
    }
    bool TryRead(out DateTime parsed)
    {
        parsed=value;
        if(timeOnly)
        {
            if(!TimeOnly.TryParse(input.Text,culture,System.Globalization.DateTimeStyles.AllowWhiteSpaces,out var time))return false;
            parsed=DateTime.Today.AddHours(time.Hour).AddMinutes(time.Minute);return true;
        }
        if(!DateTime.TryParseExact(input.Text.Trim(),DisplayFormat,culture,System.Globalization.DateTimeStyles.AllowWhiteSpaces,out var date)
            ||date.Date<new DateTime(1753,1,1)||date.Date>new DateTime(9998,12,31))return false;
        parsed=date.Date;return true;
    }
    void Adjust(int minutes)
    {
        if(TryRead(out var parsed))Value=DateTime.Today.Add(TimeOnly.FromDateTime(parsed).AddMinutes(minutes).ToTimeSpan());
    }
    internal MonthCalendar? CurrentCalendar=>calendarPopup?.Items.OfType<ToolStripControlHost>().Select(x=>x.Control).OfType<MonthCalendar>().SingleOrDefault();
    void OpenCalendar()
    {
        if(calendarPopup is not null){calendarPopup.Close();calendarPopup.Dispose();}
        var calendar=new MonthCalendar {MaxSelectionCount=1,MinDate=new DateTime(1753,1,1),MaxDate=new DateTime(9998,12,31)};
        calendar.SetDate(TryRead(out var selected)?selected:value);
        var popup=new ToolStripDropDown {Padding=Padding.Empty};calendarPopup=popup;
        popup.Items.Add(new ToolStripControlHost(calendar){Margin=Padding.Empty,Padding=Padding.Empty});
        calendar.DateChanged+=(_,e)=>Value=e.Start;
        calendar.DateSelected+=(_,_)=>popup.Close();
        popup.Closed+=(_,_)=>input.Focus();popup.Show(this,new Point(0,Height));calendar.Focus();
    }
    internal void ApplyColors(Color background,Color button,Color text,Color muted)
    {
        BackColor=background;input.BackColor=background;input.ForeColor=text;
        foreach(var item in new[]{previous,next,calendarButton}){item.BackColor=button;item.ForeColor=text;item.DisabledTextColor=muted;item.BorderColor=muted;}
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);int side=30*DeviceDpi/96,gap=6*DeviceDpi/96,left;
        if(timeOnly){next.SetBounds(Width-side,0,side,Height);previous.SetBounds(next.Left-gap-side,0,side,Height);left=previous.Left;}
        else{calendarButton.SetBounds(Width-side,0,side,Height);left=calendarButton.Left;}
        input.SetBounds(0,Math.Max(0,(Height-input.PreferredHeight)/2),Math.Max(20,left-gap),input.PreferredHeight);input.AccessibleName=AccessibleName;
    }
    protected override void Dispose(bool disposing){if(disposing)calendarPopup?.Dispose();base.Dispose(disposing);}
}
internal sealed class AppMenuRenderer(ProfessionalColorTable colors,Color accent):ToolStripProfessionalRenderer(colors)
{
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds=e.ImageRectangle;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(SystemInformation.HighContrast?SystemColors.MenuText:accent,2);
        float unit=Math.Max(1,Math.Min(bounds.Width,bounds.Height)/16f);
        float x=bounds.Left+(bounds.Width-11*unit)/2,y=bounds.Top+bounds.Height/2f;
        e.Graphics.DrawLines(pen,[new PointF(x,y),new PointF(x+4*unit,y+4*unit),new PointF(x+11*unit,y-4*unit)]);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var bounds=e.ArrowRectangle;float x=bounds.Left+bounds.Width/2f,y=bounds.Top+bounds.Height/2f;
        float size=Math.Max(2,Math.Min(bounds.Width,bounds.Height)/4f);
        var color=SystemInformation.HighContrast?SystemColors.MenuText:e.Item?.ForeColor??e.ArrowColor;
        using var pen=new Pen(color,1.5f);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        PointF[] points=e.Direction switch
        {
            ArrowDirection.Left=>[new(x+size/2,y-size),new(x-size/2,y),new(x+size/2,y+size)],
            ArrowDirection.Up=>[new(x-size,y+size/2),new(x,y-size/2),new(x+size,y+size/2)],
            ArrowDirection.Down=>[new(x-size,y-size/2),new(x,y+size/2),new(x+size,y-size/2)],
            _=>[new(x-size/2,y-size),new(x+size/2,y),new(x-size/2,y+size)]
        };
        e.Graphics.DrawLines(pen,points);
    }
}
