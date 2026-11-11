using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace AltTabFlow;

internal sealed class SwitcherForm : Form
{
    private readonly Preferences prefs;
    private readonly CatalogService catalog;
    private readonly Func<nint,bool> activateTarget;
    private readonly TextBox search = new();
    private readonly ContextMenuStrip itemMenu = new();
    private readonly ToolTip toolTip = new() { InitialDelay = 550, ReshowDelay = 150, AutoPopDelay = 8000 };
    private readonly List<(Rectangle Rect, Action Action, string Tip)> hits = new();
    private Rectangle hoverRect,pressedRect;
    private Action? pressedAction;
    private readonly List<nint> thumbnails = new();
    private readonly List<(WindowInfo Window, Rectangle Rect)> desiredThumbs = new();
    private string thumbnailSignature = "", lastTip = "";
    private List<WindowInfo> snapshot = new();
    private List<Entry> entries = new();
    private readonly ForegroundHistory recency = new();
    private int selected, page, pageSize = 6, columns = 1;
    private ListLayout listLayout=new(1,6,74,1120,750,false);
    private TileLayout tileLayout=new(3,2,1300,900);
    private string? group;
    private nint origin, monitor;
    private bool persistent, configuring, forceExit, updating, thumbnailQueued;
    private float scale = 1;
    private Rectangle content;
    private Stopwatch? opening;
    private double lastPaintMs;
    private readonly Queue<double> paintSamples = new();
    private readonly System.Windows.Forms.Timer refreshUi = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer visualTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer commitTimer = new() { Interval = 10 };
    // Used only while the switcher is visible. It does not fight Windows
    // foreground focus for unrelated apps after the user switches away.
    internal const int CommitDeadlineMs=900;
    private readonly bool nativeActivation;
    private bool committing, presenting, requestAccepted;
    private nint commitTarget;
    private long commitStarted, releaseTimestamp, lastNavigationTick;
    public bool IsCommitting=>committing;
    public Action? CommitFinished;
    private volatile bool catalogDirty;
    private readonly Dictionary<(int Size,bool Bold),Font> fonts=new();
    private readonly Bitmap brandIcon;
    private readonly TargetAura aura=new();
    private readonly TargetPeek peek=new();
    private readonly TargetDimmer dimmer=new();
    private readonly WindowActivation activation=new();
    public Action? PreferencesChanged;
    public Action<bool>? ShortcutSuppression;
    public Action? ExitRequested;
    public bool Paused { get; set; }
    public bool InspectionMode { get; set; }
    public bool IsPersistent => persistent;
    public bool IsConfiguring => configuring;
    public long InputTimestamp { get; set; }
    public string CurrentView => prefs.View;

    private ThemePalette Palette=>ThemePalette.Resolve(prefs);
    private Color Bg => Palette.Bg;
    private Color Surface => Palette.Surface;
    private Color Raised => Palette.Raised;
    private Color Ink => Palette.Ink;
    private Color Muted => Palette.Muted;
    private Color Accent => Palette.Accent;
    private Color SelectedBg => Palette.Selected;
    private int D(float v) => (int)Math.Round(v * scale);
    private Rectangle R(float x, float y, float w, float h) => new(D(x), D(y), D(w), D(h));
    private float W => ClientSize.Width / scale;
    private float H => ClientSize.Height / scale;

