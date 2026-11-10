using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AltTabFlow;

// Four thin per-pixel-alpha surfaces, never a full-monitor animation bitmap.
// No activation, mouse interception, screenshots, or changes to the target window.
internal sealed class TargetAura : IDisposable
{
    private readonly AuraStrip[] strips=Enumerable.Range(0,4).Select(_=>new AuraStrip()).ToArray();
    private readonly System.Windows.Forms.Timer timer=new(){Interval=33};
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private nint target, switcher;
    private Color color;
    private bool strong;
    private Rectangle frame;
    private PointF[] points=Array.Empty<PointF>();
    private float[] progress=Array.Empty<float>();
    private long frames;
    private double renderTotalMs;
    public object Statistics=>new{active=timer.Enabled,target=target.ToInt64(),frames,averageRenderMs=frames==0?0:renderTotalMs/frames,bufferPixels=strips.Sum(x=>x.PixelCount),frame=frame.ToString()};
    public TargetAura(){timer.Tick+=(_,_)=>Tick();}
    public void Select(nint hwnd,nint switcherHandle,Color accent,bool emphasize) {
        if(strong!=emphasize)frame=Rectangle.Empty;
        target=hwnd;switcher=switcherHandle;color=accent;strong=emphasize;
        if(!TryFrame(out var rect)){Hide();return;}
        Layout(rect);Render();timer.Start();
    }
    private bool TryFrame(out Rectangle rect){
        rect=Rectangle.Empty;
        if(target==0||!Native.IsWindow(target)||!Native.IsWindowVisible(target)||Native.IsIconic(target))return false;
        if(Native.DwmGetWindowAttributeRect(target,9,out var r,Marshal.SizeOf<Native.Rect>())!=0&&!Native.GetWindowRect(target,out r))return false;
        rect=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
        return rect.Width>40&&rect.Height>40&&SystemInformation.VirtualScreen.IntersectsWith(rect);
    }
    internal static Rectangle[] PlanStrips(Rectangle r,int pad=14)=>new[]{
        new Rectangle(r.Left-pad,r.Top-pad,r.Width+pad*2,pad*2),
        new Rectangle(r.Right-pad,r.Top+pad,pad*2,Math.Max(1,r.Height-pad*2)),
        new Rectangle(r.Left-pad,r.Bottom-pad,r.Width+pad*2,pad*2),
        new Rectangle(r.Left-pad,r.Top+pad,pad*2,Math.Max(1,r.Height-pad*2))
    };
    private void Layout(Rectangle rect){
        if(frame==rect&&points.Length>0)return;
        frame=rect;
        var bounds=PlanStrips(rect,strong?22:14);
        for(int i=0;i<4;i++)strips[i].Configure(bounds[i]);
        using var path=new GraphicsPath();
        // Stroke lies just INSIDE the target edge, so maximized windows retain
        // their aura even when the outside half is beyond the monitor.
        float x=rect.Left+2,y=rect.Top+2,w=rect.Width-4,h=rect.Height-4,r=10;
        path.AddArc(x,y,r*2,r*2,180,90);path.AddArc(x+w-r*2,y,r*2,r*2,270,90);
        path.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90);path.AddArc(x,y+h-r*2,r*2,r*2,90,90);path.CloseFigure();path.Flatten(null,.7f);
        var polygon=path.PathPoints.Concat(new[]{path.PathPoints[0]}).ToArray();
        var sampled=new List<PointF>();
        for(int i=0;i<polygon.Length-1;i++){
            var a=polygon[i];var b=polygon[i+1];float length=Distance(a,b);
            int n=Math.Max(1,(int)Math.Ceiling(length/64));
            for(int j=0;j<n;j++)sampled.Add(new(a.X+(b.X-a.X)*j/n,a.Y+(b.Y-a.Y)*j/n));
        }
        sampled.Add(sampled[0]);points=sampled.ToArray();progress=new float[points.Length];
        for(int i=1;i<points.Length;i++)progress[i]=progress[i-1]+Distance(points[i-1],points[i]);
        float total=progress[^1];for(int i=0;i<progress.Length;i++)progress[i]/=total;
    }
    private static float Distance(PointF a,PointF b)=>MathF.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private void Tick(){
        if(!TryFrame(out var rect)||!Native.IsWindowVisible(switcher)){Hide();return;}
        Layout(rect);Render();
    }
    private void Render(){
        long stamp=Stopwatch.GetTimestamp();float phase=(float)(clock.Elapsed.TotalSeconds/1.4%1);
        foreach(var strip in strips)strip.Render(points,progress,phase,color,switcher,strong);
        frames++;renderTotalMs+=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
    }
    public void Hide(){timer.Stop();foreach(var strip in strips)strip.Hide();target=0;}
    public void Dispose(){timer.Dispose();foreach(var strip in strips)strip.Dispose();}

    private sealed class AuraStrip : Form
    {
        private nint dc,dib,oldBitmap;
        private Bitmap? bitmap;
        private Graphics? canvas;
        private Rectangle surface;
        private readonly Pen[] pens={new(Color.Transparent,22),new(Color.Transparent,13),new(Color.Transparent,6),new(Color.Transparent,2)};
        private static readonly int[] Alpha={13,24,58,220};
        public int PixelCount=>surface.Width*surface.Height;
        public AuraStrip(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;Text="AltTabFlow aura";foreach(var pen in pens){pen.StartCap=pen.EndCap=LineCap.Round;}}
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x80|0x8000000;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=-1;return;}if(m.Msg==0x21){m.Result=3;return;}base.WndProc(ref m);}
        public void Configure(Rectangle bounds){
            if(bounds.Size!=surface.Size){
                ReleaseBuffer();dc=Native.CreateCompatibleDC(0);
                var info=new Native.BitmapInfo{Size=40,Width=bounds.Width,Height=-bounds.Height,Planes=1,BitCount=32};
                dib=Native.CreateDIBSection(dc,ref info,0,out var bits,0,0);
                if(dib==0)throw new InvalidOperationException("아우라 버퍼를 만들 수 없습니다.");
                oldBitmap=Native.SelectObject(dc,dib);
                bitmap=new Bitmap(bounds.Width,bounds.Height,bounds.Width*4,PixelFormat.Format32bppPArgb,bits);
                canvas=Graphics.FromImage(bitmap);canvas.SmoothingMode=SmoothingMode.AntiAlias;
            }
            surface=bounds;Bounds=bounds;
        }
        public void Render(PointF[] points,float[] progress,float phase,Color accent,nint switcher,bool strong){
            if(canvas==null)return;
            canvas.ResetTransform();canvas.Clear(Color.Transparent);canvas.TranslateTransform(-surface.Left,-surface.Top);
            for(int i=0;i<points.Length-1;i++){
                var a=points[i];var b=points[i+1];
                if(Math.Max(a.X,b.X)+12<surface.Left||Math.Min(a.X,b.X)-12>surface.Right||Math.Max(a.Y,b.Y)+12<surface.Top||Math.Min(a.Y,b.Y)-12>surface.Bottom)continue;
                float d=(phase-progress[i]+1)%1;
                float strength=(strong?.55f:.20f)+1.3f*MathF.Exp(-d*9)+.8f*MathF.Exp(-((d+.5f)%1)*9);
                for(int j=0;j<pens.Length;j++){pens[j].Width=(j==0?22:j==1?13:j==2?6:2)*(strong?1.5f:1);pens[j].Color=Color.FromArgb(Math.Min(255,(int)(Alpha[j]*strength*(strong?1.65f:1))),accent);canvas.DrawLine(pens[j],a,b);}
            }
            canvas.Flush();
            var dest=new Native.Point{X=surface.Left,Y=surface.Top};var src=new Native.Point();var size=new Native.Size{Width=surface.Width,Height=surface.Height};
            var blend=new Native.BlendFunction{SourceConstantAlpha=255,AlphaFormat=1};
            bool firstShow=!Visible;
            if(firstShow)Show();
            Native.UpdateLayeredWindow(Handle,0,ref dest,ref size,dc,ref src,0,ref blend,2);
            // Keep the aura behind our chooser while remaining above ordinary windows.
            // A foreign app's asynchronous raise can arrive after this frame.
            // Reassert our narrow effect above it, still below the chooser.
            Native.SetWindowPos(Handle,switcher,0,0,0,0,0x13);
        }
        private void ReleaseBuffer(){
            canvas?.Dispose();canvas=null;bitmap?.Dispose();bitmap=null;
            if(dc!=0&&oldBitmap!=0)Native.SelectObject(dc,oldBitmap);
            if(dib!=0)Native.DeleteObject(dib);if(dc!=0)Native.DeleteDC(dc);
            dc=dib=oldBitmap=0;
        }
        protected override void Dispose(bool disposing){if(disposing){ReleaseBuffer();foreach(var pen in pens)pen.Dispose();}base.Dispose(disposing);}
    }
}
