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
    public static void RunFixture(string report){
        var work=(Screen.PrimaryScreen??Screen.AllScreens[0]).WorkingArea;
        using var target=new TestWindow{Text="AltTabFlow 별도 프로세스 대상",Bounds=new Rectangle(work.Left+35,work.Top+40,470,270),BackColor=Color.SeaGreen};
        using var cover=new TestWindow{Text="AltTabFlow 별도 프로세스 가림",Bounds=target.Bounds,BackColor=Color.IndianRed,TopMost=true};
        target.Show();cover.Show();
        File.WriteAllText(report,JsonSerializer.Serialize(new{target=target.Handle.ToInt64(),cover=cover.Handle.ToInt64(),x=target.Left+55,y=target.Top+55}));
        using var timeout=new System.Windows.Forms.Timer{Interval=12000};timeout.Tick+=(_,_)=>Application.ExitThread();timeout.Start();Application.Run();
    }
    public static void Run(){
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
            Check("미리 선택은 키보드 포커스를 빼앗지 않음",Native.GetForegroundWindow()==foreground);
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
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,"peek-test.json"),JsonSerializer.Serialize(new{failures,tests=result,environment},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures==0?0:1;
    }
}
