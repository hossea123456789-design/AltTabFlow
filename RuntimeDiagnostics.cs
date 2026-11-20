using System.Collections.Concurrent;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace AltTabFlow;

// All routine messages are bounded and asynchronous. Hook callbacks never
// write to the filesystem or wait for the logging worker.
internal sealed class DiagnosticLog : IDisposable {
    private sealed record Entry(DateTimeOffset Time,long Sequence,string Event,object Data,TaskCompletionSource<bool>? Barrier=null);
    private readonly BlockingCollection<Entry> queue=new(512);
    private readonly Thread worker;
    private readonly object criticalGate=new();
    private readonly string directory,runId;
    private readonly long maxBytes;
    private long sequence,dropped,writeErrors;
    private int disposed;
    internal DiagnosticLog(string directory,string runId,long maxBytes=1_000_000){
        this.directory=directory;this.runId=runId;this.maxBytes=maxBytes;
        worker=new Thread(Drain){IsBackground=true,Name="AltTabFlow diagnostics"};
        worker.Start();
    }
    internal object Statistics=>new{queued=queue.Count,dropped=Interlocked.Read(ref dropped),
        writeErrors=Interlocked.Read(ref writeErrors),workerAlive=worker.IsAlive};
    private Entry New(string evt,object data)=>new(DateTimeOffset.Now,Interlocked.Increment(ref sequence),evt,data);
    internal void Write(string evt,object data){
        try{if(!queue.TryAdd(New(evt,data)))Interlocked.Increment(ref dropped);}
        catch(InvalidOperationException){Interlocked.Increment(ref dropped);}
    }
    internal bool Critical(string evt,object data){
        if(!Monitor.TryEnter(criticalGate,1000)){Interlocked.Increment(ref writeErrors);return false;}
        try{Append("critical.jsonl",New(evt,data),true);return true;}
        catch{Interlocked.Increment(ref writeErrors);return false;}
        finally{Monitor.Exit(criticalGate);}
    }
    internal bool Flush(int timeoutMs=1500){
        var barrier=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try{
            if(!queue.TryAdd(new(DateTimeOffset.Now,0,"",new{},barrier),Math.Min(100,timeoutMs)))return false;
            return barrier.Task.Wait(timeoutMs)&&barrier.Task.Result;
        }catch(InvalidOperationException){return false;}
    }
    private void Drain(){
        foreach(var item in queue.GetConsumingEnumerable()){
            if(item.Barrier!=null){item.Barrier.TrySetResult(true);continue;}
            try{Append("events.jsonl",item,false);}
            catch{Interlocked.Increment(ref writeErrors);}
        }
    }
    private void Append(string file,Entry item,bool durable){
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,file);
        if(File.Exists(path)&&new FileInfo(path).Length>=maxBytes){
            for(int i=3;i>=1;i--)if(File.Exists(path+"."+i))File.Move(path+"."+i,path+"."+(i+1),true);
            File.Move(path,path+".1",true);
        }
        byte[] bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{
            time=item.Time,pid=Environment.ProcessId,runId,seq=item.Sequence,
            evt=item.Event,data=item.Data})+Environment.NewLine);
        using var stream=new FileStream(path,FileMode.Append,FileAccess.Write,
            FileShare.ReadWrite|FileShare.Delete);
        stream.Write(bytes);
        if(durable)stream.Flush(true);
    }
    public void Dispose(){
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        queue.CompleteAdding();
        worker.Join(1500);
    }
}
internal static class AtomicFile {
    internal static void Write(string path,string contents){
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var stream=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                var bytes=Encoding.UTF8.GetBytes(contents);stream.Write(bytes);stream.Flush(true);
            }
            File.Move(tmp,path,true);
        }finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
}
internal sealed record RunState(string RunId,int Pid,string Version,string? Executable,bool Elevated,
    DateTimeOffset StartedAt,DateTimeOffset LastSeen,string State,
    string? ExitReason=null,int? ExitCode=null,object? Health=null);
