namespace AltTabFlow;

// HWNDs of an owner and its last active popup can alternate in EnumWindows.
// Keep their history under one stable owner, without merging unrelated windows
// merely because they run in the same process (Chrome/Excel, for example).
internal static class WindowIdentity
{
    public static nint Root(nint hwnd) {
        nint root=Native.GetAncestor(hwnd,2);if(root==0)return hwnd;
        Native.GetWindowThreadProcessId(root,out uint pid);
        var seen=new HashSet<nint>{root};
        for(int i=0;i<16;i++) {
            nint owner=Native.GetWindow(root,4);
            if(owner==0||!seen.Add(owner))break;
            Native.GetWindowThreadProcessId(owner,out uint ownerPid);
            if(ownerPid!=pid)break;
            // Hidden framework parking/tool windows are not an application's
            // real owner. Otherwise two independent forms could be conflated.
            if(!Native.IsWindowVisible(owner)&&((long)Native.GetWindowLongPtr(owner,-20)&0x80)!=0)break;
            root=owner;
        }
        return root;
    }
    public static bool Same(nint a,nint b)=>a!=0&&b!=0&&(a==b||Root(a)==Root(b));
}

internal sealed class ForegroundHistory
{
    private readonly object gate=new();
    private readonly Dictionary<(nint Root,uint Pid),long> ranks=new();
    private long sequence;
    private uint lastTime;
    private bool initialized;
    public bool Record(nint root,uint pid,uint time) {
        if(root==0)return false;
        lock(gate) {
            // WinEvent time and GetTickCount use the same wrapping millisecond clock.
            // A delayed event must never supersede a newer direct foreground sample.
            if(initialized&&unchecked((int)(time-lastTime))<0)return false;
            initialized=true;lastTime=time;ranks[(root,pid)]=++sequence;
            if(ranks.Count>512)foreach(var key in ranks.OrderBy(x=>x.Value).Take(128).Select(x=>x.Key).ToArray())ranks.Remove(key);
            return true;
        }
    }
    public long Rank(nint root,uint pid){lock(gate)return ranks.GetValueOrDefault((root,pid));}
    public long Rank(nint hwnd){Native.GetWindowThreadProcessId(hwnd,out uint pid);return Rank(WindowIdentity.Root(hwnd),pid);}
}

internal readonly record struct ShortcutInput(SwitchCommand Command,long Timestamp);

// Foreground permission fallback can communicate with foreign GUI queues.
// Keep it on a worker that never owns our UI windows. Only one request runs;
// new requests replace pending work, and cancellation is checked before calls.
internal sealed class WindowActivation : IDisposable
{
    private readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    private Task<bool>? work;
    private nint target,origin;
    private long started;
    // Game/maximized-window transitions often take longer than 250ms. Keep
    // foreground retries bounded and off the UI thread; never loop forever.
    internal const int ActivationDeadlineMs=850;
    public WindowActivation(){timer.Tick+=(_,_)=>Pump();}
    public void Request(nint hwnd){
        Volatile.Write(ref target,hwnd);origin=Native.GetForegroundWindow();started=Environment.TickCount64;
        Pump();if(Volatile.Read(ref target)!=0)timer.Start();
    }
    private void Pump(){
        nint wanted=Volatile.Read(ref target);
        if(wanted==0){timer.Stop();return;}
        nint foreground=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(foreground,out uint pid);
        if(WindowIdentity.Same(foreground,wanted)||Environment.TickCount64-started>=ActivationDeadlineMs||
            (foreground!=0&&pid!=Environment.ProcessId&&!WindowIdentity.Same(foreground,origin))){Cancel();return;}
        if(work==null||work.IsCompleted)work=Task.Run(()=>Native.Activate(wanted,()=>Volatile.Read(ref target)==wanted));
    }
    public void Cancel(){Volatile.Write(ref target,0);timer.Stop();}
    public void Dispose(){Cancel();timer.Dispose();}
}

internal static class QuickGesture
{
    // Only a complete, already queued plain gesture may skip creating the UI.
    // Arrow keys, pinned mode and Enter/group handling always use the normal UI.
    public static int Length(IReadOnlyList<ShortcutInput> input,int start) {
        int i=start;
        while(i<input.Count&&input[i].Command is SwitchCommand.Next or SwitchCommand.Previous or SwitchCommand.SameApp or SwitchCommand.SameAppPrevious)i++;
        return i>start&&i<input.Count&&input[i].Command==SwitchCommand.Commit?i-start+1:0;
    }
}