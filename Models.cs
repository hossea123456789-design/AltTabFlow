using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Globalization;

namespace AltTabFlow;

internal sealed class Preferences
{
    public string View { get; set; } = "목록";
    public bool GroupApps { get; set; } = true;
    public bool ReplaceAltTab { get; set; } = true;
    public bool CurrentMonitorOnly { get; set; }
    public bool OnPrimaryMonitor { get; set; } = true;
    public bool ShowPreview { get; set; } = false;
    public bool LightTheme { get; set; }
    public string? ColorPreset { get; set; }
    public bool HighlightTarget { get; set; } = true;
    public bool EmphasizeTarget { get; set; } = true;
    public bool PeekTarget { get; set; } = true;
    public bool? StartWithWindows { get; set; }
    public bool AutoStartByDefault => StartWithWindows ?? true;
    public int UiScale { get; set; } = 100;
    public List<string> Pinned { get; set; } = new();
    public List<string> Excluded { get; set; } = new();
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AltTabFlow");
    public static string FilePath => Path.Combine(DataDirectory, "settings.json");
    public static Preferences Load() {
        try {
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new();
            if (p.View != "목록" && p.View != "카드" && p.View != "아이콘") p.View = "목록";
            p.UiScale = Math.Clamp(p.UiScale, 85, 140); p.Pinned ??= new(); p.Excluded ??= new();
            p.Pinned=NormalizeApps(p.Pinned);p.Excluded=NormalizeApps(p.Excluded);
            return p;
        } catch { return new(); }
    }
    internal static List<string> NormalizeApps(IEnumerable<string> values) => values.Select(x=>x.Trim()).Where(x=>x.Length>0).Select(x=>x.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?x[..^4]:x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public void Save() {
        Directory.CreateDirectory(DataDirectory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public static bool StartupEnabled {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("AltTabFlow") != null; }
        set {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) key.SetValue("AltTabFlow", "\"" + Environment.ProcessPath + "\" --background");
            else key.DeleteValue("AltTabFlow", false);
        }
    }
}

internal sealed record WindowInfo(nint Handle, string Title, string ProcessName, string AppName, string Path, int Order, bool Minimized)
{
    public string GroupKey => string.IsNullOrEmpty(Path) ? ProcessName.ToLowerInvariant() : Path.ToLowerInvariant();
}

internal sealed class Entry
{
    public required List<WindowInfo> Windows { get; init; }
    public WindowInfo Window => Windows[0];
    public bool IsGroup => Windows.Count > 1;
}

internal static class WindowCatalog
{
    private static readonly ConcurrentDictionary<string, Bitmap> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Bitmap FallbackIcon = SystemIcons.Application.ToBitmap();
    public static Bitmap CachedIcon(WindowInfo w) => Icons.GetValueOrDefault(w.GroupKey, FallbackIcon);
    public static Bitmap? CachedAppIcon(WindowInfo w)=>Icons.GetValueOrDefault(w.GroupKey);
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) {
        ["EXCEL"]="Excel", ["chrome"]="Chrome", ["msedge"]="Edge", ["explorer"]="파일 탐색기",
        ["slack"]="Slack", ["olk"]="Outlook", ["OUTLOOK"]="Outlook", ["ChatGPT"]="Codex / ChatGPT",
        ["claude"]="Claude", ["Code"]="Visual Studio Code", ["WINWORD"]="Word", ["POWERPNT"]="PowerPoint",
        ["SystemSettings"]="Windows 설정", ["notepad"]="메모장", ["p4v"]="Perforce", ["p4_toolbox"]="P4 도구모음",
        ["ApplicationFrameHost"]="Windows 앱", ["team_presence_pip"]="팀 출근 현황"
    };
    public static string AppName(string process) => Names.GetValueOrDefault(process,process);
    public static List<WindowInfo> Read() {
        var windows = new List<WindowInfo>();
        var processCache = new Dictionary<uint, (string Name, string Path)>();
        Native.EnumWindows((h, _) => {
            try {
                if (!Native.IsWindowVisible(h)) return true;
                Native.GetWindowThreadProcessId(h, out uint pid);
                if (pid == Environment.ProcessId || pid == 0) return true;
                var style = (long)Native.GetWindowLongPtr(h, -20);
                bool appWindow = (style & 0x40000) != 0;
                if (!appWindow && ((style & 0x80) != 0 || (style & 0x8000000) != 0)) return true;
                if (Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
                string title = Native.Title(h);
                if (string.IsNullOrWhiteSpace(title)) return true;
                string cls = Native.Class(h);
                if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow") return true;
                // Keep one visible representative for an owner/popup chain, like the native switcher.
                if (!appWindow) {
                    nint root = Native.GetAncestor(h, 3), last = root;
                    for (int i = 0; i < 8; i++) {
                        nint popup = Native.GetLastActivePopup(last);
                        if (popup == last || popup == 0) break;
                        last = popup;
                        if (Native.IsWindowVisible(last)) break;
                    }
                    if (last != h && Native.IsWindowVisible(last)) return true;
                }
                if (!processCache.TryGetValue(pid, out var proc)) {
                    using var p = Process.GetProcessById((int)pid);
                    string path = ""; try { path = p.MainModule?.FileName ?? ""; } catch { }
                    proc = (p.ProcessName, path); processCache[pid] = proc;
                }
                string name = Names.GetValueOrDefault(proc.Name, proc.Name);
                windows.Add(new(h, title, proc.Name, name, proc.Path, windows.Count, Native.IsIconic(h)));
            } catch { /* A window may disappear while enumerating. */ }
            return true;
        }, 0);
        return windows;
    }
    public static bool Matches(WindowInfo w, string query) {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string haystack = w.Title + " " + w.AppName + " " + w.ProcessName;
        return words.All(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
    public static List<Entry> Build(IEnumerable<WindowInfo> source, Preferences p, string query, string? group, nint monitor) {
        var windows = source.Where(w => !p.Excluded.Contains(w.ProcessName, StringComparer.OrdinalIgnoreCase));
        if (p.CurrentMonitorOnly && monitor != 0) windows = windows.Where(w => Native.MonitorFromWindow(w.Handle, 2) == monitor);
        if (group != null) windows = windows.Where(w => w.GroupKey == group);
        windows = windows.Where(w => Matches(w, query));
        List<Entry> entries;
        if (p.GroupApps && group == null && string.IsNullOrWhiteSpace(query))
            entries = windows.GroupBy(w => w.GroupKey).Select(g => new Entry { Windows = g.ToList() }).ToList();
        else entries = windows.Select(w => new Entry { Windows = new() { w } }).ToList();
        var pinned=p.Pinned.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries.OrderBy(e => pinned.Contains(e.Window.ProcessName)?0:1)
            .ThenBy(e=>e.Window.AppName,AppNameComparer.Instance)
            .ThenBy(e=>e.Window.GroupKey,StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Window.Order).ToList();
    }
    internal static int InitialSelection(IReadOnlyList<Entry> entries,IEnumerable<WindowInfo> recency,nint origin,bool pinned,int direction){
        int current=-1;for(int i=0;i<entries.Count;i++)if(entries[i].Windows.Any(x=>WindowIdentity.Same(x.Handle,origin))){current=i;break;}
        if(pinned||entries.Count<=1)return Math.Max(0,current);
        var candidates=recency.Select(w=>{for(int i=0;i<entries.Count;i++)if(entries[i].Windows.Any(x=>x.Handle==w.Handle))return i;return -1;}).Where(i=>i>=0&&i!=current).Distinct().ToList();
        if(candidates.Count==0)return Math.Max(0,current);
        return direction<0?candidates[^1]:candidates[0];
    }
    public static Bitmap GetIcon(WindowInfo w) {
        if (Icons.TryGetValue(w.GroupKey, out var cached)) return cached;
        Bitmap? bitmap = null;
        try {
            Native.SendMessageTimeout(w.Handle, 0x7F, 1, 0, 2, 35, out nint icon);
            if (icon == 0) icon = Native.GetClassLongPtr(w.Handle, -14);
            if (icon != 0) { using var owned = (Icon)Icon.FromHandle(icon).Clone(); bitmap = owned.ToBitmap(); }
            if (bitmap == null && File.Exists(w.Path)) { using var owned = Icon.ExtractAssociatedIcon(w.Path); bitmap = owned?.ToBitmap(); }
        } catch { }
        bitmap ??= SystemIcons.Application.ToBitmap();
        // Paint uses only this cache. Extraction runs on the catalog worker, never on Alt+Tab.
        // Keep a bounded cache for this process lifetime; do not dispose a bitmap being painted.
        if (Icons.Count < 160) Icons[w.GroupKey] = bitmap;
        else { bitmap.Dispose(); return FallbackIcon; }
        return bitmap;
    }
}

internal sealed class AppNameComparer : IComparer<string>
{
    public static readonly AppNameComparer Instance=new();
    private static readonly CompareInfo Korean=CultureInfo.GetCultureInfo("ko-KR").CompareInfo;
    private static int Kind(string name) {
        char c=name.Length>0?char.ToUpperInvariant(name[0]):'\0';
        if(c is >= 'A' and <= 'Z')return 0;
        if(c is >= '\uAC00' and <= '\uD7A3' or >= '\u1100' and <= '\u11FF' or >= '\u3130' and <= '\u318F')return 1;
        return char.IsDigit(c)?2:3;
    }
    public int Compare(string? a,string? b) {
        a=(a??"").Trim();b=(b??"").Trim();
        int category=Kind(a).CompareTo(Kind(b));
        return category!=0?category:Korean.Compare(a,b,CompareOptions.IgnoreCase|CompareOptions.IgnoreWidth);
    }
}

internal sealed class CatalogService : IDisposable
{
    private WindowInfo[] snapshot = Array.Empty<WindowInfo>();
    private readonly System.Threading.Timer timer;
    private int busy, disposed;
    public event Action? Updated;
    public WindowInfo[] Snapshot => Volatile.Read(ref snapshot);
    public double LastRefreshMs { get; private set; }
    public CatalogService() { timer = new System.Threading.Timer(_ => Refresh(), null, 0, 1800); }
    public void RefreshSoon() { if (Volatile.Read(ref disposed) == 0) try { timer.Change(60, 1800); } catch (ObjectDisposedException) { } }
    private void Refresh() {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref busy, 1) == 1) return;
        try {
            var sw = Stopwatch.StartNew();
            var rows = WindowCatalog.Read().ToArray();
            Volatile.Write(ref snapshot, rows);
            LastRefreshMs = sw.Elapsed.TotalMilliseconds;
            Updated?.Invoke();
            foreach (var row in rows.GroupBy(x => x.GroupKey).Select(x => x.First())) {
                if (Volatile.Read(ref disposed) != 0) break;
                WindowCatalog.GetIcon(row);
            }
            Updated?.Invoke();
        } catch { } finally { Volatile.Write(ref busy, 0); }
    }
    public void Dispose() { Interlocked.Exchange(ref disposed, 1); timer.Dispose(); }
}