internal sealed class RunJournal {
    private readonly object gate=new();
    private readonly string path;
    private readonly DiagnosticLog log;
    private RunState state;
    internal RunJournal(string directory,DiagnosticLog log,string runId){
        path=Path.Combine(directory,"session.json");this.log=log;
        bool elevated=false;
        try{using var current=WindowsIdentity.GetCurrent();
            elevated=new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator);}
        catch{}
        var now=DateTimeOffset.Now;
        state=new(runId,Environment.ProcessId,typeof(Program).Assembly.GetName().Version?.ToString()??"unknown",
            Environment.ProcessPath,elevated,now,now,"running");
    }
    internal RunState Snapshot{get{lock(gate)return state;}}
    internal void Begin(){
        lock(gate){
            if(File.Exists(path))try{
                var previous=JsonSerializer.Deserialize<RunState>(File.ReadAllText(path));
                if(previous!=null){
                    AtomicFile.Write(Path.Combine(Path.GetDirectoryName(path)!,"session.previous.json"),
                        File.ReadAllText(path));
                    if(previous.State!="clean")
                        log.Critical("previous-session-incomplete",new{previous,
                            note="No clean shutdown record; terminating process and exact time are unknown."});
                }
            }catch(Exception ex){log.Critical("session-read-error",new{error=ex.ToString()});}
            Persist();
            log.Critical("session-start",state);
        }
    }
    private void Persist(){
        try{AtomicFile.Write(path,JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception ex){log.Critical("session-write-error",new{error=ex.ToString()});}
    }
    internal void Heartbeat(object health){
        lock(gate){
            if(state.State!="running")return;
            state=state with{LastSeen=DateTimeOffset.Now,Health=health};Persist();
            log.Write("heartbeat",new{health,logging=log.Statistics});
        }
    }
    internal void Stopping(string reason){
        lock(gate){
            if(state.State!="running")return;
            state=state with{LastSeen=DateTimeOffset.Now,State="stopping",ExitReason=reason};
            log.Critical("shutdown-request",new{reason});Persist();
        }
    }
    internal void Fault(string reason){
        lock(gate){state=state with{LastSeen=DateTimeOffset.Now,State="faulted",ExitReason=reason};Persist();}
    }
    internal void Finish(bool normal,int exitCode){
        lock(gate){
            state=state with{LastSeen=DateTimeOffset.Now,
                State=normal&&state.State=="stopping"?"clean":"incomplete",ExitCode=exitCode};
            log.Critical("session-end",state);Persist();
        }
    }
}
internal static class AppLog {
    public static bool Suppressed;
    private static DiagnosticLog? log;
    private static RunJournal? journal;
    private static int cleanupErrors;
    internal static bool CleanupFailed=>Volatile.Read(ref cleanupErrors)!=0;
    internal static object Statistics=>new{logging=log?.Statistics,session=journal?.Snapshot,
        cleanupErrors=Volatile.Read(ref cleanupErrors)};
    internal static void BeginSession(){
        if(Suppressed)return;
        string id=Guid.NewGuid().ToString("N");
        log=new DiagnosticLog(Preferences.DataDirectory,id);
        journal=new RunJournal(Preferences.DataDirectory,log,id);
        journal.Begin();
        try{
            Microsoft.Win32.SystemEvents.SessionEnding+=(_,e)=>Critical("windows-session-ending",
                new{reason=e.Reason.ToString()});
            Microsoft.Win32.SystemEvents.SessionEnded+=(_,e)=>Critical("windows-session-ended",
                new{reason=e.Reason.ToString()});
        }catch(Exception ex){Error("session-events-registration-error",ex);}
    }
    public static void Write(string evt,object data){if(!Suppressed)log?.Write(evt,data);}
    internal static void Critical(string evt,object data){if(!Suppressed)log?.Critical(evt,data);}
    internal static void Error(string evt,Exception ex)=>Critical(evt,new{type=ex.GetType().FullName,error=ex.ToString()});
    internal static void Fatal(object error,bool terminating){
        Critical("fatal-error",new{error=error.ToString(),terminating});
        if(terminating)journal?.Fault("unhandled-exception");
    }
    internal static void Stopping(string reason){if(!Suppressed)journal?.Stopping(reason);}
    internal static void Heartbeat(object health){if(!Suppressed)journal?.Heartbeat(health);}
    internal static void CleanupError(string step,Exception ex){
        if(!Suppressed)Interlocked.Increment(ref cleanupErrors);
        Critical("cleanup-error",new{step,error=ex.ToString()});
    }
    internal static void Finish(bool normal){
        if(Suppressed)return;
        journal?.Finish(normal&&!CleanupFailed,Environment.ExitCode);
        log?.Flush();log?.Dispose();
    }
    internal static void ProcessExiting(){
        if(Suppressed||journal==null||journal.Snapshot.State=="clean")return;
        Critical("process-exit-without-clean-shutdown",new{Environment.ExitCode,
            state=journal.Snapshot.State});
    }
    internal static void UseFixture(DiagnosticLog target,RunJournal journalTarget){
        log=target;journal=journalTarget;Suppressed=false;
    }
}
internal sealed class CallbackGuard {
    private readonly string name;
    private long failures,nextReport;
    internal CallbackGuard(string name){this.name=name;}
    internal long Failures=>Interlocked.Read(ref failures);
    internal bool Invoke(Action callback){
        try{callback();return true;}
        catch(Exception ex){Report(ex);return false;}
    }
    internal void Report(Exception ex){
        Interlocked.Increment(ref failures);
        long now=Environment.TickCount64;
        if(now>=Volatile.Read(ref nextReport)){
            Volatile.Write(ref nextReport,now+30_000);
            AppLog.Write("callback-error",new{boundary=name,error=ex.ToString(),failures=Failures});
        }
    }
}
internal static class CleanupSteps {
    internal static bool Run(params (string Name,Action Action)[] steps){
        bool ok=true;
        foreach(var step in steps){
            try{step.Action();}
            catch(Exception ex){ok=false;AppLog.CleanupError(step.Name,ex);}
        }
        return ok;
    }
}
