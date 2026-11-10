using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;

namespace AltTabFlow;

// One-time elevated task installation for the SAME interactive Windows user.
// A SYSTEM/service task is incorrect: it cannot display the switcher in the
// user's desktop session. Task Scheduler COM with InteractiveToken/Highest does.
internal static class ElevatedStartup
{
    internal static string TaskName(string sid)=>"AltTabFlow.Elevated."+sid.Replace('-','_');
    internal static bool UseRegistryRun(bool start,bool elevated)=>start&&!elevated;
    internal static bool UseElevatedTask(bool start,bool elevated)=>start&&elevated;
    internal static string LaunchArguments=>"--background";
    // A highest-privilege task must NOT point at a user-writable Downloads,
    // Desktop or AppData executable. Install the signed/verified candidate
    // into the admin-protected Program Files folder first.
    internal static string ProtectedInstallPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AltTabFlow","AltTabFlow.exe");
    private static WindowsIdentity Identity=>WindowsIdentity.GetCurrent();
    private static string CurrentSid=>Identity.User?.Value??throw new InvalidOperationException("Windows 사용자 SID를 확인할 수 없습니다.");

    public static bool IsInstalled {
        get {
            try {
                dynamic service=OpenService();
                dynamic folder=service.GetFolder("\\");
                dynamic task=folder.GetTask(TaskName(CurrentSid));
                return task!=null;
            } catch {return false;}
        }
    }

