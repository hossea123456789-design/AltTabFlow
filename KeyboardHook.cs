using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AltTabFlow;

internal enum SwitchCommand { Next, Previous, PinnedNext, PinnedPrevious, SameApp, SameAppPrevious, Commit, Persistent, Pause, Left, Right, Up, Down }

// Pure routing: modifier values are a fresh snapshot for this shortcut, never a
// latch seeded at startup (a missed Ctrl-up used to turn Tab into a close toggle).
internal sealed class ShortcutRouter
{
    private readonly Action<SwitchCommand> post;
    private bool session, swallowedTab, swallowedGrave, swallowedSpace, swallowedPause;
    private int swallowedArrows;
    public ShortcutRouter(Action<SwitchCommand> post) { this.post=post; }
    public bool Route(uint key,bool down,bool alt,bool ctrl,bool shift,bool enabled,bool otherAltDown=false) {
        if(key is 0xA4 or 0xA5 or 0x12 && !down && session && !otherAltDown) {
            session=false;post(SwitchCommand.Commit);
        }
        if(key is >=0x25 and <=0x28) {
            int bit=1<<((int)key-0x25);
            if(session&&down){swallowedArrows|=bit;post(key==0x25?SwitchCommand.Left:key==0x26?SwitchCommand.Up:key==0x27?SwitchCommand.Right:SwitchCommand.Down);return true;}
            if(!down&&(swallowedArrows&bit)!=0){swallowedArrows&=~bit;return true;}
        }
        if(key==0x7B&&ctrl&&alt&&down){if(!swallowedPause)post(SwitchCommand.Pause);swallowedPause=true;return true;}
        if(key==0x7B&&!down&&swallowedPause){swallowedPause=false;return true;}
        if(key==0x20&&ctrl&&alt&&down){if(!swallowedSpace){session=false;post(SwitchCommand.Persistent);}swallowedSpace=true;return true;}
        if(key==0x20&&!down&&swallowedSpace){swallowedSpace=false;return true;}
        if(enabled&&alt&&down&&key==9){
            session=true;swallowedTab=true;
            post(ctrl?(shift?SwitchCommand.PinnedPrevious:SwitchCommand.PinnedNext):(shift?SwitchCommand.Previous:SwitchCommand.Next));
            return true;
        }
        if(key==9&&!down&&swallowedTab){swallowedTab=false;return true;}
        if(enabled&&alt&&!ctrl&&down&&key==0xC0){session=true;swallowedGrave=true;post(shift?SwitchCommand.SameAppPrevious:SwitchCommand.SameApp);return true;}
        if(key==0xC0&&!down&&swallowedGrave){swallowedGrave=false;return true;}
        return false;
    }
}

internal sealed class KeyboardHook : IDisposable
{
    private readonly ShortcutRouter router;
    private readonly Thread thread;
    private readonly Native.HookProc callback;
    private readonly Native.WinEventProc foregroundCallback;
    private readonly ManualResetEventSlim ready = new();
    private nint handle;
    private nint foregroundHook;
    private uint threadId;
    private Exception? failure;
    private long eventCount, tabCount;
    private bool lastTabAlt, lastTabCtrl;
    public object Statistics => new {present=handle!=0,foregroundPresent=foregroundHook!=0,alive=thread.IsAlive,events=Interlocked.Read(ref eventCount),tabs=Interlocked.Read(ref tabCount),lastTabAlt,lastTabCtrl};
    public volatile bool Enabled = true;
    public KeyboardHook(Action<SwitchCommand> post,Action<nint,uint>? foreground=null) {
        router = new ShortcutRouter(post); callback = Callback;
        foregroundCallback=(_,_,hwnd,_,_,_,time)=> {
            // Delayed notifications describe history, not necessarily current focus.
            if(hwnd!=0&&hwnd==Native.GetForegroundWindow())foreground?.Invoke(hwnd,time);
        };
        thread = new Thread(Run) { IsBackground = true, Name = "AltTabFlow shortcuts" };
        thread.Start(); ready.Wait(TimeSpan.FromSeconds(4));
        if (failure != null) throw failure;
        if (handle == 0) throw new InvalidOperationException("단축키 초기화에 실패했습니다.");
    }
    private void Run() {
        threadId = Native.GetCurrentThreadId();
        handle = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        foregroundHook=Native.SetWinEventHook(3,3,0,foregroundCallback,0,0,2);
        if (handle == 0) failure = new Win32Exception(Marshal.GetLastWin32Error());
        ready.Set();
        if (handle == 0) return;
        try { while (Native.GetMessage(out _, 0, 0, 0) > 0) { } }
        finally { Native.UnhookWindowsHookEx(handle); handle = 0;if(foregroundHook!=0)Native.UnhookWinEvent(foregroundHook);foregroundHook=0; }
    }
    private nint Callback(int code, nint wp, nint lp) {
        if (code < 0) return Native.CallNextHookEx(handle, code, wp, lp);
        try {
            Interlocked.Increment(ref eventCount);
            var k = Marshal.PtrToStructure<Native.Keyboard>(lp);
            bool down = (uint)wp is 0x100 or 0x104;
            bool up = (uint)wp is 0x101 or 0x105;
            if (!down && !up) return Native.CallNextHookEx(handle, code, wp, lp);
            // LLKHF_ALTDOWN belongs to this event. GetAsyncKeyState is used only
            // for OTHER keys, whose earlier transitions have already completed.
            bool alt = (k.Flags & 0x20) != 0;
            bool ctrl = Native.GetAsyncKeyState(0x11)<0, shift = Native.GetAsyncKeyState(0x10)<0;
            if(k.Key==9&&down){Interlocked.Increment(ref tabCount);lastTabAlt=alt;lastTabCtrl=ctrl;}
            bool otherAltDown=k.Key==0xA4?Native.GetAsyncKeyState(0xA5)<0:k.Key==0xA5&&Native.GetAsyncKeyState(0xA4)<0;
            if(router.Route(k.Key,down,alt,ctrl,shift,Enabled,otherAltDown))return 1;
        } catch { /* A hook must never propagate exceptions into user input. */ }
        return Native.CallNextHookEx(handle, code, wp, lp);
    }
    public void Dispose() {
        if (threadId != 0) Native.PostThreadMessage(threadId, 0x12, 0, 0);
        thread.Join(1000); ready.Dispose();
    }
}
