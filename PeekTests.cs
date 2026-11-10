using System.Text.Json;
using System.Diagnostics;

namespace AltTabFlow;

// Integration regression using only disposable windows created by this test.
// WindowFromPoint checks the composed desktop's actual occluder, not just whether
// SetWindowPos returned success or a thumbnail of an obscured window.
internal static class PeekTests
{
    private sealed class TestWindow : Form {
        public TestWindow(){ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;}
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x80|0x8000000;return p;}}
    }
    private static bool Topmost(nint h)=>((long)Native.GetWindowLongPtr(h,-20)&8)!=0;
    private static void Settle(){for(int i=0;i<8;i++){Application.DoEvents();Thread.Sleep(10);}}
    private sealed class FocusWindow : Form {
        public string? BusySignal;
        public FocusWindow(){ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;}
        protected override bool ShowWithoutActivation=>true;
        protected override void WndProc(ref Message m){
            if(m.Msg==0x8001){if(BusySignal!=null)File.WriteAllText(BusySignal,"busy");Thread.Sleep(120);return;}
            base.WndProc(ref m);
        }
    }
    public static void RunFocusFixture(string report) {
        var work=(Screen.PrimaryScreen??Screen.AllScreens[0]).WorkingArea;
        using var target=new FocusWindow{Text="AltTabFlow 전환 회귀 검사",Bounds=new Rectangle(work.Left+35,work.Top+40,400,220),BusySignal=report+".busy"};
        using var popup=new FocusWindow{Text="AltTabFlow 소유 팝업 검사",Bounds=new Rectangle(work.Left+455,work.Top+40,250,180),TopMost=true,Owner=target};
        target.Show();popup.Show();
        File.WriteAllText(report,JsonSerializer.Serialize(new{target=target.Handle.ToInt64(),popup=popup.Handle.ToInt64()}));
        using var timeout=new System.Windows.Forms.Timer{Interval=120000};timeout.Tick+=(_,_)=>Application.ExitThread();timeout.Start();Application.Run();
    }
    public static void RunFixture(string report){
        var work=(Screen.PrimaryScreen??Screen.AllScreens[0]).WorkingArea;
        using var target=new TestWindow{Text="AltTabFlow 별도 프로세스 대상",Bounds=new Rectangle(work.Left+35,work.Top+40,470,270),BackColor=Color.SeaGreen};
        using var cover=new TestWindow{Text="AltTabFlow 별도 프로세스 가림",Bounds=target.Bounds,BackColor=Color.IndianRed,TopMost=true};
        target.Show();cover.Show();
        File.WriteAllText(report,JsonSerializer.Serialize(new{target=target.Handle.ToInt64(),cover=cover.Handle.ToInt64(),x=target.Left+55,y=target.Top+55}));
        using var timeout=new System.Windows.Forms.Timer{Interval=12000};timeout.Tick+=(_,_)=>Application.ExitThread();timeout.Start();Application.Run();
    }
    public static void Run(bool includeFocus=false){
        var result=new List<object>();int failures=0;
        var environment=new List<object>();
        void Observe(string phase,Native.Point location,nint expected){
            nint actual=Native.WindowFromPoint(location);
            Native.GetWindowThreadProcessId(actual,out uint pid);
            string process="";try{using var owner=Process.GetProcessById((int)pid);process=owner.ProcessName;}catch{}
            environment.Add(new{phase,x=location.X,y=location.Y,expected=expected.ToInt64(),actual=actual.ToInt64(),actualClass=Native.Class(actual),process,expectedVisible=Native.IsWindowVisible(expected)});
        }
        void Check(string name,bool value){result.Add(new{name,passed=value});if(!value)failures++;}
        var work=(Screen.PrimaryScreen??Screen.AllScreens[0]).WorkingArea;
        using var target=new TestWindow{Text="AltTabFlow 검증 · 대상",Bounds=new Rectangle(work.Left+35,work.Top+40,470,270),BackColor=Color.SeaGreen};
        using var cover=new TestWindow{Text="AltTabFlow 검증 · 가리는 창",Bounds=target.Bounds,BackColor=Color.IndianRed,TopMost=true};
        using var chooser=new TestWindow{Text="AltTabFlow 검증 · 선택 화면",Bounds=new Rectangle(work.Left+530,work.Top+40,230,150),BackColor=Color.DarkSlateBlue,TopMost=true};
        var point=new Native.Point{X=target.Left+55,Y=target.Top+55};
        var foreground=Native.GetForegroundWindow();
        using var peek=new TargetPeek();
        try{
            target.Show();cover.Show();chooser.Show();Settle();
            Observe("local-cover",point,cover.Handle);
            Check("겹친 always-on-top 창이 실제로 대상을 가림",Native.WindowFromPoint(point)==cover.Handle);
            peek.Select(target.Handle,chooser.Handle);Settle();
            Check("미리 선택한 실제 창이 가림 창보다 앞으로 나옴",Native.WindowFromPoint(point)==target.Handle);
            nint afterPeek=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(afterPeek,out uint afterPeekPid);
            // The user may click another external app during this test. That is
            // allowed; none of our no-activate preview windows may take focus.
            Check("미리 선택은 키보드 포커스를 빼앗지 않음",afterPeek==foreground||afterPeekPid!=Environment.ProcessId);
            peek.Restore();Settle();
            Check("취소 시 원래 창 겹침 복원",Native.WindowFromPoint(point)==cover.Handle);
            Check("취소 시 대상의 일반 창 속성 복원",!Topmost(target.Handle));
            peek.Select(target.Handle,chooser.Handle);Settle();peek.Accept();Settle();
            Check("선택 확정 후 임시 always-on-top 속성이 남지 않음",!Topmost(target.Handle));
            target.Hide();cover.Hide();
            string fixtureReport=Path.Combine(Preferences.DataDirectory,"peek-fixture-"+Guid.NewGuid().ToString("N")+".json");
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.ArgumentList.Add("--peek-fixture");info.ArgumentList.Add(fixtureReport);
            using var fixture=Process.Start(info)!;
            try{
                var wait=Stopwatch.StartNew();while(!File.Exists(fixtureReport)&&!fixture.HasExited&&wait.ElapsedMilliseconds<6000){Application.DoEvents();Thread.Sleep(10);}
                using var data=JsonDocument.Parse(File.ReadAllText(fixtureReport));var root=data.RootElement;
                nint foreignTarget=(nint)root.GetProperty("target").GetInt64(),foreignCover=(nint)root.GetProperty("cover").GetInt64();
                var foreignPoint=new Native.Point{X=root.GetProperty("x").GetInt32(),Y=root.GetProperty("y").GetInt32()};
                Native.SetWindowPos(chooser.Handle,-1,0,0,0,0,0x13);Settle();
                Observe("foreign-cover",foreignPoint,foreignCover);
                Check("별도 프로세스에서 가려진 대상 재현",Native.WindowFromPoint(foreignPoint)==foreignCover);
                var latency=Stopwatch.StartNew();peek.Select(foreignTarget,chooser.Handle);
                while(Native.WindowFromPoint(foreignPoint)!=foreignTarget&&latency.ElapsedMilliseconds<500){Application.DoEvents();Thread.Sleep(1);}
                Check("별도 프로세스 실제 창이 최상단 대상이 됨",Native.WindowFromPoint(foreignPoint)==foreignTarget);
                result.Add(new{name="별도 프로세스 창 표시 요청부터 확인까지 ms",value=latency.Elapsed.TotalMilliseconds});
                peek.Restore();Settle();Check("별도 프로세스 취소 시 가림 순서와 속성 복원",Native.WindowFromPoint(foreignPoint)==foreignCover&&!Topmost(foreignTarget));
                bool repeated=true;
                for(int i=0;i<5;i++){peek.Select(foreignTarget,chooser.Handle);Settle();repeated&=Native.WindowFromPoint(foreignPoint)==foreignTarget;peek.Restore();Settle();repeated&=!Topmost(foreignTarget)&&Native.WindowFromPoint(foreignPoint)==foreignCover;}
                Check("별도 프로세스 5회 반복 선택 취소 안정성",repeated);
            }finally{peek.Restore();if(!fixture.HasExited)fixture.Kill();fixture.WaitForExit();if(File.Exists(fixtureReport))File.Delete(fixtureReport);}
        }finally{peek.Restore();chooser.Hide();cover.Hide();target.Hide();}
        // A responsive switcher must not share an input queue with a busy app.
        // Use our own foreign process and owned topmost popup, never a user app.
        if(includeFocus) {
        string focusReport=Path.Combine(Preferences.DataDirectory,"focus-fixture-"+Guid.NewGuid().ToString("N")+".json");
        var focusInfo=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};focusInfo.ArgumentList.Add("--focus-fixture");focusInfo.ArgumentList.Add(focusReport);
        using(var fixture=Process.Start(focusInfo)!) {
            try {
                var wait=Stopwatch.StartNew();while(!File.Exists(focusReport)&&!fixture.HasExited&&wait.ElapsedMilliseconds<6000){Application.DoEvents();Thread.Sleep(10);}
                using var data=JsonDocument.Parse(File.ReadAllText(focusReport));
                nint foreign=(nint)data.RootElement.GetProperty("target").GetInt64(),popup=(nint)data.RootElement.GetProperty("popup").GetInt64();
                using var catalog=new CatalogService();
                var switcher=new SwitcherForm(new Preferences{PeekTarget=false,EmphasizeTarget=false,HighlightTarget=false},catalog){InspectionMode=true,ShowInTaskbar=true};
                try {
                    switcher.PrepareNavigationTest(new(){new(foreign,"Fixture owner","fixture","fixture","fixture.exe",0,false)});
                    switcher.Text="AltTabFlow 전환 검사 · 이 창을 클릭하면 시작";
                    switcher.InspectionMode=true;
                    switcher.Show();Native.ShowWindow(switcher.Handle,5);switcher.TestActivate();
                    wait.Restart();while(Native.GetForegroundWindow()!=switcher.Handle&&wait.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(10);}
                    Settle();
                    environment.Add(new{phase="focus-fixture",foreign=foreign.ToInt64(),popup=popup.ToInt64(),ownerRoot=WindowIdentity.Root(foreign).ToInt64(),popupRoot=WindowIdentity.Root(popup).ToInt64(),popupOwner=Native.GetWindow(popup,4).ToInt64(),topmost=Topmost(popup),ownerTopmost=Topmost(foreign),foreground=Native.GetForegroundWindow().ToInt64(),chooser=switcher.Handle.ToInt64(),visible=switcher.Visible});
                    Check("별도 프로세스 메인과 항상 위 팝업 소유 관계 재현",WindowIdentity.Same(foreign,popup)&&Topmost(popup)&&!Topmost(foreign));
                    Check("전환 검사 시작 시 선택 화면이 실제 전경",Native.GetForegroundWindow()==switcher.Handle);
                    Native.PostMessage(foreign,0x8001,0,0);
                    wait.Restart();while(!File.Exists(focusReport+".busy")&&wait.ElapsedMilliseconds<1500){Application.DoEvents();Thread.Sleep(1);}
                    Check("대상 프로세스 UI의 일시적인 지연 재현",File.Exists(focusReport+".busy"));
                    Check("활성화 직전 대상 UI가 WM_NULL에 아직 응답하지 않음",Native.SendMessageTimeout(foreign,0,0,0,0x22,5,out _)==0);
                    var latency=Stopwatch.StartNew();switcher.TestKey(Keys.Enter);double requestMs=latency.Elapsed.TotalMilliseconds;
                    Check("대상 UI가 바빠도 활성화 요청은 80ms 이내 반환",requestMs<80);
                    Check("대상 전경 확인 전에는 선택 화면을 조기 숨기지 않음",WindowIdentity.Same(Native.GetForegroundWindow(),foreign)||(switcher.Visible&&switcher.IsCommitting));
                    while(switcher.IsCommitting&&latency.ElapsedMilliseconds<700){Application.DoEvents();Thread.Sleep(1);}
                    Check("지연 대상의 실제 활성화 후 선택 화면 종료",WindowIdentity.Same(Native.GetForegroundWindow(),foreign)&&!switcher.Visible&&!switcher.IsCommitting);
                    Check("전환 후 소유 팝업의 항상 위 설정 유지",Topmost(popup)&&!Topmost(foreign));
                    result.Add(new{name="busy-target activation request ms",value=requestMs});
                    result.Add(new{name="busy-target confirmed switch ms",value=latency.Elapsed.TotalMilliseconds});
                } finally {switcher.Shutdown();}
            } catch(Exception ex){failures++;result.Add(new{name="별도 프로세스 전환 회귀 예외",passed=false,error=ex.ToString()});}
            finally{if(!fixture.HasExited)fixture.Kill();fixture.WaitForExit();if(File.Exists(focusReport))File.Delete(focusReport);if(File.Exists(focusReport+".busy"))File.Delete(focusReport+".busy");}
        }
        }
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,includeFocus?"focus-test.json":"peek-test.json"),JsonSerializer.Serialize(new{failures,tests=result,environment},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures==0?0:1;
    }
}
