using System.Runtime.InteropServices;
using System.Text;

namespace AltTabFlow;

internal static class Native
{
    public delegate bool EnumProc(nint hwnd, nint param);
    public delegate nint HookProc(int code, nint wParam, nint lParam);
    public delegate void WinEventProc(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] public static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(nint hook);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; public Rect(Rectangle r) { Left = r.Left; Top = r.Top; Right = r.Right; Bottom = r.Bottom; } }
    [StructLayout(LayoutKind.Sequential)] public struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Message { public nint Hwnd; public uint Msg; public nuint WParam; public nint LParam; public uint Time; public Point Pt; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] public struct Keyboard { public uint Key, ScanCode, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct ThumbnailProperties {
        public uint Flags; public Rect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, nint param);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint="GetClassLongPtrW")] public static extern nint GetClassLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] public static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] public static extern nint GetLastActivePopup(nint hwnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(nint hwnd);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern nuint SetTimer(nint hwnd,nuint id,uint milliseconds,nint callback);
    [DllImport("user32.dll")] public static extern bool KillTimer(nint hwnd,nuint id);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(Point point);
    [StructLayout(LayoutKind.Sequential,Pack=1)] public struct BlendFunction {public byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat;}
    [StructLayout(LayoutKind.Sequential)] public struct BitmapInfo {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPelsPerMeter,YPelsPerMeter;public uint ClrUsed,ClrImportant;}
    [DllImport("gdi32.dll")] public static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] public static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
    [DllImport("gdi32.dll")] public static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(nint dc);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(nint hwnd,nint dstDc,ref Point dst,ref Size size,nint srcDc,ref Point src,uint key,ref BlendFunction blend,uint flags);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] public static extern int DwmGetWindowAttributeRect(nint hwnd,int attr,out Rect value,int size);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")] public static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll", SetLastError=true)] public static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int code, nint wp, nint lp);
    [DllImport("user32.dll")] public static extern int GetMessage(out Message message, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint id, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll")] public static extern bool PostMessage(nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wp, nint lp, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern nint GetModuleHandle(string? module);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(nint dest, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);

    public static string Title(nint hwnd) { var s = new StringBuilder(1024); GetWindowText(hwnd, s, s.Capacity); return s.ToString(); }
    public static string Class(nint hwnd) { var s = new StringBuilder(256); GetClassName(hwnd, s, s.Capacity); return s.ToString(); }
    public static bool Activate(nint hwnd,Func<bool>? currentRequest=null) {
        if(currentRequest!=null&&!currentRequest())return false;
        if (!IsWindow(hwnd)) return false;
        if(GetForegroundWindow()==hwnd)return true;
        var popup = GetLastActivePopup(hwnd);
        if (popup != 0 && IsWindowVisible(popup)) hwnd = popup;
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, 9);
        bool firstRequest=SetForegroundWindow(hwnd);
        // SetForegroundWindow returning true is NOT proof the compositor has
        // finished handing focus from a busy maximized/full-screen game.
        nint foreground=GetForegroundWindow();
        if(WindowIdentity.Same(foreground,hwnd))return true;
        if(foreground==0||IsHungAppWindow(foreground)||IsHungAppWindow(hwnd))return false;
        // Called by WindowActivation's worker, never the application's GUI.
        // Do not attach our UI thread even when it is the foreground or target.
        if(SendMessageTimeout(foreground,0,0,0,0x22,20,out _)==0)return false;
        uint current=GetCurrentThreadId(),other=GetWindowThreadProcessId(foreground,out uint otherPid);
        uint targetThread=GetWindowThreadProcessId(hwnd,out uint targetPid);
        if(targetPid!=Environment.ProcessId&&SendMessageTimeout(hwnd,0,0,0,0x22,20,out _)==0)return false;
        if(currentRequest!=null&&!currentRequest())return false;
        bool attached=otherPid!=Environment.ProcessId&&current!=other&&AttachThreadInput(current,other,true);
        bool targetAttached=targetPid!=Environment.ProcessId&&targetThread!=current&&targetThread!=other&&AttachThreadInput(current,targetThread,true);
        try {
            if(currentRequest!=null&&!currentRequest())return false;
            BringWindowToTop(hwnd);
            bool fallbackRequest=SetForegroundWindow(hwnd);
            return WindowIdentity.Same(GetForegroundWindow(),hwnd)||firstRequest||fallbackRequest;
        }
        finally { if(targetAttached)AttachThreadInput(current,targetThread,false);if(attached)AttachThreadInput(current,other,false); }
    }
}
