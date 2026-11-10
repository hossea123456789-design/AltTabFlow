namespace AltTabFlow;

// Raise without taking keyboard focus. Undo temporary Z-order before the next
// preview/cancel/commit, so alphabetical browsing does not alter the MRU history.
internal sealed class TargetPeek : IDisposable
{
    private nint target, previous, chooser;
    private bool wasTopmost;
    private bool requestAccepted;
    private int lastError;
    private readonly List<nint> originallyTopmostOwned=new();
    private const uint PositionFlags=0x4613; // Async, no activation/size/move/owner movement/changing message.
    private static bool Topmost(nint hwnd)=>((long)Native.GetWindowLongPtr(hwnd,-20)&8)!=0;
    public object Statistics=>new{target=target.ToInt64(),chooser=chooser.ToInt64(),requestAccepted,lastError,wasTopmost,temporaryTopmost=target!=0&&Topmost(target),foreground=Native.GetForegroundWindow().ToInt64()};
    public void Select(nint hwnd,nint switcher){
        if(hwnd==target&&switcher==chooser)return;
        Restore();
        if(!Native.IsWindow(hwnd)||!Native.IsWindowVisible(hwnd)||Native.IsIconic(hwnd)||!Native.IsWindow(switcher))return;
        target=hwnd;chooser=switcher;wasTopmost=Topmost(hwnd);
        previous=Native.GetWindow(hwnd,3);
        var seen=new HashSet<nint>();
        while(previous!=0&&seen.Count<512&&seen.Add(previous)){
            Native.GetWindowThreadProcessId(previous,out uint pid);
            if(pid!=Environment.ProcessId&&Topmost(previous)==wasTopmost)break;
            previous=Native.GetWindow(previous,3);
        }
        if(previous!=0&&seen.Count>=512)previous=0;
        // Demoting the root also demotes owned palettes. Remember any that were
        // already topmost so cancellation/commit does not change their setting.
        nint root=WindowIdentity.Root(target);
        Native.EnumWindows((h,_)=>{if(h!=target&&WindowIdentity.Root(h)==root&&Topmost(h))originallyTopmostOwned.Add(h);return true;},0);
        Native.GetWindowThreadProcessId(target,out uint targetPid);
        Native.AllowSetForegroundWindow(targetPid);
        // HWND_TOP only raises within the normal band. Position immediately
        // behind the topmost chooser instead, above any obscuring topmost app.
        // This may temporarily promote the target; Restore/Accept undo that band.
        requestAccepted=Native.SetWindowPos(target,chooser,0,0,0,0,PositionFlags);
        lastError=requestAccepted?0:System.Runtime.InteropServices.Marshal.GetLastWin32Error();
    }
    public void Restore(){
        if(target==0)return;
        if(Native.IsWindow(target)){
            RestoreBand();
            if(previous!=0&&Native.IsWindow(previous)&&Topmost(previous)==wasTopmost)
                Native.SetWindowPos(target,previous,0,0,0,0,PositionFlags);
        }
        Clear();
    }
    private void RestoreBand(){
        Native.SetWindowPos(target,wasTopmost?-1:-2,0,0,0,0,PositionFlags);
        foreach(nint h in originallyTopmostOwned)if(Native.IsWindow(h))Native.SetWindowPos(h,-1,0,0,0,0,PositionFlags);
    }
    private void Clear(){target=previous=chooser=0;originallyTopmostOwned.Clear();}
    public void Accept(){if(target!=0&&Native.IsWindow(target))RestoreBand();Clear();}
    public void Dispose()=>Restore();
}

internal sealed class TargetDimmer : IDisposable
{
    private readonly List<DimSurface> surfaces=new();
    private Rectangle lastTarget;
    public void Select(nint target,nint switcher){
        if(!Native.IsWindow(target)||Native.IsIconic(target)){Hide();return;}
        if(Native.DwmGetWindowAttributeRect(target,9,out var r,System.Runtime.InteropServices.Marshal.SizeOf<Native.Rect>())!=0&&!Native.GetWindowRect(target,out r)){Hide();return;}
        var rect=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
        var screens=Screen.AllScreens;
        while(surfaces.Count<screens.Length)surfaces.Add(new DimSurface());
        for(int i=0;i<surfaces.Count;i++){
            if(i>=screens.Length){surfaces[i].Hide();continue;}
            var surface=surfaces[i];var bounds=screens[i].Bounds;
            if(surface.Bounds!=bounds||lastTarget!=rect||!surface.Visible){
                surface.Bounds=bounds;
                var region=new Region(new Rectangle(Point.Empty,bounds.Size));
                var hole=Rectangle.Intersect(bounds,rect);hole.Offset(-bounds.X,-bounds.Y);
                if(hole.Width>0&&hole.Height>0)region.Exclude(hole);
                var old=surface.Region;surface.Region=region;old?.Dispose();
            }
            if(!surface.Visible){surface.Show();Native.SetWindowPos(surface.Handle,switcher,0,0,0,0,0x13);}
        }
        lastTarget=rect;
    }
    public void Hide(){foreach(var surface in surfaces)surface.Hide();}
    public void Dispose(){foreach(var surface in surfaces)surface.Dispose();surfaces.Clear();}
    private sealed class DimSurface : Form
    {
        public DimSurface(){Text="AltTabFlow focus shade";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;BackColor=Color.Black;Opacity=.30;}
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x20|0x80|0x8000000;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=-1;return;}if(m.Msg==0x21){m.Result=3;return;}base.WndProc(ref m);}
    }
}

