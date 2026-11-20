using System.Diagnostics;
using System.Text.Json;

namespace AltTabFlow;

// Subprocess fixtures use only their own newly-created temporary directory.
// No production mutex, hook, window, preferences or autostart is touched.
internal static class DiagnosticsTests
{
    public static void Fixture(string mode,string directory) {
        if(mode.StartsWith("settings-",StringComparison.Ordinal)){SettingsFixture(mode,directory);return;}
        using var log=new DiagnosticLog(directory,Guid.NewGuid().ToString("N"));
        var journal=new RunJournal(directory,log,Guid.NewGuid().ToString("N"));
        AppLog.UseFixture(log,journal);journal.Begin();
        switch(mode){
            case "clean":
                for(int i=0;i<100;i++)log.Write("fixture-event",new{i});
                journal.Stopping("fixture-quit");journal.Finish(true,0);log.Flush();break;
            case "fatal":
                // Exercises the real AppDomain handler; no Windows crash dialog.
                Native.SetErrorMode(0x0002|0x8000);
                new Thread(()=>throw new InvalidOperationException("AltTabFlow isolated diagnostics fixture exception")){IsBackground=false}.Start();
                Thread.Sleep(10_000);break;
            case "hold":
                journal.Heartbeat(new{fixture=true});log.Critical("fixture-ready",new{});
                Thread.Sleep(30_000);break;
            default:throw new ArgumentException("Unknown diagnostics fixture mode");
        }
    }
    public static void Run() {
        var tests=new List<object>();int failures=0;
        void Check(string name,bool passed){tests.Add(new{name,passed});if(!passed)failures++;}
        string root=Path.Combine(Path.GetTempPath(),"AltTabFlow-DiagnosticsTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        static JsonElement Read(string path)=>JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        static List<JsonElement> Lines(string path)=>File.ReadAllLines(path).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToList();
        Process Start(string mode,string dir){var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--diagnostics-fixture");info.ArgumentList.Add(mode);info.ArgumentList.Add(dir);return Process.Start(info)!;}
        try {
            string clean=Path.Combine(root,"clean");
            using(var child=Start("clean",clean)){bool exited=child.WaitForExit(8000);if(!exited)child.Kill();Check("독립 프로세스 정상 종료",exited&&child.ExitCode==0);}
            Check("정상 종료 사유와 clean 마커 보존",Read(Path.Combine(clean,"session.json")).GetProperty("State").GetString()=="clean"&&Read(Path.Combine(clean,"session.json")).GetProperty("ExitReason").GetString()=="fixture-quit");
            var events=Lines(Path.Combine(clean,"events.jsonl"));
            Check("종료 전 대기 로그 100개 배출",events.Count(x=>x.GetProperty("evt").GetString()=="fixture-event")==100);
            Check("발생 시각·실행 ID·순번 기록",events.All(x=>x.TryGetProperty("time",out _)&&x.TryGetProperty("runId",out _)&&x.GetProperty("seq").GetInt64()>0));
            using(var child=Start("clean",clean)){if(!child.WaitForExit(8000)){child.Kill();throw new TimeoutException();}}
            Check("정상 재시작을 비정상 종료로 오인하지 않음",Lines(Path.Combine(clean,"critical.jsonl")).Any(x=>x.GetProperty("evt").GetString()=="previous-session-clean")&&!Lines(Path.Combine(clean,"critical.jsonl")).Any(x=>x.GetProperty("evt").GetString()=="previous-session-incomplete"));

            string killed=Path.Combine(root,"killed");
            using(var child=Start("hold",killed)){
                try{var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<8000&&!File.Exists(Path.Combine(killed,"critical.jsonl")))Thread.Sleep(20);
                    Check("강제 종료 전 running 마커 기록",File.Exists(Path.Combine(killed,"session.json"))&&Read(Path.Combine(killed,"session.json")).GetProperty("State").GetString()=="running");
                }finally{if(!child.HasExited)child.Kill();child.WaitForExit(5000);}
            }
            using(var child=Start("clean",killed)){if(!child.WaitForExit(8000)){child.Kill();throw new TimeoutException();}}
            Check("실제 강제 종료 후 다음 실행에서 미완료 종료 탐지",Lines(Path.Combine(killed,"critical.jsonl")).Any(x=>x.GetProperty("evt").GetString()=="previous-session-incomplete"));
            Check("이전 실행 마커 별도 보존",Read(Path.Combine(killed,"session.previous.json")).GetProperty("State").GetString()=="running");

            string fatal=Path.Combine(root,"fatal");
            using(var child=Start("fatal",fatal)){bool exited=child.WaitForExit(12_000);if(!exited)child.Kill();Check("실제 백그라운드 미처리 예외 종료 재현",exited&&child.ExitCode!=0);}
            Check("종료 직전 실제 예외 형식·스택이 디스크에 남음",Lines(Path.Combine(fatal,"critical.jsonl")).Any(x=>x.GetProperty("evt").GetString()=="fatal-error"&&x.GetProperty("data").GetProperty("error").GetString()!.Contains("DiagnosticsTests")));
            Check("치명적 오류 마커는 clean으로 기록하지 않음",Read(Path.Combine(fatal,"session.json")).GetProperty("State").GetString()=="faulted");

            string rotation=Path.Combine(root,"rotation");
            using(var log=new DiagnosticLog(rotation,"rotation-test",512)){
                for(int i=0;i<40;i++){log.Write("rotate",new{i,text=new string('x',200)});log.Critical("rotate-critical",new{i,text=new string('x',200)});}
                Check("회전 로그 배출",log.Flush());
                Check("일반·중요 로그 각각 4개 백업으로 용량 제한",Directory.GetFiles(rotation).Length==10&&!File.Exists(Path.Combine(rotation,"events.jsonl.5")));
            }
            Check("회전한 모든 로그가 정상 JSONL",Directory.GetFiles(rotation).All(p=>Lines(p).Count>0));

            string blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"directory blocked");
            using(var log=new DiagnosticLog(blocked,"failure-test")){
                var watch=Stopwatch.StartNew();for(int i=0;i<20_000;i++)log.Write("burst",new{i});watch.Stop();
                Check("폭주 로그 큐는 512개로 제한",JsonSerializer.SerializeToElement(log.Statistics).GetProperty("queued").GetInt32()<=512);
                Check("로그 저장 불가에도 입력 호출은 반환",watch.ElapsedMilliseconds<3000);
                Check("중요 로그 쓰기 실패가 앱 밖으로 전파되지 않음",!log.Critical("critical",new{}));log.Flush();
                Check("저장 실패·드롭 횟수 진단 가능",JsonSerializer.SerializeToElement(log.Statistics).GetProperty("writeErrors").GetInt64()>0&&JsonSerializer.SerializeToElement(log.Statistics).GetProperty("dropped").GetInt64()>0);
            }
            bool finalCleanup=false;
            Check("일부 정리 예외 뒤에도 다음 자원 정리 수행",!CleanupSteps.Run(("fault",()=>throw new InvalidOperationException("fixture")),("final",()=>finalCleanup=true))&&finalCleanup);
            var guard=new CallbackGuard("fixture");bool laterEvent=false;
            Check("네이티브 콜백 예외가 경계 밖으로 전파되지 않음",!guard.Invoke(()=>throw new InvalidOperationException("fixture"))&&guard.Failures==1);
            Check("콜백 오류 이후 후속 이벤트 처리",guard.Invoke(()=>laterEvent=true)&&laterEvent);

            string settings=Path.Combine(root,"settings.json");var pref=new Preferences{View="카드",Pinned=new(){"excel"},StartWithWindows=false};pref.SaveTo(settings);
            pref.View="아이콘";pref.SaveTo(settings);
            Check("설정 저장 전 정상본 백업",Preferences.LoadFrom(settings+".bak").View=="카드");
            File.WriteAllText(settings,"{ damaged original");
            var recovered=Preferences.LoadFrom(settings);
            Check("손상 설정을 최신 복구 사본에서 읽고 자동 실행 선호 보존",recovered.View=="아이콘"&&!recovered.AutoStartByDefault&&recovered.Pinned.SequenceEqual(new[]{"excel"}));
            Check("읽기만으로 손상 원본을 덮어쓰지 않음",File.ReadAllText(settings)=="{ damaged original");
            recovered.SaveTo(settings);
            Check("명시적 저장 시 손상 원본을 별도 보존",Directory.GetFiles(root,"settings.json.corrupt-*").Any(p=>File.ReadAllText(p)=="{ damaged original"));
            Check("복구 저장 뒤에도 정상 백업 보존",Preferences.LoadFrom(settings+".bak").View=="카드");
            SettingsPersistenceChecks(root,Check,Start);

            string brokenJournal=Path.Combine(root,"broken-journal");Directory.CreateDirectory(brokenJournal);File.WriteAllText(Path.Combine(brokenJournal,"session.json"),"broken");
            using(var log=new DiagnosticLog(brokenJournal,"corrupt")){var journal=new RunJournal(brokenJournal,log,"corrupt");journal.Begin();Check("손상 실행 마커 때문에 앱 시작이 중단되지 않음",journal.Snapshot.State=="running"&&Lines(Path.Combine(brokenJournal,"critical.jsonl")).Any(x=>x.GetProperty("evt").GetString()=="session-read-error"));}
        }catch(Exception ex){failures++;tests.Add(new{name="진단 검증 예외",passed=false,error=ex.ToString()});}
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,"diagnostics-test.json"),JsonSerializer.Serialize(new{failures,tests,fixtureDirectory=root},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures==0?0:1;
    }
    private static Preferences SettingsSample()=>new(){View="아이콘",GroupApps=false,SortAlphabetically=true,PrioritizePinned=true,ReplaceAltTab=false,CurrentMonitorOnly=true,OnPrimaryMonitor=false,ShowPreview=true,LightTheme=true,ColorPreset="rose",HighlightTarget=false,EmphasizeTarget=false,PeekTarget=false,StartWithWindows=false,UiScale=125,Pinned=new(){"editor","sample-app"},Excluded=new(){"hidden-one","hidden-two"}};
    private static void SettingsFixture(string mode,string directory){
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"settings.json");
        if(mode=="settings-read"){var loaded=Preferences.LoadFrom(path);File.WriteAllText(Path.Combine(directory,"read.json"),JsonSerializer.Serialize(loaded));return;}
        if(mode is not ("settings-write" or "settings-hold"))throw new ArgumentException("Unknown settings fixture");
        SettingsSample().SaveTo(path);File.WriteAllText(Path.Combine(directory,"ready"),"saved");
        if(mode=="settings-hold")Thread.Sleep(30_000);
    }
    private static void SettingsPersistenceChecks(string root,Action<string,bool> check,Func<string,string,Process> start){
        string dir=Path.Combine(root,"settings-restart");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"settings.json");
        void Run(string mode){using var child=start(mode,dir);if(!child.WaitForExit(8000)){child.Kill();throw new TimeoutException("Settings fixture timeout");}if(child.ExitCode!=0)throw new InvalidOperationException("Settings fixture failed");}
        string Hash(string file)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        bool Same(Preferences p)=>p.Fingerprint()==SettingsSample().Fingerprint();
        Run("settings-write");string primary=Hash(path);Run("settings-read");
        var after=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(dir,"read.json")))!;
        check("독립 프로세스 종료·재실행 후 전체 사용자 설정 동일",Same(after));
        check("재실행 후 숨김 앱과 상단 목록 보존",after.Excluded.SequenceEqual(new[]{"hidden-one","hidden-two"})&&after.Pinned.SequenceEqual(new[]{"editor","sample-app"}));
        check("재실행 후 자동 실행 해제·정렬·테마·FX 보존",!after.AutoStartByDefault&&after.SortAlphabetically&&after.PrioritizePinned&&after.ColorPreset=="rose"&&!after.HighlightTarget&&!after.EmphasizeTarget&&!after.PeekTarget);
        check("읽기 재실행은 원본 설정을 덮어쓰지 않음",Hash(path)==primary);
        check("최초 저장부터 최신 복구 사본 생성",Hash(path+".lastgood")==primary);
        var hiddenRows=new[]{new WindowInfo(1,"Hidden","hidden-one","Hidden","",0,false),new WindowInfo(2,"Visible","visible","Visible","",1,false)};
        check("재로드한 숨김 목록을 실제 창 필터에 적용",WindowCatalog.Build(hiddenRows,after,"",null,0).Single().Window.ProcessName=="visible");

        File.Delete(path);Run("settings-read");after=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(dir,"read.json")))!;
        check("원본 설정이 없어도 최신 복구 사본으로 전체 복원",Same(after));
        check("복구 읽기는 누락 원본을 임의 생성하지 않음",!File.Exists(path));
        File.Delete(path+".lastgood");SettingsSample().SaveTo(path+".bak");
        check("원본·최신 사본 누락 시 기존 bak 복원",Same(Preferences.LoadFrom(path)));
        File.WriteAllText(path,"{ truncated");
        check("잘린 원본과 최신 사본 부재에서도 기존 bak 복원",Same(Preferences.LoadFrom(path)));
        File.WriteAllText(path+".lastgood","broken recovery");
        check("원본·최신 사본 손상 시 기존 bak 복원",Same(Preferences.LoadFrom(path)));
        File.WriteAllText(path+".bak","broken previous");
        var defaults=Preferences.LoadFrom(path);
        check("모든 사본 손상 시에도 파일을 기본값으로 덮어쓰지 않음",File.ReadAllText(path)=="{ truncated"&&File.ReadAllText(path+".lastgood")=="broken recovery"&&File.ReadAllText(path+".bak")=="broken previous");
        check("모든 사본 손상 시 기본 설정을 메모리에서만 사용",defaults.Excluded.Count==0&&defaults.View=="목록");

        SettingsSample().SaveTo(path);primary=Hash(path);
        check("손상 원본은 명시적 저장 때 별도 보존",Directory.GetFiles(dir,"settings.json.corrupt-*").Any(p=>File.ReadAllText(p)=="{ truncated"));
        File.Delete(path+".lastgood");var valid=Preferences.LoadFrom(path);
        check("기존 설치의 정상 설정에 최신 복구 사본 초기 생성",Same(valid)&&Hash(path+".lastgood")==primary&&Hash(path)==primary);
        valid.Commit(next=>next.Excluded.Add("last-hidden"),path);
        check("숨김 변경이 반환되기 전에 원본과 최신 사본에 저장",Preferences.LoadFrom(path).Excluded.Contains("last-hidden")&&Hash(path)==Hash(path+".lastgood"));
        check("이전 세대 bak은 직전 정상 설정 유지",Same(Preferences.LoadFrom(path+".bak")));
        File.WriteAllText(path,"null");
        check("null JSON도 최신 숨김 항목까지 복구",Preferences.LoadFrom(path).Excluded.Contains("last-hidden"));
        valid.SaveTo(path);
        string before=valid.Fingerprint(),beforeFile=Hash(path),beforeRecovery=Hash(path+".lastgood");bool rejected=false;
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))try{valid.Commit(next=>{next.Excluded.Clear();next.ColorPreset="violet";},path);}catch(IOException){rejected=true;}
        check("잠긴 설정 저장 실패를 호출자에게 전달",rejected);
        check("저장 실패 시 메모리의 숨김·테마 설정도 유지",valid.Fingerprint()==before);
        check("저장 실패 시 원본과 최신 사본 모두 유지",Hash(path)==beforeFile&&Hash(path+".lastgood")==beforeRecovery);
        check("실패한 저장의 임시 파일 정리",Directory.GetFiles(dir,"*.tmp").Length==0);
        valid.Commit(next=>{next.Excluded.Clear();next.Pinned.Clear();next.StartWithWindows=true;},path);
        check("사용자가 명시적으로 비운 숨김 목록을 과거 백업으로 되돌리지 않음",Preferences.LoadFrom(path).Excluded.Count==0&&Preferences.LoadFrom(path).Pinned.Count==0&&Preferences.LoadFrom(path).AutoStartByDefault);
        File.WriteAllText(path,"{\"Excluded\":[null,\" app.exe \",\"APP\",\"\"],\"Pinned\":null}");
        check("오래된 설정의 null·중복·exe 접미사 정규화",Preferences.LoadFrom(path).Excluded.SequenceEqual(new[]{"app"})&&Preferences.LoadFrom(path).Pinned.Count==0);

        File.Delete(Path.Combine(dir,"ready"));
        using(var child=start("settings-hold",dir)){
            try{var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<8000&&!File.Exists(Path.Combine(dir,"ready")))Thread.Sleep(20);if(!File.Exists(Path.Combine(dir,"ready")))throw new TimeoutException("Settings write did not finish");}
            finally{if(!child.HasExited)child.Kill();child.WaitForExit(5000);}
        }
        Run("settings-read");
        check("저장 직후 시험 프로세스 강제 종료 뒤에도 전체 설정 보존",Same(JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(dir,"read.json")))!));
    }
}
