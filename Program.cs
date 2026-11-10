using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace AltTabFlow;

internal static class AppLog
{
    private static readonly object Gate=new();
    public static void Write(string evt,object data) {
        // Never block input or rendering for disk I/O.
        ThreadPool.QueueUserWorkItem(_=> {
            try { lock(Gate) { Directory.CreateDirectory(Preferences.DataDirectory); string path=Path.Combine(Preferences.DataDirectory,"events.jsonl"); if(File.Exists(path)&&new FileInfo(path).Length>1_000_000)File.Move(path,path+".previous",true); File.AppendAllText(path,JsonSerializer.Serialize(new{time=DateTimeOffset.Now,evt,data})+Environment.NewLine); } } catch { }
        });
    }
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>AppLog.Write("ui-error",new{e.Exception.Message,e.Exception.StackTrace});
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>AppLog.Write("fatal-error",new{error=e.ExceptionObject.ToString()});
        Directory.CreateDirectory(Preferences.DataDirectory);
        if(args.Contains("--self-test")) { SelfTests.Run(); return; }
        if(args.Contains("--peek-self-test")) { PeekTests.Run(); return; }
        if(args.Contains("--menu-self-test")) { MenuTests.Run(); return; }
        if(args.Length==2&&args[0]=="--peek-fixture") { PeekTests.RunFixture(args[1]); return; }
        if(args.Contains("--dump")) { File.WriteAllText(Path.Combine(Preferences.DataDirectory,"windows.json"),JsonSerializer.Serialize(WindowCatalog.Read().Select(x=>new{hwnd=x.Handle.ToInt64(),x.Title,x.AppName,x.ProcessName,x.Path,x.Minimized}),new JsonSerializerOptions{WriteIndented=true})); return; }
        using var mutex=new Mutex(true,@"Local\AltTabFlow.CurrentUser."+Environment.UserName,out bool first);
        if(!first) { SendCommand(args.Contains("--quit")?"quit":args.Contains("--status")?"status":args.Contains("--pause")?"pause":args.Contains("--hide")?"hide":"show"); return; }
        if(args.Contains("--quit")||args.Contains("--status")||args.Contains("--pause")||args.Contains("--hide")) return;
        bool running=false;
        try { using var context=new SwitcherContext(args); running=true; Application.Run(context); }
        catch(Exception ex) {
            AppLog.Write(running?"runtime-error":"startup-error",new{ex.Message,ex.StackTrace});
            MessageBox.Show((running?"AltTabFlow 실행 중 오류가 발생해 종료되었습니다.":"AltTabFlow를 시작하지 못했습니다.")+"\n"+ex.Message,"AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
    private static void SendCommand(string command) {
        try { using var pipe=new NamedPipeClientStream(".","AltTabFlow."+Environment.UserName,PipeDirection.Out); pipe.Connect(1800); using var writer=new StreamWriter(pipe); writer.WriteLine(command); writer.Flush(); } catch { }
    }
}

internal sealed class SwitcherContext : ApplicationContext
{
    private readonly Preferences prefs=Preferences.Load();
    private readonly CatalogService catalog=new();
    private readonly SwitcherForm form;
    private readonly NotifyIcon tray;
    private readonly KeyboardHook? keyboard;
    private readonly Native.WinEventProc foregroundCallback;
    private readonly nint foregroundHook;
    private readonly CancellationTokenSource cancellation=new();
    private bool closing,paused;
    private readonly Queue<object> shortcutHistory=new();
    private readonly ToolStripMenuItem pauseMenu;
    private readonly System.Windows.Forms.Timer startupTimer=new(){Interval=350};
    public SwitcherContext(string[] args) {
        try { Preferences.StartupEnabled=prefs.AutoStartByDefault; prefs.StartWithWindows=prefs.AutoStartByDefault; prefs.Save(); }
        catch(Exception ex){AppLog.Write("startup-registration-failed",new{ex.Message});}
        form=new(prefs,catalog);
        if(args.Contains("--preview")||args.Contains("--inspect")) { form.InspectionMode=true; form.ShowInTaskbar=true; }
        _=form.Handle;
        foregroundCallback=(_,_,hwnd,_,_,_,_)=>form.TrackForeground(hwnd);
        foregroundHook=Native.SetWinEventHook(3,3,0,foregroundCallback,0,0,2);
        form.PreferencesChanged=()=> { if(keyboard!=null)keyboard.Enabled=prefs.ReplaceAltTab&&!paused&&!form.IsConfiguring; };
        form.ShortcutSuppression=suppress=> { if(keyboard!=null)keyboard.Enabled=prefs.ReplaceAltTab&&!paused&&!suppress; };
        form.ExitRequested=Exit;
        var menu=new ContextMenuStrip();
        menu.Items.Add("AltTabFlow 열기    Ctrl+Alt+Space",null,(_,_)=>form.OpenSwitcher(true,0));
        menu.Items.Add("설정",null,(_,_)=> { if(!form.Visible)form.OpenSwitcher(true,0); form.ShowSettings(); });
        pauseMenu=new ToolStripMenuItem("Alt+Tab 대체 일시 중지",null,(_,_)=>TogglePause()); menu.Items.Add(pauseMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("완전히 종료 (기본 Alt+Tab 복원)",null,(_,_)=>Exit());
        tray=new NotifyIcon { Icon=CreateIcon(),Text="AltTabFlow · Ctrl+Alt+Space",Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=(_,_)=>form.OpenSwitcher(true,0);
        if(!args.Contains("--preview")) keyboard=new KeyboardHook(command=> { long stamp=Stopwatch.GetTimestamp(); if(!closing&&!form.IsDisposed)try{form.BeginInvoke((Action)(()=> {if(!form.Visible&&command is SwitchCommand.Next or SwitchCommand.Previous or SwitchCommand.SameApp or SwitchCommand.SameAppPrevious or SwitchCommand.Persistent)form.InputTimestamp=stamp;Handle(command);}));}catch(InvalidOperationException){} }) {Enabled=prefs.ReplaceAltTab};
        _=Listen();
        startupTimer.Tick+=(_,_)=> {
            startupTimer.Stop();
            if(!args.Contains("--background"))form.OpenSwitcher(true,0);
        }; startupTimer.Start();
        AppLog.Write("started",new{version=typeof(Program).Assembly.GetName().Version?.ToString(),preview=args.Contains("--preview"),pid=Environment.ProcessId});
    }
    private static Icon CreateIcon() {
        using var stream=typeof(Program).Assembly.GetManifestResourceStream("AltTabFlow.Assets.AltTabFlow.ico");
        using var icon=new Icon(stream!,32,32); return (Icon)icon.Clone();
    }
    private void Handle(SwitchCommand command) {
        if(closing||form.IsConfiguring) return;
        shortcutHistory.Enqueue(new{time=DateTimeOffset.Now,command=command.ToString(),visible=form.Visible,persistent=form.IsPersistent});while(shortcutHistory.Count>24)shortcutHistory.Dequeue();
        switch(command) {
            case SwitchCommand.Next: form.OpenSwitcher(false,1); break;
            case SwitchCommand.Previous: form.OpenSwitcher(false,-1); break;
            case SwitchCommand.PinnedNext: form.OpenSwitcher(true,1); break;
            case SwitchCommand.PinnedPrevious: form.OpenSwitcher(true,-1); break;
            case SwitchCommand.SameApp: form.OpenSwitcher(false,1,true); break;
            case SwitchCommand.SameAppPrevious: form.OpenSwitcher(false,-1,true); break;
            case SwitchCommand.Persistent: form.TogglePersistent(); break;
            case SwitchCommand.Commit: form.AltReleased(); break;
            case SwitchCommand.Pause: TogglePause(); break;
            case SwitchCommand.Left: form.DetailedNavigate(Keys.Left); break;
            case SwitchCommand.Right: form.DetailedNavigate(Keys.Right); break;
            case SwitchCommand.Up: form.DetailedNavigate(Keys.Up); break;
            case SwitchCommand.Down: form.DetailedNavigate(Keys.Down); break;
        }
    }
    private void TogglePause() {
        paused=!paused; if(keyboard!=null)keyboard.Enabled=prefs.ReplaceAltTab&&!paused;
        form.Paused=paused; form.Dismiss(true);
        pauseMenu.Text=paused?"Alt+Tab 대체 다시 켜기":"Alt+Tab 대체 일시 중지";
        tray.Text=paused?"AltTabFlow · Alt+Tab 대체 중지됨":"AltTabFlow · Ctrl+Alt+Space";
        tray.ShowBalloonTip(1500,"AltTabFlow",paused?"기본 Windows Alt+Tab으로 전환했습니다.":"AltTabFlow Alt+Tab을 다시 켰습니다.",ToolTipIcon.Info);
    }
    private async Task Listen() {
        while(!cancellation.IsCancellationRequested) {
            try {
                using var server=new NamedPipeServerStream("AltTabFlow."+Environment.UserName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellation.Token);
                using var reader=new StreamReader(server); var line=await reader.ReadLineAsync(cancellation.Token);
                if(closing)break;
                switch(line) {
                    case "show": form.OpenSwitcher(true,0); break;
                    case "quit": Exit(); break;
                    case "hide": form.Dismiss(false); break;
                    case "pause": TogglePause(); break;
                    case "status": File.WriteAllText(Path.Combine(Preferences.DataDirectory,"status.json"),JsonSerializer.Serialize(new{ui=form.DiagnosticState(),pid=Environment.ProcessId,paused,altTabEnabled=keyboard?.Enabled??false,hook=keyboard?.Statistics,shortcuts=shortcutHistory.ToArray()},new JsonSerializerOptions{WriteIndented=true})); break;
                }
            } catch(OperationCanceledException){break;} catch(Exception ex){AppLog.Write("ipc-error",new{ex.Message}); if(!closing)await Task.Delay(200);}
        }
    }
    private void Exit() {
        if(closing)return; closing=true;
        cancellation.Cancel(); startupTimer.Dispose(); keyboard?.Dispose();
        if(foregroundHook!=0)Native.UnhookWinEvent(foregroundHook);
        tray.Visible=false; tray.Icon?.Dispose(); tray.Dispose(); catalog.Dispose(); form.Shutdown(); ExitThread();
    }
    protected override void Dispose(bool disposing) { if(disposing){Exit();cancellation.Dispose();}base.Dispose(disposing); }
}

internal static class SelfTests
{
    public static void Run() {
        var result=new List<object>(); int failures=0;
        void Check(string name,bool condition) {result.Add(new{name,passed=condition});if(!condition)failures++;}
        WindowInfo Row(int h,string app,string title,int order)=>new(h,title,app,app,"C:/Apps/"+app+".exe",order,false);
        var rows=new List<WindowInfo>{Row(1,"EXCEL","보상 시뮬레이터.xlsx",0),Row(2,"chrome","콘텐츠맵",1),Row(3,"EXCEL","퀘스트 보상 입력.xlsx",2),Row(4,"slack","메시지",3)};
        var p=new Preferences();
        var groups=WindowCatalog.Build(rows,p,"",null,0);
        Check("같은 앱 창 묶기",groups.Count==3&&groups.Single(x=>x.Window.ProcessName=="EXCEL").Windows.Count==2);
        Check("그룹에 최근 창 제목 보존",groups.Single(x=>x.Window.ProcessName=="EXCEL").Window.Title=="보상 시뮬레이터.xlsx");
        var search=WindowCatalog.Build(rows,p,"퀘스트 보상",null,0);
        Check("한글 다중 단어 검색 및 그룹 안의 창 발견",search.Count==1&&search[0].Window.Handle==3);
        Check("대소문자 무시 앱 이름 검색",WindowCatalog.Build(rows,p,"excel",null,0).Count==2);
        Check("그룹 펼치기",WindowCatalog.Build(rows,p,"",rows[0].GroupKey,0).Count==2);
        p.Excluded.Add("CHROME"); Check("앱 제외 대소문자 무시",WindowCatalog.Build(rows,p,"",null,0).Count==2);p.Excluded.Clear();
        p.Pinned.Add("slack"); Check("즐겨찾기 우선 표시",WindowCatalog.Build(rows,p,"",null,0)[0].Window.ProcessName=="slack");p.Pinned.Clear();
        p.GroupApps=false;Check("묶기 해제",WindowCatalog.Build(rows,p,"",null,0).Count==4);p.GroupApps=true;
        Check("검색 결과 없음",WindowCatalog.Build(rows,p,"없는 제목",null,0).Count==0);
        var sortedRows=new[]{Row(10,"zeta","z",0),Row(11,"나무","n",1),Row(12,"Alpha","a",2),Row(13,"가방","g",3),Row(14,"beta","b",4),Row(15,"다람쥐","d",5)};
        string Sort(Preferences pref)=>string.Join(",",WindowCatalog.Build(sortedRows,pref,"",null,0).Select(x=>x.Window.AppName));
        Check("영문 먼저 A~Z 이후 가나다순",Sort(new())=="Alpha,beta,zeta,가방,나무,다람쥐");
        var mruEntries=WindowCatalog.Build(sortedRows,new(),"",null,0);
        var mru=new[]{sortedRows[2],sortedRows[5],sortedRows[0],sortedRows[1],sortedRows[3],sortedRows[4]};
        int lastApp=WindowCatalog.InitialSelection(mruEntries,mru,12,false,1);
        Check("이름순 화면에서도 첫 AltTab은 직전 프로그램",mruEntries[lastApp].Window.Handle==15);
        var reverseMru=new[]{sortedRows[5],sortedRows[2],sortedRows[0],sortedRows[1],sortedRows[3],sortedRows[4]};
        Check("다시 AltTab하면 직전 프로그램으로 왕복",mruEntries[WindowCatalog.InitialSelection(mruEntries,reverseMru,15,false,1)].Window.Handle==12);
        Check("영문 다음 항목이 직전 프로그램을 덮어쓰지 않음",lastApp!=1);
        Check("상세 화면은 현재 프로그램 선택으로 시작",mruEntries[WindowCatalog.InitialSelection(mruEntries,mru,12,true,0)].Window.Handle==12);
        Check("포커스 강조와 대상 창 앞으로 보이기 기본 켜짐",new Preferences().EmphasizeTarget&&new Preferences().PeekTarget);
        var sortPrefs=new Preferences{Pinned=new(){"나무","zeta","Alpha"}};
        Check("등록 앱 내부도 등록 순서와 무관하게 이름순",Sort(sortPrefs)=="Alpha,zeta,나무,beta,가방,다람쥐");
        sortPrefs.Pinned.Reverse();Check("등록 목록 순서 변경은 화면 순서에 영향 없음",Sort(sortPrefs)=="Alpha,zeta,나무,beta,가방,다람쥐");
        sortPrefs.Excluded.Add("Alpha");Check("상단 등록해도 숨김 우선",Sort(sortPrefs)=="zeta,나무,beta,가방,다람쥐");
        var normalized=Preferences.NormalizeApps(new[]{" CHROME.exe ","chrome","EXCEL.EXE"," "});Check("등록 이름 정규화와 중복 제거",normalized.SequenceEqual(new[]{"CHROME","EXCEL"}));
        Check("자동 실행 초기 기본값 켜짐",new Preferences().AutoStartByDefault);
        var optedOut=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences{StartWithWindows=false}));
        Check("자동 실행 해제 후 재로드에도 해제 유지",optedOut!=null&&!optedOut.AutoStartByDefault);
        var legacy=JsonSerializer.Deserialize<Preferences>("{\"View\":\"목록\"}");Check("기존 설정에 자동 실행 항목이 없어도 새 기본값 적용",legacy!.AutoStartByDefault);
        foreach(var palette in ThemePalette.All){
            Check(palette.Name+" 선택 저장과 재로드",ThemePalette.Resolve(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences{ColorPreset=palette.Id}))!).Id==palette.Id);
        }
        Check("기존 밝은 테마 설정 호환",ThemePalette.Resolve(new Preferences{LightTheme=true}).Light);
        var strips=TargetAura.PlanStrips(new Rectangle(-4096,0,4096,2160));
        Check("4K 아우라 버퍼는 전체 화면의 5% 미만",strips.Sum(x=>(long)x.Width*x.Height)<4096L*2160*.05);
        Check("멀티 모니터 음수 좌표 아우라 유지",strips[0].Left< -4096&&strips.All(x=>x.Width>0&&x.Height>0));
        var commands=new List<SwitchCommand>();var router=new ShortcutRouter(commands.Add);
        bool Route(uint key,bool down=true,bool alt=true,bool ctrl=false,bool shift=false,bool enabled=true,bool otherAlt=false)=>router.Route(key,down,alt,ctrl,shift,enabled,otherAlt);
        Route(0xA4);for(int i=0;i<5;i++){Route(9);Route(9,false);}
        Check("Alt 누른 채 Tab 5회는 Next 5회이며 중간 확정 없음",commands.SequenceEqual(Enumerable.Repeat(SwitchCommand.Next,5)));
        Route(0xA4,false);Route(0xA4,false);Check("Alt를 놓을 때 Commit은 한 번만",commands.SequenceEqual(Enumerable.Repeat(SwitchCommand.Next,5).Append(SwitchCommand.Commit)));
        commands.Clear();Route(0xA2,ctrl:true);Route(9,ctrl:false);Route(9,false);Route(0xA4,false);
        Check("Ctrl-up 누락 뒤 실제 Ctrl 해제 상태면 정상 빠른 전환",commands.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
        commands.Clear();for(int i=0;i<3;i++){Route(9,ctrl:true);Route(9,false,ctrl:true);}Route(0xA4,false);
        Check("Ctrl+Alt+Tab 반복도 닫기 토글 없이 계속 이동",commands.SequenceEqual(new[]{SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.Commit}));
        commands.Clear();Route(9,shift:true);Route(9,false);Route(0xA4,false);
        Check("Shift 역방향 후 Alt 해제 확정",commands.SequenceEqual(new[]{SwitchCommand.Previous,SwitchCommand.Commit}));
        commands.Clear();Route(9);Route(9,false);Route(0x27);Route(0x27,false);Route(0xA4,false);
        Check("Alt 유지 중 방향키는 Commit보다 먼저 상세 조작 처리",commands.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Right,SwitchCommand.Commit}));
        commands.Clear();Route(9);Route(9,false);Route(0xA4,false,otherAlt:true);Check("양쪽 Alt 중 하나만 놓으면 확정하지 않음",commands.Count==1);Route(0xA5,false);
        commands.Clear();Check("일시 중지 중 AltTab은 가로채지 않음",!Route(9,enabled:false)&&commands.Count==0);
        commands.Clear();Route(0x20,ctrl:true);Route(0x20,ctrl:true);Route(0x20,false);Check("명시적 CtrlAltSpace만 열기 닫기 토글",commands.SequenceEqual(new[]{SwitchCommand.Persistent}));
        foreach(var viewport in new[]{new Size(4048,2048),new Size(1872,976),new Size(1318,704)}){
            foreach(int count in new[]{17,22}){
                var layout=LayoutPlanner.Plan(count,viewport.Width,viewport.Height,false);
                Check($"{viewport.Width}x{viewport.Height}에서 {count}개 앱 전체 표시",layout.Capacity>=count&&layout.Width<=viewport.Width&&layout.Height<=viewport.Height);
            }
        }
        foreach(bool icons in new[]{true,false}){var layout=LayoutPlanner.PlanTiles(22,4048,2048,icons);Check($"주 모니터 {(icons?"아이콘":"카드")}형에서 22개 앱 전체 표시",layout.Capacity>=22&&layout.Width<=4048&&layout.Height<=2048);}
        using(var navigationCatalog=new CatalogService()) {
            foreach(string mode in new[]{"목록","카드","아이콘"}) {
                nint activated=0;
                var navigationPrefs=new Preferences{View=mode};
                var form=new SwitcherForm(navigationPrefs,navigationCatalog,h=>{activated=h;return true;});
                try {
                    var navigationRows=Enumerable.Range(1,4).Select(i=>Row(100+i,"chrome","Chrome 창 "+i,i)).Append(Row(200,"EXCEL","경제.xlsx",5)).ToList();
                    form.PrepareNavigationTest(navigationRows);form.TestKey(Keys.Tab);
                    Check(mode+" Tab 반복은 빠른 전환 유지",!form.NavigationState.Persistent);
                    form.TestKey(Keys.Up);
                    Check(mode+" 방향키를 누르면 상세 모드 전환",form.NavigationState.Persistent);
                    form.PrepareNavigationTest(navigationRows);form.TestKey(Keys.Enter);
                    Check(mode+" Enter로 4개 Chrome 창 진입",form.NavigationState.Group!=null&&form.NavigationState.Count==4&&activated==0);
                    Check(mode+" 진입 후 Alt 해제에도 화면 유지",form.NavigationState.Persistent);
                    form.TestKey(Keys.Back);
                    Check(mode+" Backspace로 전체 목록 복귀",form.NavigationState.Group==null&&form.NavigationState.Count==2&&form.NavigationState.Selected==101);
                    form.TestKey(Keys.Enter);form.TestKey(Keys.Tab);form.TestKey(Keys.Enter);
                    Check(mode+" 두 번째 Enter로 선택한 개별 창 전환",activated==102);
                    Check(mode+" 닫은 뒤 상세 모드 초기화",!form.NavigationState.Persistent);
                } finally {form.Shutdown();}
            }
        }
        var many=Enumerable.Range(1,500).Select(i=>Row(i,"App"+(i%25),"문서 "+i,i)).ToList();
        var sw=Stopwatch.StartNew();for(int i=0;i<200;i++)WindowCatalog.Build(many,p,"",null,0);sw.Stop();
        result.Add(new{name="500개 창 그룹화 평균 ms",value=sw.Elapsed.TotalMilliseconds/200});
        var live=WindowCatalog.Read();Check("실제 Windows 창 열거",live.Count>0);Check("중복 HWND 없음",live.Select(x=>x.Handle).Distinct().Count()==live.Count);
        var json=JsonSerializer.Serialize(new{failures,tests=result,liveWindowCount=live.Count},new JsonSerializerOptions{WriteIndented=true});
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,"self-test.json"),json);Environment.ExitCode=failures==0?0:1;
    }
}
