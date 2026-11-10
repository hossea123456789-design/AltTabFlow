using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AltTabFlow;

internal static class AppLog
{
    private static readonly object Gate=new();
    public static bool Suppressed;
    public static void Write(string evt,object data) {
        if(Suppressed)return;
        // Never block input or rendering for disk I/O.
        ThreadPool.QueueUserWorkItem(_=> {
            try { lock(Gate) { Directory.CreateDirectory(Preferences.DataDirectory); string path=Path.Combine(Preferences.DataDirectory,"events.jsonl"); if(File.Exists(path)&&new FileInfo(path).Length>1_000_000)File.Move(path,path+".previous",true); File.AppendAllText(path,JsonSerializer.Serialize(new{time=DateTimeOffset.Now,pid=Environment.ProcessId,evt,data})+Environment.NewLine); } } catch { }
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
        AppLog.Suppressed=args.Any(x=>x.EndsWith("self-test",StringComparison.Ordinal)||x.EndsWith("fixture",StringComparison.Ordinal));
        if(args.Contains("--self-test")) { SelfTests.Run(); return; }
        if(args.Contains("--peek-self-test")) { PeekTests.Run(); return; }
        if(args.Contains("--focus-self-test")) { PeekTests.Run(true); return; }
        if(args.Contains("--menu-self-test")) { MenuTests.Run(); return; }
        if(args.Contains("--ui-self-test")) { UiTests.Run(); return; }
        if(args.Contains("--ui-preview")){using var preview=new SettingsForm(Preferences.Load(),Array.Empty<WindowInfo>()){PreviewOnly=true};preview.TestPage(2);preview.Show();Native.ShowWindow(preview.Handle,5);Application.Run(preview);return;}
        if(args.Length==2&&args[0]=="--peek-fixture") { PeekTests.RunFixture(args[1]); return; }
        if(args.Length==2&&args[0]=="--focus-fixture") { PeekTests.RunFocusFixture(args[1]); return; }
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
    private readonly ConcurrentQueue<ShortcutInput> pendingInput=new();
    private int drainPosted;
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
        form.CommitFinished=PostDrain;
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
        if(!args.Contains("--preview")) keyboard=new KeyboardHook(command=> {
            pendingInput.Enqueue(new(command,Stopwatch.GetTimestamp()));PostDrain();
        },form.TrackForeground) {Enabled=prefs.ReplaceAltTab};
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
    private void PostDrain() {
        if(closing||form.IsDisposed||pendingInput.IsEmpty||Interlocked.Exchange(ref drainPosted,1)!=0)return;
        try{form.BeginInvoke((Action)DrainInput);}catch(InvalidOperationException){Interlocked.Exchange(ref drainPosted,0);}
    }
    private void DrainInput() {
        Interlocked.Exchange(ref drainPosted,0);
        if(closing)return;
        while(!form.IsCommitting&&!pendingInput.IsEmpty) {
            var batch=pendingInput.ToArray();
            int length=!form.Visible&&!form.IsConfiguring?QuickGesture.Length(batch,0):0;
            if(length>0) {
                var gesture=new List<ShortcutInput>();
                for(int i=0;i<length;i++)if(pendingInput.TryDequeue(out var input))gesture.Add(input);
                form.QuickSwitch(gesture);continue;
            }
            if(pendingInput.TryDequeue(out var next))Handle(next);
        }
    }
    private void Handle(ShortcutInput input) {
        if(closing||form.IsConfiguring) return;
        var command=input.Command;
        if(!form.Visible&&command is SwitchCommand.Next or SwitchCommand.Previous or SwitchCommand.PinnedNext or SwitchCommand.PinnedPrevious or SwitchCommand.SameApp or SwitchCommand.SameAppPrevious or SwitchCommand.Persistent)form.InputTimestamp=input.Timestamp;
        shortcutHistory.Enqueue(new{time=DateTimeOffset.Now,command=command.ToString(),visible=form.Visible,persistent=form.IsPersistent});while(shortcutHistory.Count>24)shortcutHistory.Dequeue();
        switch(command) {
            case SwitchCommand.Next: form.OpenSwitcher(false,1); break;
            case SwitchCommand.Previous: form.OpenSwitcher(false,-1); break;
            case SwitchCommand.PinnedNext: form.OpenSwitcher(true,1); break;
            case SwitchCommand.PinnedPrevious: form.OpenSwitcher(true,-1); break;
            case SwitchCommand.Pin: form.PinSelection(); break;
            case SwitchCommand.SameApp: form.OpenSwitcher(false,1,true); break;
            case SwitchCommand.SameAppPrevious: form.OpenSwitcher(false,-1,true); break;
            case SwitchCommand.Persistent: form.TogglePersistent(); break;
            case SwitchCommand.Commit: form.NavigationReleased(input.Timestamp); break;
            case SwitchCommand.Cancel: form.Dismiss(true); break;
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
        var history=new ForegroundHistory();
        history.Record(10,1,100);history.Record(20,2,120);history.Record(30,3,140);
        Check("실제로 클릭한 창을 기준으로 최근 사용 순서 기록",history.Rank(30,3)>history.Rank(20,2)&&history.Rank(20,2)>history.Rank(10,1));
        Check("늦게 도착한 포커스 이벤트가 현재 기록을 덮지 않음",!history.Record(10,1,110)&&history.Rank(30,3)>history.Rank(10,1));
        Check("재사용된 HWND의 다른 프로세스 기록을 섞지 않음",history.Rank(30,99)==0);
        var wrapHistory=new ForegroundHistory();wrapHistory.Record(10,1,uint.MaxValue-10);
        Check("시스템 밀리초 카운터 오버플로 뒤에도 순서 유지",wrapHistory.Record(20,2,10)&&wrapHistory.Rank(20,2)>wrapHistory.Rank(10,1));
        Check("오버플로 이전의 지연 이벤트는 무시",!wrapHistory.Record(10,1,uint.MaxValue-5));
        using(var owner=new Form())using(var popup=new Form())using(var otherWindow=new Form()) {
            popup.Owner=owner;_=owner.Handle;_=popup.Handle;_=otherWindow.Handle;
            result.Add(new{name="owner-fixture",owner=owner.Handle.ToInt64(),popup=popup.Handle.ToInt64(),rootOwner=WindowIdentity.Root(owner.Handle).ToInt64(),rootPopup=WindowIdentity.Root(popup.Handle).ToInt64(),popupOwner=Native.GetWindow(popup.Handle,4).ToInt64()});
            var ownedRows=new[]{Row((int)owner.Handle,"p4_toolbox","P4 main",0),Row((int)otherWindow.Handle,"Other","other",1)};
            var ownedEntries=WindowCatalog.Build(ownedRows,new(),"",null,0);
            int choice=WindowCatalog.InitialSelection(ownedEntries,ownedRows,popup.Handle,false,1);
            Check("P4 메인 대신 소유 팝업을 클릭해도 다른 프로그램으로 전환",ownedEntries[choice].Window.Handle==otherWindow.Handle);
            var swappedRows=new[]{ownedRows[0] with{Handle=popup.Handle},ownedRows[1]};
            var swappedEntries=WindowCatalog.Build(swappedRows,new(),"",null,0);
            Check("목록에는 팝업이 남고 메인을 클릭한 반대 경우도 전환",swappedEntries[WindowCatalog.InitialSelection(swappedEntries,swappedRows,owner.Handle,false,1)].Window.Handle==otherWindow.Handle);
            Check("동일 프로세스의 별도 창은 소유 관계 없이 합치지 않음",!WindowIdentity.Same(owner.Handle,otherWindow.Handle));
            history.Record(WindowIdentity.Root(popup.Handle),(uint)Environment.ProcessId,200);
            Check("메인과 소유 팝업의 최근 사용 순서를 공유",history.Rank(owner.Handle)==history.Rank(popup.Handle)&&history.Rank(owner.Handle)>0);
        }
        ShortcutInput[] Gesture(params SwitchCommand[] values)=>values.Select(x=>new ShortcutInput(x,0)).ToArray();
        Check("이미 끝난 빠른 AltTab은 UI 생성 없이 전환 가능",QuickGesture.Length(Gesture(SwitchCommand.Next,SwitchCommand.Commit),0)==2);
        Check("여러 Tab 뒤 Alt 해제도 순서를 보존",QuickGesture.Length(Gesture(SwitchCommand.Next,SwitchCommand.Next,SwitchCommand.Previous,SwitchCommand.Commit),0)==4);
        Check("방향키가 끼면 빠른 경로를 사용하지 않음",QuickGesture.Length(Gesture(SwitchCommand.Next,SwitchCommand.Right,SwitchCommand.Commit),0)==0);
        Check("고정 상세 모드는 빠른 경로를 사용하지 않음",QuickGesture.Length(Gesture(SwitchCommand.PinnedNext,SwitchCommand.Commit),0)==0);
        Check("아직 Alt를 잡고 있으면 선택 화면을 표시",QuickGesture.Length(Gesture(SwitchCommand.Next),0)==0);
        Check("연속 왕복 제스처는 각각 따로 확정",QuickGesture.Length(Gesture(SwitchCommand.Next,SwitchCommand.Commit,SwitchCommand.Next,SwitchCommand.Commit),0)==2);
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
        Check("Ctrl+Alt+Tab 반복은 계속 이동하며 키 해제에 Commit 없음",commands.SequenceEqual(new[]{SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.PinnedNext}));
        commands.Clear();Route(9,shift:true);Route(9,false);Route(0xA4,false);
        Check("Shift 역방향 후 Alt 해제 확정",commands.SequenceEqual(new[]{SwitchCommand.Previous,SwitchCommand.Commit}));
        commands.Clear();Route(9);Route(9,false);Route(0x27);Route(0x27,false);Route(0xA4,false);
        Check("Alt 유지 중 방향키 이동은 최종 해제 Commit보다 먼저 처리",commands.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Right,SwitchCommand.Commit}));
        commands.Clear();Route(9);Route(9,false);Route(0xA4,false,otherAlt:true);Check("양쪽 Alt 중 하나만 놓으면 확정하지 않음",commands.Count==1);Route(0xA5,false);
        commands.Clear();Check("일시 중지 중 AltTab은 가로채지 않음",!Route(9,enabled:false)&&commands.Count==0);
        commands.Clear();Route(0x20,ctrl:true);Route(0x20,ctrl:true);Route(0x20,false);Check("명시적 CtrlAltSpace만 열기 닫기 토글",commands.SequenceEqual(new[]{SwitchCommand.Persistent}));
        // Key-up ordering matters: the last held key, not Alt-up alone, commits.
        var releaseCommands=new List<SwitchCommand>();var releaseRouter=new ShortcutRouter(releaseCommands.Add);
        bool Key(uint key,bool down,bool alt=false,bool ctrl=false,bool shift=false,bool otherAlt=false,bool otherCtrl=false,bool otherShift=false)=>releaseRouter.Route(key,down,alt,ctrl,shift,true,otherAlt,otherCtrl,otherShift);
        void BeginReleaseCase(){releaseCommands.Clear();releaseRouter=new ShortcutRouter(releaseCommands.Add);Key(0xA4,true);Key(9,true,true);Key(9,false,true);}
        BeginReleaseCase();Key(0x27,true,true);Key(0xA4,false);
        Check("Alt를 먼저 놓아도 화살표를 잡고 있으면 아직 확정하지 않음",!releaseCommands.Contains(SwitchCommand.Commit));
        Key(0x27,true);Key(0x27,false);Key(0x27,false);
        Check("Alt 해제 뒤 화살표 반복 이동 후 마지막 해제에 정확히 한 번 확정",releaseCommands.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Right,SwitchCommand.Right,SwitchCommand.Commit}));
        BeginReleaseCase();Key(0x25,true,true);Key(0x25,false,true);
        Check("화살표를 먼저 놓아도 Alt를 잡고 있으면 선택 화면 유지",!releaseCommands.Contains(SwitchCommand.Commit));Key(0xA4,false);
        Check("화살표 먼저 해제 후 Alt 해제로 확정",releaseCommands.Last()==SwitchCommand.Commit);
        BeginReleaseCase();Key(9,true,true);Key(0xA4,false);
        Check("Tab을 잡고 있으면 Alt 해제만으로 확정하지 않음",!releaseCommands.Contains(SwitchCommand.Commit));Key(9,false);
        Check("Alt 다음 Tab 해제도 확정",releaseCommands.Last()==SwitchCommand.Commit);
        BeginReleaseCase();Key(0x25,true,true);Key(0x26,true,true);Key(0xA4,false);Key(0x25,false);
        Check("화살표 둘 중 하나만 놓으면 확정하지 않음",!releaseCommands.Contains(SwitchCommand.Commit));Key(0x26,false);
        Check("여러 화살표 중 마지막 키 해제로 확정",releaseCommands.Last()==SwitchCommand.Commit);
        BeginReleaseCase();Key(0xA0,true,true,shift:true);Key(0xA4,false,shift:true);
        Check("Shift를 누른 채면 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0xA0,false,shift:true,otherShift:true);
        Check("양쪽 Shift 중 하나만 놓아도 계속 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0xA1,false,shift:true);
        Check("현재 Shift-up 이벤트로 마지막 해제 판단",releaseCommands.Last()==SwitchCommand.Commit);
        BeginReleaseCase();Key(0xA2,true,true,ctrl:true);Key(0xA4,false,ctrl:true);Key(0xA2,false,ctrl:true,otherCtrl:true);
        Check("AltTab 중 Ctrl을 누르면 고정 모드로 승격",releaseCommands.Last()==SwitchCommand.Pin);Key(0xA3,false,ctrl:true);
        Check("고정 모드는 마지막 Ctrl 해제에도 Commit 없음",!releaseCommands.Contains(SwitchCommand.Commit));
        BeginReleaseCase();Key(0x27,true,true);Key(0x1B,true,true);Key(0x1B,false,true);Key(0xA4,false);Key(0x27,false);
        Check("Esc 취소 후 남은 키를 놓아도 전환하지 않음",releaseCommands.Last()==SwitchCommand.Cancel&&!releaseCommands.Contains(SwitchCommand.Commit));
        BeginReleaseCase();Key(0x0D,true,true);Key(0xA4,false);
        Check("묶음 진입 Enter를 잡고 있으면 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0x0D,false);
        Check("Enter까지 모두 놓으면 확정",releaseCommands.Last()==SwitchCommand.Commit);
        releaseCommands.Clear();releaseRouter=new ShortcutRouter(releaseCommands.Add);Key(0x27,true);Key(0xA4,true);Key(9,true,true);Key(9,false,true);Key(0xA4,false);
        Check("AltTab 전에 누른 화살표도 마지막 해제까지 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0x27,false);
        Check("시작 전에 누른 화살표 해제도 확정",releaseCommands.Last()==SwitchCommand.Commit);
        Check("Alt 문맥 비트가 없어도 Tab에서 실제 Alt 눌림을 인식",KeyboardHook.AltSnapshot(9,0,true));
        Check("Alt 자체 key-up은 이전 비동기 눌림으로 되살리지 않음",!KeyboardHook.AltSnapshot(0xA4,0,true));
        Check("Alt 문맥·실제 눌림 모두 없으면 일반 Tab",!KeyboardHook.AltSnapshot(9,0,false));
        foreach(var order in new[]{new uint[]{9,0xA4,0xA2},new uint[]{9,0xA2,0xA4},new uint[]{0xA4,9,0xA2},new uint[]{0xA4,0xA2,9},new uint[]{0xA2,9,0xA4},new uint[]{0xA2,0xA4,9}}){
            var emitted=new List<SwitchCommand>();var pinnedRouter=new ShortcutRouter(emitted.Add);
            pinnedRouter.Route(0xA2,true,false,false,false,true);pinnedRouter.Route(0xA4,true,false,true,false,true);
            bool consumed=pinnedRouter.Route(9,true,KeyboardHook.AltSnapshot(9,0,true),true,false,true);
            bool altHeld=true,ctrlHeld=true;
            foreach(uint key in order){if(key==0xA4)altHeld=false;if(key==0xA2)ctrlHeld=false;pinnedRouter.Route(key,false,altHeld,ctrlHeld,false,true);}
            Check("CtrlAltTab 해제 순서 "+string.Join("/",order)+"는 고정 열기만 전달",consumed&&emitted.SequenceEqual(new[]{SwitchCommand.PinnedNext}));
            emitted.Clear();pinnedRouter.Route(9,true,true,false,false,true);pinnedRouter.Route(9,false,true,false,false,true);pinnedRouter.Route(0xA4,false,false,false,false,true);
            Check("고정 제스처 이후 일반 AltTab 세션 초기화 "+string.Join("/",order),emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
        }
        var promoted=new List<SwitchCommand>();var promotedRouter=new ShortcutRouter(promoted.Add);
        promotedRouter.Route(9,true,true,false,false,true);promotedRouter.Route(9,false,true,false,false,true);promotedRouter.Route(0xA2,true,true,false,false,true);
        Check("Tab 다음 늦은 Ctrl-down은 이동 없이 Pin 명령",promoted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Pin}));
        promotedRouter.Route(0xA2,false,true,true,false,true);promotedRouter.Route(9,true,true,false,false,true);promotedRouter.Route(9,false,true,false,false,true);promotedRouter.Route(0xA4,false,false,false,false,true);
        Check("Ctrl 먼저 해제 후 Tab 반복도 고정 유지하며 확정하지 않음",promoted.Last()==SwitchCommand.PinnedNext&&!promoted.Contains(SwitchCommand.Commit));
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
                var navigationPrefs=new Preferences{View=mode,HighlightTarget=false,EmphasizeTarget=false,PeekTarget=false};
                var form=new SwitcherForm(navigationPrefs,navigationCatalog,h=>{activated=h;return true;}){InspectionMode=true};
                try {
                    var navigationRows=Enumerable.Range(1,4).Select(i=>Row(100+i,"chrome","Chrome 창 "+i,i)).Append(Row(200,"EXCEL","경제.xlsx",5)).ToList();
                    form.PrepareNavigationTest(navigationRows);form.TestKey(Keys.Tab);
                    Check(mode+" Tab 반복은 빠른 전환 유지",!form.NavigationState.Persistent);
                    form.TestKey(Keys.Up);
                    Check(mode+" 방향키 이동 후에도 전체 키 해제 확정 유지",!form.NavigationState.Persistent);
                    form.PrepareNavigationTest(navigationRows);form.TestKey(Keys.Enter);
                    Check(mode+" Enter로 4개 Chrome 창 진입",form.NavigationState.Group!=null&&form.NavigationState.Count==4&&activated==0);
                    Check(mode+" 묶음 진입도 전체 키 해제 확정 유지",!form.NavigationState.Persistent);
                    form.TestKey(Keys.Back);
                    Check(mode+" Backspace로 전체 목록 복귀",form.NavigationState.Group==null&&form.NavigationState.Count==2&&form.NavigationState.Selected==101);
                    form.TestKey(Keys.Enter);form.TestKey(Keys.Tab);form.TestKey(Keys.Enter);
                    Check(mode+" 두 번째 Enter로 선택한 개별 창 전환",activated==102);
                    Check(mode+" 닫은 뒤 상세 모드 초기화",!form.NavigationState.Persistent);
                    activated=0;form.PrepareNavigationTest(navigationRows);form.Show();
                    var formRouter=new ShortcutRouter(command=>{
                        if(command==SwitchCommand.Up)form.DetailedNavigate(Keys.Up);
                        if(command==SwitchCommand.Down)form.DetailedNavigate(Keys.Down);
                        if(command==SwitchCommand.Commit)form.NavigationReleased();
                    });
                    formRouter.Route(9,true,true,false,false,true);formRouter.Route(9,false,true,false,false,true);
                    formRouter.Route(0x28,true,true,false,false,true);formRouter.Route(0x28,false,true,false,false,true);
                    formRouter.Route(0x26,true,true,false,false,true);formRouter.Route(0xA4,false,false,false,false,true);
                    Check(mode+" 실제 폼에서 Alt를 먼저 놓고 화살표를 잡으면 유지",form.Visible&&activated==0);
                    nint highlighted=form.NavigationState.Selected;
                    formRouter.Route(0x26,false,false,false,false,true);
                    Check(mode+" 마지막 화살표 해제로 Chrome 대표 창 전환 후 닫힘",highlighted==101&&activated==highlighted&&!form.Visible);
                    activated=0;form.PrepareNavigationTest(navigationRows);form.Show();form.TestKey(Keys.Enter);form.TestKey(Keys.Tab);
                    form.NavigationReleased();
                    Check(mode+" 펼친 Chrome 목록에서는 하이라이트한 개별 창으로 전환",activated==102&&!form.Visible);
                    activated=0;form.PrepareNavigationTest(navigationRows,true);form.Show();form.DetailedNavigate(Keys.Down);form.NavigationReleased();
                    Check(mode+" 명시적 고정 모드에서는 키 해제 후에도 화면 유지",form.Visible&&form.IsPersistent&&activated==0);form.Dismiss(false);
                    // Route the full Ctrl+Alt+Tab gesture through the real open,
                    // repeat, release and group-navigation methods.
                    activated=0;
                    var pinnedFormRouter=new ShortcutRouter(command=>{
                        switch(command){
                            case SwitchCommand.PinnedNext:form.OpenSwitcher(true,1);break;
                            case SwitchCommand.PinnedPrevious:form.OpenSwitcher(true,-1);break;
                            case SwitchCommand.Next:form.OpenSwitcher(false,1);break;
                            case SwitchCommand.Pin:form.PinSelection();break;
                            case SwitchCommand.Commit:form.NavigationReleased();break;
                            case SwitchCommand.Cancel:form.Dismiss(false);break;
                        }
                    });
                    void PinnedGesture(bool shift=false){pinnedFormRouter.Route(9,true,true,true,shift,true);pinnedFormRouter.Route(9,false,true,true,shift,true);pinnedFormRouter.Route(0xA2,false,true,false,shift,true);pinnedFormRouter.Route(0xA4,false,false,false,shift,true);if(shift)pinnedFormRouter.Route(0xA0,false,false,false,false,true);}
                    PinnedGesture();Check(mode+" CtrlAltTab은 숨김 상태에서 실제 고정 화면을 엶",form.Visible&&form.IsPersistent&&activated==0);
                    form.NavigationReleased();Check(mode+" 고정 화면은 잔여 해제 명령에도 닫히지 않음",form.Visible&&activated==0);
                    form.PrepareNavigationTest(navigationRows,true);PinnedGesture();Check(mode+" CtrlAltTab 반복은 닫지 않고 다음 앱 선택",form.Visible&&form.IsPersistent&&form.NavigationState.Selected==200&&activated==0);
                    PinnedGesture(true);Check(mode+" CtrlAltShiftTab은 이전 앱 선택",form.Visible&&form.NavigationState.Selected==101);
                    form.TestKey(Keys.Enter);form.NavigationReleased();Check(mode+" 고정 Chrome 묶음 Enter 진입 후에도 유지",form.Visible&&form.NavigationState.Count==4&&activated==0);
                    form.TestKey(Keys.Back);form.TestKey(Keys.Enter);form.TestKey(Keys.Tab);form.TestKey(Keys.Enter);Check(mode+" 고정 화면 Enter로 선택 개별 창 확정",activated==102&&!form.Visible);
                    activated=0;form.PrepareNavigationTest(navigationRows);form.Show();pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);var beforePin=form.NavigationState.Selected;
                    pinnedFormRouter.Route(0xA2,true,true,false,false,true);pinnedFormRouter.Route(0xA4,false,false,true,false,true);pinnedFormRouter.Route(0xA2,false,false,false,false,true);
                    Check(mode+" 늦게 누른 Ctrl도 현재 선택을 유지하며 고정",form.Visible&&form.IsPersistent&&form.NavigationState.Selected==beforePin&&activated==0);
                    form.TestKey(Keys.Escape);Check(mode+" 고정 화면 Esc는 전환 없이 닫힘",!form.Visible&&activated==0);
                    form.PrepareNavigationTest(navigationRows);form.Show();pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);pinnedFormRouter.Route(0xA4,false,false,false,false,true);
                    Check(mode+" 고정 화면 취소 뒤 일반 AltTab은 키 해제로 전환",!form.Visible&&activated==200);
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