    private static dynamic OpenService()
    {
        var type=Type.GetTypeFromProgID("Schedule.Service",throwOnError:true)
            ??throw new NotSupportedException("Windows 작업 스케줄러를 사용할 수 없습니다.");
        dynamic service=Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    internal static bool Configure(IWin32Window owner,bool enable)
    {
        if(enable&&string.IsNullOrWhiteSpace(Environment.ProcessPath))
            throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
        if(enable&&MessageBox.Show(owner,
            "Windows 로그인 시 AltTabFlow를 관리자 권한으로 자동 실행합니다.\n\n한 번만 UAC 승인이 필요하며, 이후 로그인에서는 관리자 확인창 없이 실행됩니다.\n실행 파일은 보안을 위해 Program Files 보호 폴더에 설치됩니다.\n\n설정하시겠습니까?",
            "관리자 자동 시작 등록",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return false;

        var info=new ProcessStartInfo(Environment.ProcessPath!){
            UseShellExecute=true,Verb="runas",WorkingDirectory=AppContext.BaseDirectory
        };
        info.ArgumentList.Add("--startup-task");
        info.ArgumentList.Add(enable?"install":"remove");
        info.ArgumentList.Add(CurrentSid);
        info.ArgumentList.Add(Environment.ProcessPath!);
        try {
            using var child=Process.Start(info)??throw new IOException("관리자 작업 등록 도우미를 실행하지 못했습니다.");
            if(!child.WaitForExit(30000))throw new TimeoutException("작업 등록 시간이 초과되었습니다.");
            if(child.ExitCode!=0)throw new InvalidOperationException("Windows 작업 스케줄러가 요청을 완료하지 못했습니다. 코드: "+child.ExitCode);
            if(enable&&!IsInstalled)throw new InvalidOperationException("관리자 자동 시작 작업을 확인하지 못했습니다.");
            if(!enable&&IsInstalled)throw new InvalidOperationException("관리자 자동 시작 작업이 남아 있습니다.");
            return true;
        } catch(Win32Exception ex) when(ex.NativeErrorCode==1223){
            return false; // UAC cancelled, leave original startup selection untouched.
        }
    }

    internal static int RunHelper(string[] args)
    {
        if(args.Length!=4||args[0]!="--startup-task"||
           (args[1]!="install"&&args[1]!="remove"))return 2;
        if(!GameCompatibility.IsElevated)return 3;
        if(!string.Equals(args[2],CurrentSid,StringComparison.OrdinalIgnoreCase)){
            // Do not silently register a different administrative account's task.
            MessageBox.Show("관리자 승인이 현재 로그인한 Windows 계정과 다른 계정으로 이루어졌습니다.\n사용자의 관리자 권한 계정에서만 자동 시작을 설정할 수 있습니다.",
                "AltTabFlow 관리자 자동 시작",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return 4;
        }
        try {
            var service=OpenService();
            dynamic folder=service.GetFolder("\\");
            string name=TaskName(CurrentSid);
            if(args[1]=="remove") {
                try{folder.DeleteTask(name,0);}catch(System.Runtime.InteropServices.COMException ex)
                    when((uint)ex.HResult==0x80070002u){}
                return 0;
            }
            string file=Path.GetFullPath(args[3]);
            if(!File.Exists(file)||!string.Equals(Path.GetFileName(file),"AltTabFlow.exe",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("유효한 AltTabFlow.exe가 아닙니다.");
            string protectedPath=ProtectedInstallPath;
            Directory.CreateDirectory(Path.GetDirectoryName(protectedPath)!);
            if(!string.Equals(file,protectedPath,StringComparison.OrdinalIgnoreCase))
                File.Copy(file,protectedPath,true);
            // Copy optional game compatibility scripts alongside the protected EXE.
            foreach(var companion in new[]{"Game-compat-mode.cmd","Run-GameCompat.ps1","사용법.txt"}){
                string source=Path.Combine(Path.GetDirectoryName(file)!,companion);
                if(File.Exists(source))File.Copy(source,Path.Combine(Path.GetDirectoryName(protectedPath)!,companion),true);
            }
            dynamic definition=service.NewTask(0);
            definition.RegistrationInfo.Description="AltTabFlow - Windows 로그인 시 현재 사용자로 관리자 권한 창 전환 실행";
            definition.Principal.UserId=Identity.Name;
            definition.Principal.LogonType=3;  // TASK_LOGON_INTERACTIVE_TOKEN
            definition.Principal.RunLevel=1;   // TASK_RUNLEVEL_HIGHEST
            dynamic trigger=definition.Triggers.Create(9); // TASK_TRIGGER_LOGON
            trigger.UserId=Identity.Name;
            trigger.Enabled=true;
            dynamic action=definition.Actions.Create(0); // TASK_ACTION_EXEC
            action.Path=protectedPath;
            action.Arguments=LaunchArguments;
            action.WorkingDirectory=Path.GetDirectoryName(protectedPath)!;
            definition.Settings.Enabled=true;
            definition.Settings.StartWhenAvailable=true;
            definition.Settings.ExecutionTimeLimit="PT0S";
            definition.Settings.DisallowStartIfOnBatteries=false;
            definition.Settings.StopIfGoingOnBatteries=false;
            definition.Settings.MultipleInstances=2; // TASK_INSTANCES_IGNORE_NEW
            folder.RegisterTaskDefinition(name,definition,6,Identity.Name,null,3,null); // CREATE_OR_UPDATE, interactive
            return 0;
        } catch(Exception ex){
            MessageBox.Show("작업 스케줄러에 관리자 자동 시작을 등록하지 못했습니다.\n"+ex.Message,
                "AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return 5;
        }
    }
}

internal static class StartupTests
{
    public static void Run()
    {
        var tests=new List<object>();int failures=0;
        void Check(string name,bool ok){tests.Add(new{name,passed=ok});if(!ok)failures++;}
        Check("Elevated startup is opt-in",!ElevatedStartup.UseElevatedTask(true,false));
        Check("Explicit elevated start uses task",ElevatedStartup.UseElevatedTask(true,true));
        Check("Elevated disabled with autostart off",!ElevatedStartup.UseElevatedTask(false,true));
        Check("Normal autostart uses Run key",ElevatedStartup.UseRegistryRun(true,false));
        Check("Elevated autostart suppresses Run key",!ElevatedStartup.UseRegistryRun(true,true));
        Check("No autostart suppresses Run key",!ElevatedStartup.UseRegistryRun(false,false));
        Check("Elevated task uses background mode",ElevatedStartup.LaunchArguments=="--background");
        Check("Elevated task executable is stored under Program Files",ElevatedStartup.ProtectedInstallPath.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),StringComparison.OrdinalIgnoreCase));
        Check("Task name is stable for Windows SID",ElevatedStartup.TaskName("S-1-5-21-123")=="AltTabFlow.Elevated.S_1_5_21_123");
        Check("Task name differs across Windows accounts",ElevatedStartup.TaskName("S-1-1-1")!=ElevatedStartup.TaskName("S-1-1-2"));
        Check("Two startup mechanisms cannot both be enabled",!Enumerable.Range(0,2).Any(i=>Enumerable.Range(0,2).Any(j=>ElevatedStartup.UseRegistryRun(i!=0,j!=0)&&ElevatedStartup.UseElevatedTask(i!=0,j!=0))));
        Directory.CreateDirectory(Preferences.DataDirectory);
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,"startup-test.json"),
            JsonSerializer.Serialize(new{tests,failures},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures==0?0:1;
    }
}