    public SwitcherForm(Preferences prefs, CatalogService catalog,Func<nint,bool>? activateTarget=null) {
        this.prefs = prefs; this.catalog = catalog;
        this.activateTarget=activateTarget??(h=>{activation.Request(h);return true;});
        nativeActivation=activateTarget==null;
        using(var stream=typeof(SwitcherForm).Assembly.GetManifestResourceStream("AltTabFlow.Assets.AltTabFlow.ico"))
        using(var icon=new Icon(stream!,48,48)) { brandIcon=icon.ToBitmap(); Icon=(Icon)icon.Clone(); }
        Text = "AltTabFlow · 창 전환"; Name = "AltTabFlow";
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false; TopMost = true; KeyPreview = true; AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        search.BorderStyle = BorderStyle.None;
        search.PlaceholderText = "창 제목이나 앱 이름으로 검색";
        search.AccessibleName = "창 제목 또는 앱 이름 검색";
        search.TextChanged += (_, _) => { if (!updating) { persistent=true; selected = page = 0; Rebuild(false); } };
        search.MouseDown += (_, _) => { persistent = true; Invalidate(); };
        Controls.Add(search);
        // Closed runs inside ToolStripDropDown.SetVisibleCore(false), which still
        // touches the menu's HWND afterwards. Keep the menu alive for this form.
        itemMenu.Closed += (_,_)=> {
            configuring=false;
            if(!forceExit)ShortcutSuppression?.Invoke(false);
        };
        Deactivate += (_, _) => {
            if(forceExit||presenting)return;
            if(committing){CheckCommit();return;}
            if(!Visible||configuring||InspectionMode)return;
            // A maximized game may immediately reclaim foreground after
            // Show(). Don't close the switcher and lose the user's key-up.
            nint foreground=Native.GetForegroundWindow();
            // While the real Alt key is held, foreground churn from a game
            // (especially after Alt+Enter) must not silently kill our session.
            // Pinned sessions still dismiss when focus genuinely leaves.
            bool altHeld=AltPhysicallyHeld();
            bool recent=lastNavigationTick!=0&&Stopwatch.GetElapsedTime(lastNavigationTick).TotalMilliseconds<220;
            if(ShouldRetainOnDeactivate(persistent,altHeld,
                ShouldKeepOverlayOnGameFocus(foreground,origin),recent)){
                BeginInvoke((Action)(()=>{
                    if(Visible&&!committing&&!configuring)ReassertOverlayZOrder();
                }));
                return;
            }
            Dismiss(false);
        };
        visualTimer.Tick+=(_,_)=>{visualTimer.Stop();if(!committing)ApplyTargetVisuals();};
        commitTimer.Tick+=(_,_)=>CheckCommit();
        MouseWheel += (_, e) => {
            // Wheel is navigation only: it must NOT convert an ordinary Alt+Tab
            // session to pinned mode. The last key-up still commits selection.
            if(!Visible||configuring||committing)return;
            if(entries.Count>0)MoveSelection(e.Delta>0?-1:1);
        };
        refreshUi.Tick += (_, _) => {
            if(Visible&&!configuring&&!committing&&!InspectionMode&&
               ShouldRetainOnDeactivate(persistent,AltPhysicallyHeld(),
                   ShouldKeepOverlayOnGameFocus(Native.GetForegroundWindow(),origin),
                   lastNavigationTick!=0&&Stopwatch.GetElapsedTime(lastNavigationTick).TotalMilliseconds<220))
                ReassertOverlayZOrder();
            if (!Visible || configuring || !catalogDirty) return;
            catalogDirty = false;
            // Refresh membership/title without reordering a selection while the user navigates.
            var fresh = catalog.Snapshot;
            var keep = snapshot.Where(w => fresh.Any(f => f.Handle == w.Handle)).Select(w => fresh.First(f => f.Handle == w.Handle) with {Order=w.Order}).ToList();
            keep.AddRange(fresh.Where(w => keep.All(k => k.Handle != w.Handle)));
            snapshot = keep.Select((w,i)=>w with {Order=i}).ToList(); Rebuild(true);
        };
        catalog.Updated += OnCatalogUpdated;
    }
    private void OnCatalogUpdated() { catalogDirty = true; }
    internal static bool ShouldKeepOverlayOnGameFocus(nint foreground,nint origin)
        =>foreground!=0&&origin!=0&&WindowIdentity.Same(foreground,origin);
    internal static bool ShouldRetainOnDeactivate(bool pinned,bool altHeld,bool originForeground,bool recentNavigation)
        =>originForeground||(!pinned&&(altHeld||recentNavigation));
    internal static bool ShouldAvoidGamePeek(WindowInfo window)
        =>window.ProcessName.Equals("MapleStory",StringComparison.OrdinalIgnoreCase)
           ||window.AppName.Equals("MapleStory",StringComparison.OrdinalIgnoreCase);
    private static bool AltPhysicallyHeld()
        =>(Native.GetAsyncKeyState(0x12)&0x8000)!=0 ||
          (Native.GetAsyncKeyState(0xA4)&0x8000)!=0 ||
          (Native.GetAsyncKeyState(0xA5)&0x8000)!=0;
    internal readonly record struct PreviewSidebar(float ImageHeight,float AppY,float TitleY,float TitleHeight,float ButtonY);
    internal static PreviewSidebar LayoutPreviewSidebar(float height){
        // Fixed footer button; never paint a multi-line title beneath it.
        float imageHeight=Math.Clamp(Math.Min(247,height-456),48,247);
        float appY=229+16+imageHeight+20;
        float titleY=appY+35;
        float buttonY=height-125;
        float titleHeight=Math.Clamp(buttonY-titleY-12,0,63);
        return new PreviewSidebar(imageHeight,appY,titleY,titleHeight,buttonY);
    }
    private void ReassertOverlayZOrder(){
        if(!Visible||IsDisposed||InspectionMode)return;
        if(!KeepOverlayAboveGame(Handle))
            AppLog.Write("overlay-topmost-failed",new{win32=System.Runtime.InteropServices.Marshal.GetLastWin32Error()});
    }
    internal static bool KeepOverlayAboveGame(nint hwnd){
        // HWND_TOPMOST without stealing keyboard focus, resizing or moving.
        const uint flags=0x0001|0x0002|0x0010|0x0040;
        return Native.SetWindowPos(hwnd,(nint)(-1),0,0,0,0,flags);
    }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; if(!InspectionMode)p.ExStyle |= 0x80; return p; } }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        int rounded = 2; Native.DwmSetWindowAttribute(Handle, 33, ref rounded, 4);
    }
    protected override void WndProc(ref Message m) {
        // Releasing Alt must not enter the invisible native system menu.
        if(m.Msg==0x112&&((long)m.WParam&0xFFF0)==0xF100)return;
        base.WndProc(ref m);
    }
    public void TrackForeground(nint hwnd,uint time) {
        if (hwnd == 0) return;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return;
        recency.Record(WindowIdentity.Root(hwnd),pid,time);
        if (!catalog.Snapshot.Any(w => WindowIdentity.Same(w.Handle,hwnd))) catalog.RefreshSoon();
    }
    public void TrackForeground(nint hwnd)=>TrackForeground(hwnd,unchecked((uint)Environment.TickCount));
    public void OpenSwitcher(bool pinned, int direction = 1, bool sameApp = false) {
        if(committing)return;
        lastNavigationTick=Stopwatch.GetTimestamp();
        // Each Alt+Tab gesture selects its own lifetime. A previously pinned UI
        // must not prevent a new plain gesture from committing on key release.
        if (Visible) { persistent = pinned; releaseTimestamp=0;if(!pinned)ActiveControl=null;ReassertOverlayZOrder();if(!WindowIdentity.Same(Native.GetForegroundWindow(),Handle))activation.Request(Handle); MoveSelection(direction); return; }
        opening = Stopwatch.StartNew();
        PrepareSwitcher(pinned,direction,sameApp);
        ApplyBounds();EnsurePage(); ApplyTheme();
        presenting=true;
        try{Show(); Native.ShowWindow(Handle,5); ReassertOverlayZOrder(); activation.Request(Handle);}
        finally{presenting=false;}
        if (pinned) search.Focus(); else ActiveControl = null;
        refreshUi.Start();Invalidate();catalog.RefreshSoon();
        AppLog.Write("switch-open",new{origin=origin.ToInt64(),root=WindowIdentity.Root(origin).ToInt64(),originMaximized=Native.IsWindow(origin)&&Native.IsZoomed(origin),originTopmost=origin!=0&&((long)Native.GetWindowLongPtr(origin,-20)&8)!=0,selected=selected<entries.Count?entries[selected].Window.Handle.ToInt64():0});
    }
    private void PrepareSwitcher(bool pinned,int direction,bool sameApp) {
        origin = Native.GetForegroundWindow(); monitor = Native.MonitorFromWindow(origin, 2);
        TrackForeground(origin);
        // Filter hidden/closed branch windows without a blocking full enumeration.
        snapshot = catalog.Snapshot.Where(w=>Native.IsWindow(w.Handle)&&Native.IsWindowVisible(w.Handle)).OrderByDescending(w => recency.Rank(w.Handle)).ThenBy(w => w.Order).Select((w,i)=>w with {Order=i}).ToList();
        group = sameApp ? snapshot.FirstOrDefault(w => WindowIdentity.Same(w.Handle,origin))?.GroupKey : null;
        persistent = pinned;
        updating = true; search.Clear(); updating = false;
        selected = page = 0;
        Rebuild(false);
        selected=WindowCatalog.InitialSelection(entries,snapshot,origin,pinned,direction);
    }
    public void QuickSwitch(IReadOnlyList<ShortcutInput> gesture) {
        InputTimestamp=gesture[0].Timestamp;
        var first=gesture[0].Command;
        PrepareSwitcher(false,first is SwitchCommand.Previous or SwitchCommand.SameAppPrevious?-1:1,first is SwitchCommand.SameApp or SwitchCommand.SameAppPrevious);
        for(int i=1;i<gesture.Count-1;i++)selected=Mod(selected+(gesture[i].Command is SwitchCommand.Previous or SwitchCommand.SameAppPrevious?-1:1),entries.Count);
        releaseTimestamp=gesture[^1].Timestamp;
        Choose();
    }
    private void ApplyBounds() {
        var screen=prefs.OnPrimaryMonitor?(Screen.PrimaryScreen??Screen.FromHandle(origin)):Screen.FromHandle(origin);
        var screenMonitor=Native.MonitorFromPoint(new Native.Point{X=screen.Bounds.Left+1,Y=screen.Bounds.Top+1},2);
        uint dpi=(uint)DeviceDpi;
        if(Native.GetDpiForMonitor(screenMonitor,0,out uint monitorDpi,out _)==0)dpi=monitorDpi;
        scale = Math.Clamp(dpi / 96f * prefs.UiScale / 100f, .8f, 3f);
        var work = screen.WorkingArea;
        listLayout=LayoutPlanner.Plan(entries.Count,(work.Width-48)/scale,(work.Height-64)/scale,prefs.ShowPreview);
        tileLayout=LayoutPlanner.PlanTiles(entries.Count,(work.Width-48)/scale,(work.Height-64)/scale,prefs.View=="아이콘");
        int width=prefs.View=="목록"?D(listLayout.Width):D(tileLayout.Width);
        int height=prefs.View=="목록"?D(listLayout.Height):D(tileLayout.Height);
        if (width / scale < 900 || height / scale < 580) scale = Math.Min(width / 900f, height / 580f);
        Bounds = new Rectangle(work.Left + (work.Width-width)/2, work.Top+(work.Height-height)/2, width, height);
        search.Bounds = R(64, 92, W - 142, 29);
        search.Font=F(15);
        RecalculatePageSize();
    }
    private void ApplyTheme() { BackColor = Bg; search.BackColor = Surface; search.ForeColor = Ink; int dark = Palette.Light ? 0 : 1; Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4); }
    private static int Mod(int x, int n) => n == 0 ? 0 : (x % n + n) % n;
    private void RecalculatePageSize() {
        content = R(28, 229, W - 56, H - 300);
        if (prefs.View == "목록") { columns=1;pageSize=listLayout.Capacity; }
        else {columns=tileLayout.Columns;pageSize=tileLayout.Capacity;}
    }
    private void Rebuild(bool keep) {
        int beforeCount=entries.Count;
        nint prev = keep && selected < entries.Count ? entries[selected].Window.Handle : 0;
        // Preserve the real selected document across title/catalog refresh,
        // even when that grouped row is not the current highlighted row.
        var representatives=keep?entries.Where(e=>e.IsGroup)
            .ToDictionary(e=>e.Window.GroupKey,e=>e.Window.Handle):null;
        entries = WindowCatalog.Build(snapshot, prefs, search.Text, group, monitor);
        if(representatives!=null)foreach(var entry in entries)
            if(representatives.TryGetValue(entry.Window.GroupKey,out var hwnd))entry.SelectWindow(hwnd);
        if(prev!=0){
            int index=entries.FindIndex(e=>e.SelectWindow(prev));
            if(index>=0)selected=index;
        }
        if(Visible&&beforeCount!=entries.Count&&search.TextLength==0)ApplyBounds();
        selected = Math.Clamp(selected, 0, Math.Max(0, entries.Count-1)); EnsurePage();
        UpdateAura();
        Invalidate();
    }
    private void EnsurePage() { page = selected / Math.Max(1, pageSize); }
    public void MoveSelection(int offset) { if (entries.Count == 0) return; lastNavigationTick=Stopwatch.GetTimestamp(); selected = Mod(selected+offset, entries.Count); EnsurePage(); UpdateAura(); Invalidate(); }
    private void UpdateAura(){
        visualTimer.Stop();
        if(!Visible||configuring||committing||selected>=entries.Count||
           ShouldAvoidGamePeek(entries[selected].Window)){HideTargetVisuals();return;}
        // A timer is lower priority than queued keyboard commands. Rapid Tabs
        // coalesce to the last target; Alt-up never waits for another FX repaint.
        visualTimer.Start();
    }
    private void ApplyTargetVisuals(){
        if(!Visible||configuring||selected>=entries.Count){HideTargetVisuals();return;}
        var selectedWindow=entries[selected].Window;
        // DO NOT promote MapleStory into the chooser's topmost band just
        // because its row becomes selected. Alt+Enter/fullscreen games can
        // take over the compositor for one or two subsequent Alt+Tab cycles.
        // DWM thumbnail inside the chooser still works; only external game
        // Peek/Dim/Aura surfaces are suppressed.
        if(ShouldAvoidGamePeek(selectedWindow)){HideTargetVisuals();return;}
        nint hwnd=selectedWindow.Handle;
        if(prefs.PeekTarget)peek.Select(hwnd,Handle);else peek.Restore();
        if(prefs.EmphasizeTarget)dimmer.Select(hwnd,Handle);else dimmer.Hide();
        if(prefs.HighlightTarget)aura.Select(hwnd,Handle,Accent,prefs.EmphasizeTarget);else aura.Hide();
    }
    private void HideTargetVisuals(){visualTimer.Stop();aura.Hide();dimmer.Hide();peek.Restore();}
    public void NavigationReleased(long timestamp=0) { releaseTimestamp=persistent?0:timestamp; if (Visible && !persistent && !configuring && !committing) Choose(); }
    public void PinSelection(){if(Visible&&!configuring&&!committing){persistent=true;releaseTimestamp=0;Invalidate();}}
    public void TogglePersistent() { if (Visible) Dismiss(true); else OpenSwitcher(true, 0); }
    public void Dismiss(bool restore) {
        bool wasCommitting=committing;
        activation.Cancel();
        commitTimer.Stop();committing=false;
        refreshUi.Stop(); opening=null; lastNavigationTick=0; HideTargetVisuals(); ClearThumbnails(); Hide(); persistent=false;
        if (restore && Native.IsWindow(origin)) activation.Request(origin);
        if(wasCommitting)CommitFinished?.Invoke();
    }
    private void Choose() {
        if (selected >= entries.Count) { Dismiss(true); return; }
        var target = entries[selected].Window;
        committing=true;commitTarget=target.Handle;commitStarted=Stopwatch.GetTimestamp();
        requestAccepted=false;
        // Leave the preview band's temporary ordering in place until the
        // chosen HWND is REALLY foreground. Otherwise a topmost game is
        // restored over the target before the activation request finishes.
        visualTimer.Stop();aura.Hide();dimmer.Hide();
        bool success=activateTarget(target.Handle);
        requestAccepted=success;
        if(!committing)return; // Deactivate can confirm synchronously.
        if(!nativeActivation){FinishCommit(success);return;}
        CheckCommit();if(committing)commitTimer.Start();
    }
    private void CheckCommit() {
        if(!committing)return;
        nint foreground=Native.GetForegroundWindow();
        if(WindowIdentity.Same(foreground,commitTarget)){FinishCommit(true);return;}
        Native.GetWindowThreadProcessId(foreground,out uint pid);
        bool external=foreground!=0&&pid!=Environment.ProcessId&&!WindowIdentity.Same(foreground,origin);
        if(external||!Native.IsWindow(commitTarget)||Stopwatch.GetElapsedTime(commitStarted).TotalMilliseconds>=CommitDeadlineMs){FinishCommit(false);return;}
        if(!requestAccepted)requestAccepted=activateTarget(commitTarget);
    }
    private void FinishCommit(bool confirmed) {
        if(!committing)return;
        var data=new {hwnd=commitTarget.ToInt64(),origin=origin.ToInt64(),foreground=Native.GetForegroundWindow().ToInt64(),inputMs=InputTimestamp>0?Math.Round(Stopwatch.GetElapsedTime(InputTimestamp).TotalMilliseconds,2):0,releaseToSwitchMs=releaseTimestamp>0?Math.Round(Stopwatch.GetElapsedTime(releaseTimestamp).TotalMilliseconds,2):0,requestToSwitchMs=Math.Round(Stopwatch.GetElapsedTime(commitStarted).TotalMilliseconds,2)};
        // Confirm before demoting the temporary game/topmost preview band.
        if(confirmed)peek.Accept();else peek.Restore();
        if(confirmed&&nativeActivation)TrackForeground(Native.GetForegroundWindow());
        Dismiss(false);
        AppLog.Write(confirmed?"activated":"activate-unconfirmed",data);
        InputTimestamp=releaseTimestamp=0;
    }
    private void ExpandGroup() {
        if(selected>=entries.Count||!entries[selected].IsGroup)return;
        nint target=entries[selected].Window.Handle;
        group=entries[selected].Window.GroupKey;selected=page=0;
        updating=true;search.Clear();updating=false;Rebuild(false);
        RestoreSelection(target);
    }
    private void GoBack() {
        nint target=selected<entries.Count?entries[selected].Window.Handle:0;
        string? previous=group;group=null;selected=page=0;Rebuild(false);
        if(RestoreSelection(target))return;
        int index=entries.FindIndex(x=>x.Window.GroupKey==previous);
        if(index>=0){selected=index;EnsurePage();UpdateAura();Invalidate();}
    }
    private bool RestoreSelection(nint target) {
        int index=entries.FindIndex(e=>e.SelectWindow(target));
        if(index<0)return false;
        selected=index;EnsurePage();UpdateAura();Invalidate();return true;
    }
    private void SetView(string mode) { persistent=true; prefs.View=mode; SavePreferences(); ClearThumbnails(); ApplyBounds(); EnsurePage(); Invalidate(); }
    public void DetailedNavigate(Keys key) {
        if(!Visible||configuring)return;
        NavigateArrow(key);
    }
    private void NavigateArrow(Keys key) {
        int delta;
        if(prefs.View=="목록")delta=key==Keys.Up?-1:key==Keys.Down?1:key==Keys.Left?-(listLayout.Columns>1?listLayout.Rows:1):(listLayout.Columns>1?listLayout.Rows:1);
        else delta=key==Keys.Up?-columns:key==Keys.Down?columns:key==Keys.Left?-1:1;
        MoveSelection(delta);Invalidate();
    }
    private void SavePreferences() { try { prefs.Save(); PreferencesChanged?.Invoke(); } catch (Exception ex) { AppLog.Write("settings-save-failed", new {ex.Message}); } }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        Keys key = keyData & Keys.KeyCode; bool ctrl=(keyData & Keys.Control)!=0, shift=(keyData & Keys.Shift)!=0;
        if (key == Keys.Escape) {
            Dismiss(true); return true;
        }
        if (key == Keys.Enter) { if (selected<entries.Count&&entries[selected].IsGroup) ExpandGroup(); else Choose(); return true; }
        if (key == Keys.Back && search.TextLength==0 && !ctrl) { if(group!=null)GoBack(); return true; }
        if (key == Keys.Home && search.TextLength==0) { selected=0;EnsurePage();UpdateAura();Invalidate();return true; }
        if (key == Keys.End && search.TextLength==0) { selected=Math.Max(0,entries.Count-1);EnsurePage();UpdateAura();Invalidate();return true; }
        if (key == Keys.Tab) { if(ctrl&&(keyData&Keys.Alt)!=0)PinSelection();MoveSelection(shift ? -1 : 1); return true; }
        if(key is Keys.Up or Keys.Down or Keys.Left or Keys.Right) {
            if((key is Keys.Left or Keys.Right)&&search.Focused&&search.TextLength>0){Invalidate();return base.ProcessCmdKey(ref msg,keyData);}
            NavigateArrow(key);return true;
        }
        if (key == Keys.PageDown) { MoveSelection(pageSize); return true; }
        if (key == Keys.PageUp) { MoveSelection(-pageSize); return true; }
        if (ctrl && key is Keys.D1 or Keys.D2 or Keys.D3) { SetView(key == Keys.D1 ? "목록" : key == Keys.D2 ? "카드" : "아이콘"); return true; }
        if (ctrl && key == Keys.F) { persistent=true; search.Focus(); search.SelectAll(); Invalidate(); return true; }
        if (ctrl && key == Keys.P) { TogglePin(); return true; }
        if (key == Keys.F5) { catalog.RefreshSoon(); return true; }
        if (key == Keys.F2) { ShowSettings(); return true; }
        // Printable input is routed to a real edit control so the Korean IME works normally.
        if (!search.Focused && (keyData & Keys.Alt) == 0 && !ctrl && key is >= Keys.D0 and <= Keys.Z) { persistent=true; search.Focus(); }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (!forceExit) { e.Cancel=true; Dismiss(true); } base.OnFormClosing(e); }
    public void Shutdown() {
        if(forceExit)return;
        forceExit=true;
        // Close must finish before Dispose; never dispose from Closed itself.
        itemMenu.Close(); itemMenu.Dispose();
        catalog.Updated-=OnCatalogUpdated;activation.Dispose(); HideTargetVisuals(); ClearThumbnails(); refreshUi.Dispose();visualTimer.Dispose();commitTimer.Dispose(); toolTip.Dispose(); Close(); aura.Dispose();dimmer.Dispose();peek.Dispose(); foreach(var font in fonts.Values)font.Dispose(); fonts.Clear(); brandIcon.Dispose();
    }
    private void TogglePin() {
        if (selected >= entries.Count) return; string key=entries[selected].Window.ProcessName;
        int index=prefs.Pinned.FindIndex(x=>x.Equals(key,StringComparison.OrdinalIgnoreCase)); if(index>=0)prefs.Pinned.RemoveAt(index);else prefs.Pinned.Add(key); SavePreferences(); Rebuild(true);
    }
    private ContextMenuStrip ShowItemMenu(int index, Point point) {
        if(itemMenu.Visible)itemMenu.Close();
        foreach(ToolStripItem previous in itemMenu.Items.Cast<ToolStripItem>().ToArray())previous.Dispose();
        selected=index; Invalidate(); persistent=true;
        var item=entries[index]; var menu=itemMenu;
        menu.Renderer=new FlowMenuRenderer(Palette);menu.BackColor=Surface;menu.ForeColor=Ink;
        menu.Items.Add("이 창으로 전환", null, (_,_)=>Choose());
        if(item.IsGroup) menu.Items.Add($"{item.Windows.Count}개 창 펼치기",null,(_,_)=>ExpandGroup());
        menu.Items.Add(prefs.Pinned.Contains(item.Window.ProcessName,StringComparer.OrdinalIgnoreCase) ? "상단 정렬 해제" : "상단 정렬에 등록",null,(_,_)=>TogglePin());
        menu.Items.Add("이 앱을 목록에서 숨기기",null,(_,_)=> { prefs.Excluded.Add(item.Window.ProcessName); SavePreferences(); Rebuild(false); });
        configuring=true; ShortcutSuppression?.Invoke(true); menu.Show(this, point);
        return menu;
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if(e.Button==MouseButtons.Right) {
            int i=HitEntry(e.Location); if(i>=0) ShowItemMenu(i,e.Location); return;
        }
        if(e.Button!=MouseButtons.Left) return;
        foreach(var hit in hits.AsEnumerable().Reverse()) if(hit.Rect.Contains(e.Location)) { pressedRect=hit.Rect;pressedAction=hit.Action;Capture=true;Invalidate(pressedRect);return; }
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;var action=pressedAction;var rect=pressedRect;pressedAction=null;pressedRect=Rectangle.Empty;Capture=false;Invalidate(rect);if(rect.Contains(e.Location))action?.Invoke();}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){pressedAction=null;var old=pressedRect;pressedRect=Rectangle.Empty;if(!old.IsEmpty)Invalidate(old);}}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);var old=hoverRect;hoverRect=Rectangle.Empty;if(!old.IsEmpty)Invalidate(old);}
    private readonly Dictionary<int,Rectangle> entryRects=new();
    private int HitEntry(Point p) { foreach(var kv in entryRects) if(kv.Value.Contains(p)) return kv.Key; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e); string tip="";
        var next=Rectangle.Empty;
        foreach(var hit in hits.AsEnumerable().Reverse()) if(hit.Rect.Contains(e.Location)) { tip=hit.Tip;next=hit.Rect;break; }
        if(next!=hoverRect){var old=hoverRect;hoverRect=next;if(!old.IsEmpty)Invalidate(old);if(!next.IsEmpty)Invalidate(next);}
        Cursor=hits.Any(h=>h.Rect.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        if(tip!=lastTip) { toolTip.SetToolTip(this, tip); lastTip=tip; }
    }
    private Font F(float size,bool bold=false) {
        var key=(D(size),bold);
        if(!fonts.TryGetValue(key,out var font)){font=new Font("맑은 고딕",key.Item1,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);fonts[key]=font;}
        return font;
    }
    private void Txt(Graphics g,string text,Rectangle rect,float size,Color color,bool bold=false,bool wrap=false,TextFormatFlags extra=0) {
        var font=F(size,bold);
        TextRenderer.DrawText(g,text,font,rect,color,TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (wrap?TextFormatFlags.WordBreak:TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis) | extra);
    }
    private static GraphicsPath RoundPath(Rectangle r,int radius) {
        var p=new GraphicsPath(); int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        if(d<=0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
    }
    private void Box(Graphics g,Rectangle r,Color fill,int radius=12,Color? stroke=null) {
        using var path=RoundPath(r,D(radius)); using var brush=new SolidBrush(fill); g.FillPath(brush,path);
        if(stroke!=null) { using var pen=new Pen(stroke.Value,D(1)); g.DrawPath(pen,path); }
    }
    private void Button(Graphics g,string text,Rectangle rect,Action action,bool active=false,string tip="",bool quiet=false,bool primary=false) {
        var fill=primary?Accent:active?SelectedBg:quiet?Bg:Surface;
        if(rect==hoverRect||rect==pressedRect)fill=UiPaint.Blend(fill,rect==pressedRect?Ink:Accent,rect==pressedRect?.15f:.09f);
        Box(g,rect,fill,9,active?Accent:quiet&&rect!=hoverRect?null:UiPaint.Border(Palette));
        Txt(g,text,Rectangle.Inflate(rect,-D(8),0),13,primary?UiPaint.OnAccent(Palette):active?Accent:Ink,active||primary,false,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        hits.Add((rect,action,tip));
    }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Bg);
        hits.Clear(); entryRects.Clear(); desiredThumbs.Clear();
        g.DrawImage(brandIcon,R(23,20,46,46));
        Txt(g,"AltTabFlow",R(74,22,300,30),23,Ink,true);
        Txt(g,persistent?"상세 선택 · Enter 전환 / Esc 닫기":"빠른 전환 · 키를 모두 놓으면 전환",R(75,54,470,22),12,persistent?Accent:Muted);
        Button(g,"설정  F2",R(W-210,28,94,38),ShowSettings,false,"F2 · 보기, 단축키, 제외 목록",true);
        Button(g,"닫기  Esc",R(W-108,28,80,38),()=>Dismiss(true),false,"Esc · 원래 창으로 돌아가기",true);
        Box(g,R(28,84,W-56,47),Surface,12,search.Focused?Accent:UiPaint.Border(Palette));
        using(var pen=new Pen(Muted,D(1.8f))) { g.DrawEllipse(pen,D(42),D(99),D(11),D(11)); g.DrawLine(pen,D(51),D(108),D(56),D(113)); }
        if(search.Text.Length>0) Button(g,"×",R(W-65,92,28,28),()=>search.Clear());
        string[] modes={"목록","카드","아이콘"};
        Box(g,R(28,143,233,42),Surface,11);
        for(int i=0;i<modes.Length;i++) { string mode=modes[i]; Button(g,mode,R(32+i*76,147,73,34),()=>SetView(mode),prefs.View==mode,$"Ctrl+{i+1}"); }
        Button(g,prefs.GroupApps?"앱별 묶기  ✓":"앱별 묶기",R(277,145,132,38),()=> { persistent=true;prefs.GroupApps=!prefs.GroupApps; group=null; SavePreferences(); Rebuild(false); },prefs.GroupApps);
        if(prefs.View=="목록")Button(g,listLayout.Preview?"미리보기  ✓":"미리보기",R(419,145,110,38),()=> {persistent=true;prefs.ShowPreview=!prefs.ShowPreview;SavePreferences();ClearThumbnails();ApplyBounds();EnsurePage();Invalidate();},listLayout.Preview,"목록 옆에 미리보기 표시 · 화면이 좁으면 자동 접힘");
        int total=entries.Sum(x=>x.Windows.Count);
        Txt(g,$"{entries.Count}개 항목  ·  {total}개 창",R(W-275,155,247,24),13,Muted,false,false,TextFormatFlags.Right);
        if(group!=null) {
            Button(g,"‹ 전체 앱",R(28,194,95,26),GoBack,false,"Backspace · 앱 목록으로 돌아가기",true);
            Txt(g,snapshot.FirstOrDefault(w=>w.GroupKey==group)?.AppName??"앱의 창",R(137,197,W-300,24),13,Accent,true);
        } else Txt(g,search.Text.Length>0?"검색 결과 · 제목을 확인하고 바로 전환하세요":prefs.Pinned.Count>0?"상단 등록 앱 먼저 · 각 그룹 A~Z → 가나다순":"앱 이름순 · A~Z → 가나다 · Enter로 묶음 펼치기",R(28,199,W-56,24),12,Muted);
        if(entries.Count==0) {
            Txt(g,catalog.Snapshot.Length==0?"열린 창을 준비하고 있습니다":"일치하는 창이 없습니다",R(60,320,W-120,40),22,Ink,true,false,TextFormatFlags.HorizontalCenter);
            Txt(g,"검색어를 줄이거나 설정에서 숨긴 앱을 확인해 주세요.",R(60,367,W-120,30),14,Muted,false,false,TextFormatFlags.HorizontalCenter);
        } else if(prefs.View=="목록") PaintList(g); else PaintTiles(g);
        using(var pen=new Pen(Raised)) g.DrawLine(pen,D(28),D(H-61),D(W-28),D(H-61));
        Txt(g,persistent?"방향키 선택   Enter 펼치기 / 전환   Backspace 뒤로   Esc 닫기":"Tab·방향키 이동   키를 모두 놓으면 전환   Enter 펼치기   Esc 취소",R(28,H-43,W-180,28),12,Muted);
        int pages=Math.Max(1,(entries.Count+pageSize-1)/pageSize);
        if(pages>1){Button(g,"‹",R(W-149,H-47,29,29),()=>MovePage(-1),false,"이전 페이지");Txt(g,$"{page+1} / {pages}",R(W-115,H-42,52,25),12,Muted,false,false,TextFormatFlags.HorizontalCenter);Button(g,"›",R(W-57,H-47,29,29),()=>MovePage(1),false,"다음 페이지");}
        else Txt(g,"전체 목록",R(W-144,H-42,116,25),12,Accent,false,false,TextFormatFlags.Right);
        QueueThumbnails();
        if(opening!=null) {
            lastPaintMs=opening.Elapsed.TotalMilliseconds; paintSamples.Enqueue(lastPaintMs); if(paintSamples.Count>100) paintSamples.Dequeue(); opening=null;
            AppLog.Write("first-paint",new{milliseconds=Math.Round(lastPaintMs,2),keyToPaintMs=InputTimestamp>0?Math.Round(Stopwatch.GetElapsedTime(InputTimestamp).TotalMilliseconds,2):0,windows=snapshot.Count,view=prefs.View});
            // Only schedule the effect after the chooser has actually painted.
            UpdateAura();
        }
        base.OnPaint(e);
    }
    private void MovePage(int delta) { if(entries.Count==0) return; int pages=(entries.Count+pageSize-1)/pageSize; selected=Mod(page+delta,pages)*pageSize; EnsurePage(); UpdateAura(); Invalidate(); }
    private void PaintList(Graphics g) {
        float listWidth=listLayout.Preview?W-56-400:W-56;
        float width=(listWidth-(listLayout.Columns-1)*14)/listLayout.Columns;
        int start=page*pageSize;
        for(int i=start;i<Math.Min(entries.Count,start+pageSize);i++) {
            int index=i,local=i-start;var item=entries[i];var row=R(28+(local/listLayout.Rows)*(width+14),229+(local%listLayout.Rows)*listLayout.Pitch,width,listLayout.Pitch-8);
            bool active=i==selected;
            Box(g,row,active?SelectedBg:row==hoverRect?UiPaint.Blend(Surface,Accent,.055f):Surface,10,active?Accent:null);
            var icon=WindowCatalog.CachedIcon(item.Window);
            g.DrawImage(icon,new Rectangle(row.X+D(15),row.Y+(row.Height-D(32))/2,D(32),D(32)));
            bool pin=prefs.Pinned.Contains(item.Window.ProcessName,StringComparer.OrdinalIgnoreCase);
            Txt(g,(pin?"★  ":"")+item.Window.AppName,new Rectangle(row.X+D(61),row.Y+D(7),row.Width-D(item.IsGroup?150:80),D(23)),14,active?Accent:Ink,true);
            Txt(g,(item.Window.Minimized?"최소화 · 선택 시 복원 | ":"")+item.Window.Title,new Rectangle(row.X+D(61),row.Y+D(listLayout.Pitch<70?30:35),row.Width-D(76),D(22)),13,Muted);
            entryRects[i]=row;
            hits.Add((row,()=> { selected=index; Choose(); },item.Window.Title));
            if(item.IsGroup) Button(g,$"{item.Windows.Count}개  ›",new Rectangle(row.Right-D(81),row.Y+D(6),D(66),D(25)),()=> { selected=index; ExpandGroup(); },active,"앱의 모든 창 제목 보기");
        }
        if(!listLayout.Preview||selected>=entries.Count) return;
        var selectedEntry=entries[selected]; var w=selectedEntry.Window;
        float px=28+listWidth+20, pw=W-28-px, py=229;
        Box(g,R(px,py,pw,H-300),Surface,12);
        var sidebar=LayoutPreviewSidebar(H);
        var preview=R(px+16,py+16,pw-32,sidebar.ImageHeight);
        PaintPreview(g,w,preview);
        Txt(g,w.AppName,R(px+20,sidebar.AppY,pw-40,25),16,Ink,true);
        // Previous "선택한 창 미리보기" footer was drawn INSIDE this title.
        // The preview already labels itself; remove redundant footer text.
        // Clip the window title above the action button at all DPI/UI sizes.
        if(sidebar.TitleHeight>=12){
            string subtitle=w.Minimized?"최소화된 창 · 선택하면 복원 | "+w.Title:w.Title;
            Txt(g,subtitle,R(px+20,sidebar.TitleY,pw-40,sidebar.TitleHeight),
                14,Muted,false,sidebar.TitleHeight>=29);
        }
        Button(g,"이 창으로 전환  ↵",R(px+20,sidebar.ButtonY,pw-40,40),Choose,false,"",false,true);
    }
    private void PaintTiles(Graphics g) {
        int start=page*pageSize; float gap=12, tw=(W-56-(columns-1)*gap)/columns;
        float th=prefs.View=="아이콘"?146:204;
        for(int i=start;i<Math.Min(entries.Count,start+pageSize);i++) {
            int index=i, local=i-start; var item=entries[i]; float x=28+(local%columns)*(tw+gap), y=229+(local/columns)*(th+12);
            var rect=R(x,y,tw,th); bool active=i==selected;
            Box(g,rect,active?SelectedBg:Surface,12,active?Accent:null);
            if(prefs.View=="카드") {
                PaintPreview(g,item.Window,R(x+12,y+12,tw-24,118));
                g.DrawImage(WindowCatalog.CachedIcon(item.Window),R(x+14,y+144,24,24));
                Txt(g,item.Window.AppName,R(x+46,y+145,tw-105,23),13,Ink,true);
                Txt(g,item.Window.Title,R(x+14,y+177,tw-28,22),12,Muted);
            } else {
                g.DrawImage(WindowCatalog.CachedIcon(item.Window),R(x+(tw-40)/2,y+18,40,40));
                Txt(g,item.Window.AppName,R(x+10,y+72,tw-20,24),13,Ink,true,false,TextFormatFlags.HorizontalCenter);
                Txt(g,item.Window.Title,R(x+12,y+104,tw-24,24),11,Muted,false,false,TextFormatFlags.HorizontalCenter);
            }
            entryRects[i]=rect; hits.Add((rect,()=> { selected=index; Choose(); },item.Window.Title));
            if(item.IsGroup) Button(g,$"{item.Windows.Count} ›",R(x+tw-56,y+6,48,27),()=> { selected=index; ExpandGroup(); },active,"모든 창 제목 펼치기");
        }
    }
    private void PaintPreview(Graphics g,WindowInfo w,Rectangle rect) {
        Box(g,rect,Raised,8);
        var icon=WindowCatalog.CachedIcon(w);
        g.DrawImage(icon,new Rectangle(rect.X+(rect.Width-D(38))/2,rect.Y+(rect.Height-D(50))/2,D(38),D(38)));
        Txt(g,w.Minimized?"최소화된 창":"미리보기",new Rectangle(rect.X,rect.Bottom-D(25),rect.Width,D(20)),11,Muted,false,false,TextFormatFlags.HorizontalCenter);
        if(!w.Minimized) desiredThumbs.Add((w,rect));
    }
    private void QueueThumbnails() {
        if(thumbnailQueued||!Visible) return;
        string signature=string.Join(";",desiredThumbs.Select(t=>$"{t.Window.Handle}:{t.Rect}"));
        if(signature==thumbnailSignature) return;
        thumbnailQueued=true;
        BeginInvoke((Action)(()=> {
            thumbnailQueued=false; ClearThumbnails(false);
            if(!Visible||committing) return;
            var wanted=desiredThumbs.ToArray();
            thumbnailSignature=string.Join(";",wanted.Select(t=>$"{t.Window.Handle}:{t.Rect}"));
            foreach(var t in wanted) {
                if(Native.DwmRegisterThumbnail(Handle,t.Window.Handle,out nint thumb)!=0) continue;
                thumbnails.Add(thumb);
                if(Native.DwmQueryThumbnailSourceSize(thumb,out var size)!=0||size.Width<=0||size.Height<=0) continue;
                float fit=Math.Min((float)t.Rect.Width/size.Width,(float)t.Rect.Height/size.Height);
                int w=(int)(size.Width*fit),h=(int)(size.Height*fit);
                var dest=new Rectangle(t.Rect.X+(t.Rect.Width-w)/2,t.Rect.Y+(t.Rect.Height-h)/2,w,h);
                var p=new Native.ThumbnailProperties { Flags=0x1|0x4|0x8|0x10,Destination=new Native.Rect(dest),Opacity=255,Visible=true,ClientOnly=false };
                Native.DwmUpdateThumbnailProperties(thumb,ref p);
                if(InspectionMode) AppLog.Write("thumbnail",new{dest=dest.ToString(),sourceSize=$"{size.Width}x{size.Height}",structSize=System.Runtime.InteropServices.Marshal.SizeOf<Native.ThumbnailProperties>(),bounds=Bounds.ToString(),dpi=DeviceDpi});
            }
        }));
    }
    private void ClearThumbnails(bool reset=true) { foreach(var h in thumbnails) Native.DwmUnregisterThumbnail(h); thumbnails.Clear(); if(reset)thumbnailSignature=""; }
    public void ShowSettings() {
        persistent=true; configuring=true; HideTargetVisuals(); ShortcutSuppression?.Invoke(true); ClearThumbnails();
        try {
            using var dialog=new SettingsForm(prefs,catalog.Snapshot){ExitForUpdate=ExitRequested};
            if(dialog.ShowDialog(this)==DialogResult.OK) { SavePreferences(); ApplyBounds(); ApplyTheme(); group=null; Rebuild(true); }
        } finally { configuring=false; ShortcutSuppression?.Invoke(false); if(Visible) { Activate(); UpdateAura(); Invalidate(); } }
    }
    public object DiagnosticState() => new {
        visible=Visible, nativeVisible=Native.IsWindowVisible(Handle), ownHwnd=Handle.ToInt64(), foreground=Native.GetForegroundWindow().ToInt64(),origin=origin.ToInt64(),committing, prefs.View, prefs.GroupApps, persistent, selected, page, pageSize,
        query=search.Text, group, selectedHwnd=selected<entries.Count?entries[selected].Window.Handle.ToInt64():0,
        totalWindows=snapshot.Count, totalEntries=entries.Count, thumbnails=thumbnails.Count,
        lastPaintMs, samples=paintSamples.ToArray(), refreshMs=catalog.LastRefreshMs,
        colorPreset=Palette.Id,prefs.StartWithWindows,prefs.HighlightTarget,prefs.EmphasizeTarget,prefs.PeekTarget,aura=aura.Statistics,peek=peek.Statistics,
        bounds=Bounds.ToString(),scale,listColumns=listLayout.Columns,listRows=listLayout.Rows,allItemsVisible=entries.Count<=pageSize,previewRects=desiredThumbs.Select(x=>x.Rect.ToString()).ToArray(),
        prefs.OnPrimaryMonitor,display=Screen.FromControl(this).DeviceName,onPrimary=Screen.FromControl(this).Primary,primaryBounds=Screen.PrimaryScreen?.Bounds.ToString(),
        entries=entries.Select(x=>new {app=x.Window.AppName,title=x.Window.Title,count=x.Windows.Count,hwnd=x.Window.Handle.ToInt64()}).ToArray()
    };
    // Exercise the real keyboard routing on an off-screen form without moving user windows.
    internal void PrepareNavigationTest(List<WindowInfo> windows,bool pinned=false){snapshot=windows;selected=page=0;group=null;persistent=pinned;Rebuild(false);ApplyBounds();}
    internal void TestInitialSelection(nint current,bool pinned=false,int direction=1){
        origin=current;persistent=pinned;selected=WindowCatalog.InitialSelection(entries,snapshot,current,pinned,direction);EnsurePage();
    }
    internal void TestRefresh(List<WindowInfo> windows){snapshot=windows;Rebuild(true);}
    internal bool TestKey(Keys key){var message=Message.Create(Handle,0x100,(nint)(int)key,0);return ProcessCmdKey(ref message,key);}
    internal void TestMouse(Point point,bool down){var e=new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0);if(down)OnMouseDown(e);else OnMouseUp(e);}
    internal void TestWheel(int delta)=>OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,Width/2,Height/2,delta));
    internal Rectangle TestEntryBounds(int index)=>entryRects[index];
    internal void TestActivate()=>activation.Request(Handle);
    internal ContextMenuStrip TestItemMenu(int index)=>ShowItemMenu(index,new Point(160,240));
    internal (string? Group,int Count,bool Persistent,nint Selected) NavigationState=>(group,entries.Count,persistent,selected<entries.Count?entries[selected].Window.Handle:0);
}
