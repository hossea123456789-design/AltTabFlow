using System.Drawing.Drawing2D;
namespace AltTabFlow;

// Native keyboard semantics, shared GDI drawing. No animation timers or web runtime.
internal static class UiPaint
{
    public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<=0){p.AddRectangle(r);return p;}p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    public static void Box(Graphics g,Rectangle r,Color fill,int radius,Color? border=null){using var path=Round(r,radius);using var brush=new SolidBrush(fill);g.FillPath(brush,path);if(border is Color c){using var pen=new Pen(c);g.DrawPath(pen,path);}}
    public static void Check(Graphics g,Rectangle r,Color color){using var pen=new Pen(color,Math.Max(1.8f,r.Width/8f)){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};g.DrawLines(pen,new[]{new PointF(r.Left+r.Width*.22f,r.Top+r.Height*.52f),new PointF(r.Left+r.Width*.44f,r.Top+r.Height*.73f),new PointF(r.Left+r.Width*.8f,r.Top+r.Height*.29f)});}
    public static void Text(Graphics g,string text,Font font,Rectangle r,Color color,TextFormatFlags extra=0)=>TextRenderer.DrawText(g,text,font,r,color,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|extra);
    public static Color Blend(Color a,Color b,float t)=>Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
    public static Color Border(ThemePalette p)=>Blend(p.Surface,p.Ink,p.Light?.16f:.14f);
    public static Color OnAccent(ThemePalette p)=>p.Light?Color.White:Color.FromArgb(24,28,36);
    public static void TitleBar(Form f,ThemePalette p){if(!f.IsHandleCreated)return;int dark=p.Light?0:1,bg=ColorTranslator.ToWin32(p.Bg),ink=ColorTranslator.ToWin32(p.Ink);Native.DwmSetWindowAttribute(f.Handle,20,ref dark,4);Native.DwmSetWindowAttribute(f.Handle,35,ref bg,4);Native.DwmSetWindowAttribute(f.Handle,36,ref ink,4);}
}
internal class FlowButton : Button
{
    public ThemePalette Palette {get;set;}=ThemePalette.All[0];
    public bool Primary {get;set;}
    public bool Selected {get;set;}
    public bool Quiet {get;set;}
    public bool AlignLeft {get;set;}
    public ThemePalette? Swatch {get;set;}
    protected bool Hovered,Pressed;
    protected int D(float n)=>(int)Math.Round(n*DeviceDpi/96f);
    public FlowButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;UseVisualStyleBackColor=false;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);}
    protected override void OnMouseEnter(EventArgs e){Hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){Hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){Pressed=e.Button==MouseButtons.Left;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){Pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;var p=Palette;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??p.Bg);var r=new Rectangle(1,1,Width-3,Height-3);var fill=Primary?p.Accent:Selected?p.Selected:Quiet?(Parent?.BackColor??p.Bg):p.Surface;if(Hovered||Pressed)fill=UiPaint.Blend(fill,Pressed?p.Ink:p.Accent,Pressed?.13f:.09f);UiPaint.Box(g,r,fill,D(9),Selected?p.Accent:Quiet&&!Hovered?null:UiPaint.Border(p));
        if(Swatch is ThemePalette s){using var b=new SolidBrush(s.Accent);g.FillEllipse(b,D(12),Height/2-D(8),D(16),D(16));UiPaint.Text(g,Text,Font,new Rectangle(D(36),0,Width-D(42),Height),Enabled?p.Ink:p.Muted,TextFormatFlags.VerticalCenter);if(Selected)UiPaint.Check(g,new Rectangle(Width-D(20),D(4),D(13),D(13)),p.Accent);}
        else UiPaint.Text(g,Text,Font,new Rectangle(D(AlignLeft?16:10),0,Width-D(AlignLeft?28:20),Height),Enabled?(Primary?UiPaint.OnAccent(p):Selected?p.Accent:p.Ink):p.Muted,TextFormatFlags.VerticalCenter|(AlignLeft?0:TextFormatFlags.HorizontalCenter));
        if(Focused&&ShowFocusCues){using var path=UiPaint.Round(Rectangle.Inflate(r,-D(4),-D(4)),D(6));using var pen=new Pen(Primary?UiPaint.OnAccent(p):p.Accent){DashStyle=DashStyle.Dot};g.DrawPath(pen,path);}}
}
internal sealed class FlowCheck : CheckBox
{
    public ThemePalette Palette {get;set;}=ThemePalette.All[0];
    public string Description {get;set;}="";
    private bool hovered;private Font? detailFont;
    private int D(float n)=>(int)Math.Round(n*DeviceDpi/96f);
    public FlowCheck(){Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){var p=Palette;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(BackColor);if(hovered)UiPaint.Box(g,new Rectangle(0,0,Width-1,Height-1),UiPaint.Blend(BackColor,p.Accent,.055f),D(8));var box=new Rectangle(D(12),Height/2-D(10),D(20),D(20));UiPaint.Box(g,box,Checked?p.Accent:p.Raised,D(5),Checked?p.Accent:UiPaint.Border(p));if(Checked)UiPaint.Check(g,box,UiPaint.OnAccent(p));UiPaint.Text(g,Text,Font,new Rectangle(D(46),D(Description.Length==0?0:8),Width-D(58),Description.Length==0?Height:D(23)),Enabled?p.Ink:p.Muted,TextFormatFlags.VerticalCenter);detailFont??=new Font(Font.FontFamily,9,FontStyle.Regular);if(Description.Length>0)UiPaint.Text(g,Description,detailFont,new Rectangle(D(46),D(32),Width-D(58),D(20)),p.Muted);if(Focused&&ShowFocusCues){using var pen=new Pen(p.Accent){DashStyle=DashStyle.Dot};g.DrawRectangle(pen,D(3),D(3),Width-D(7),Height-D(7));}}
    protected override void Dispose(bool disposing){if(disposing)detailFont?.Dispose();base.Dispose(disposing);}
}
internal sealed class FlowCard : Panel
{
    public ThemePalette Palette {get;set;}=ThemePalette.All[0];
    public FlowCard(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent?.BackColor??Palette.Bg);UiPaint.Box(e.Graphics,new Rectangle(1,1,Width-3,Height-3),Palette.Surface,(int)(12*DeviceDpi/96f),UiPaint.Border(Palette));base.OnPaint(e);}
}
internal sealed class FlowMenuColors(ThemePalette p) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground=>p.Surface;
    public override Color ImageMarginGradientBegin=>p.Surface;
    public override Color ImageMarginGradientMiddle=>p.Surface;
    public override Color ImageMarginGradientEnd=>p.Surface;
    public override Color MenuItemSelected=>p.Selected;
    public override Color MenuItemBorder=>p.Accent;
    public override Color MenuBorder=>UiPaint.Border(p);
    public override Color CheckBackground=>p.Selected;
    public override Color CheckSelectedBackground=>p.Selected;
    public override Color SeparatorDark=>UiPaint.Border(p);
    public override Color SeparatorLight=>p.Surface;
}
internal sealed class FlowMenuRenderer(ThemePalette palette):ToolStripProfessionalRenderer(new FlowMenuColors(palette))
{
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;UiPaint.Check(e.Graphics,e.ImageRectangle,palette.Accent);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?palette.Ink:palette.Muted;base.OnRenderItemText(e);}
}
internal sealed class FlowSelect : FlowButton
{
    public List<object> Items {get;}=new();
    private readonly ContextMenuStrip menu=new(){ShowImageMargin=false,ShowCheckMargin=true};private int selected=-1;
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex {get=>selected;set{int next=Items.Count==0?-1:Math.Clamp(value,0,Items.Count-1);if(next==selected)return;selected=next;Text=SelectedItem?.ToString()??"";AccessibleDescription="현재 선택: "+Text;Invalidate();SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}}
    public object? SelectedItem {get=>selected>=0&&selected<Items.Count?Items[selected]:null;set=>SelectedIndex=Items.FindIndex(x=>Equals(x,value));}
    public FlowSelect(){AlignLeft=true;AccessibleRole=AccessibleRole.ComboBox;}
    protected override void OnClick(EventArgs e){foreach(ToolStripItem item in menu.Items.Cast<ToolStripItem>().ToArray())item.Dispose();menu.Renderer=new FlowMenuRenderer(Palette);menu.Font=Font;menu.MinimumSize=new Size(Width,0);menu.BackColor=Palette.Surface;menu.ForeColor=Palette.Ink;for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(Items[i].ToString()){Checked=i==selected,AutoSize=false,Size=new Size(Width,D(36)),ForeColor=Palette.Ink};item.Click+=(_,_)=>{SelectedIndex=index;Focus();};menu.Items.Add(item);}menu.Show(this,new Point(0,Height+D(4)));base.OnClick(e);}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode is Keys.Up or Keys.Left){SelectedIndex--;e.Handled=true;}else if(e.KeyCode is Keys.Down or Keys.Right){SelectedIndex++;e.Handled=true;}base.OnKeyDown(e);}
    internal void TestKey(Keys key)=>OnKeyDown(new KeyEventArgs(key));
    internal ContextMenuStrip TestMenu(){OnClick(EventArgs.Empty);return menu;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Palette.Muted,D(1.5f)){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawLines(pen,new[]{new Point(Width-D(23),Height/2-D(2)),new Point(Width-D(18),Height/2+D(3)),new Point(Width-D(13),Height/2-D(2))});}
    protected override void Dispose(bool disposing){if(disposing){menu.Close();menu.Dispose();}base.Dispose(disposing);}
}
internal sealed class FlowAppList : ListBox
{
    public ThemePalette Palette {get;set;}=ThemePalette.All[0];private readonly HashSet<object> checks=new();
    public event EventHandler? ChecksChanged;
    public IEnumerable<object> CheckedItems=>Items.Cast<object>().Where(checks.Contains);
    private Font? processFont;
    public FlowAppList(){DrawMode=DrawMode.OwnerDrawFixed;BorderStyle=BorderStyle.None;IntegralHeight=false;ItemHeight=48;AccessibleName="상단 정렬 앱 · Space로 등록 또는 해제";}
    public void SetChecked(object item,bool value){if(value)checks.Add(item);else checks.Remove(item);Invalidate();ChecksChanged?.Invoke(this,EventArgs.Empty);}
    public void ResetChecks()=>checks.Clear();
    internal void TestKey(Keys key)=>OnKeyDown(new KeyEventArgs(key));
    protected override AccessibleObject CreateAccessibilityInstance()=>new AppListAccessibility(this);
    private sealed class AppListAccessibility(FlowAppList list):ControlAccessibleObject(list){public override int GetChildCount()=>list.Items.Count;public override AccessibleObject? GetChild(int index)=>index>=0&&index<list.Items.Count?new AppAccessibility(list,index):null;}
    private sealed class AppAccessibility(FlowAppList list,int index):AccessibleObject{
        public override string? Name {get=>list.Items[index].ToString();set{}}
        public override AccessibleRole Role=>AccessibleRole.CheckButton;
        public override AccessibleStates State=>AccessibleStates.Focusable|AccessibleStates.Selectable|(list.checks.Contains(list.Items[index])?AccessibleStates.Checked:0)|(list.SelectedIndex==index?AccessibleStates.Selected:0);
        public override Rectangle Bounds=>list.RectangleToScreen(list.GetItemRectangle(index));
        public override string DefaultAction=>"등록 / 해제";
        public override AccessibleObject Parent=>list.AccessibilityObject;
        public override void DoDefaultAction()=>list.SetChecked(list.Items[index],!list.checks.Contains(list.Items[index]));
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);int i=IndexFromPoint(e.Location);if(e.Button==MouseButtons.Left&&i>=0)SetChecked(Items[i],!checks.Contains(Items[i]));}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space&&SelectedIndex>=0){SetChecked(Items[SelectedIndex],!checks.Contains(Items[SelectedIndex]));e.Handled=e.SuppressKeyPress=true;}base.OnKeyDown(e);}
    protected override void OnDrawItem(DrawItemEventArgs e){
        if(e.Index<0)return;var p=Palette;var g=e.Graphics;int D(float x)=>(int)Math.Round(x*DeviceDpi/96f);bool active=(e.State&DrawItemState.Selected)!=0,on=checks.Contains(Items[e.Index]);
        using(var b=new SolidBrush(active?p.Selected:p.Surface))g.FillRectangle(b,e.Bounds);g.SmoothingMode=SmoothingMode.AntiAlias;
        var r=new Rectangle(e.Bounds.X+D(10),e.Bounds.Y+(e.Bounds.Height-D(18))/2,D(18),D(18));UiPaint.Box(g,r,on?p.Accent:p.Raised,D(4),on?p.Accent:UiPaint.Border(p));if(on)UiPaint.Check(g,r,UiPaint.OnAccent(p));
        var item=Items[e.Index] as IAppListItem;string title=item?.AppName??Items[e.Index].ToString()??"";
        var icon=new Rectangle(e.Bounds.X+D(42),e.Bounds.Y+(e.Bounds.Height-D(28))/2,D(28),D(28));AppIconDrawing.Paint(g,icon,item?.AppIcon,title,Font,p);
        var text=new Rectangle(e.Bounds.X+D(82),e.Bounds.Y+D(item==null?0:4),e.Bounds.Width-D(94),item==null?e.Bounds.Height:D(23));UiPaint.Text(g,title,Font,text,p.Ink,TextFormatFlags.VerticalCenter);
        if(item!=null){processFont??=new Font(Font.FontFamily,8.5f);UiPaint.Text(g,item.ProcessName+".exe",processFont,new Rectangle(text.X,e.Bounds.Y+D(27),text.Width,D(18)),p.Muted);}
        if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(e.Bounds,-2,-2),p.Accent,p.Surface);
    }
    protected override void Dispose(bool disposing){if(disposing)processFont?.Dispose();base.Dispose(disposing);}
}
internal interface IAppListItem {string AppName{get;}string ProcessName{get;}Image? AppIcon{get;set;}}
internal static class AppIconDrawing
{
    public static void Paint(Graphics g,Rectangle bounds,Image? icon,string name,Font font,ThemePalette p){
        if(icon!=null){g.DrawImage(icon,bounds);return;}
        UiPaint.Box(g,bounds,p.Raised,Math.Max(4,bounds.Width/4));
        string initial=string.IsNullOrWhiteSpace(name)?"?":System.Globalization.StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
        UiPaint.Text(g,initial,font,bounds,p.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}