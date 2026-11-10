using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace AltTabFlow;

// An ordinary process cannot reliably intercept keystrokes while an elevated
// full-screen game is foreground. Do not silently claim game compatibility.
internal static class GameCompatibility
{
    public static bool IsElevated {
        get {
            using var identity=WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
    public static string StatusText => IsElevated
        ? "관리자 모드: 활성 (게임 Alt+Tab 지원)"
        : "일반 권한: 관리자 게임에서는 Windows 기본 Alt+Tab이 나타날 수 있습니다.";

    public static void RequestElevatedRestart(IWin32Window owner)
    {
        if(IsElevated) {
            MessageBox.Show(owner,"이미 관리자 권한으로 실행 중입니다. 게임에서 여전히 기본 Alt+Tab이 나타나면 'Alt+Tab 전환 사용' 옵션과 일시 중지 여부를 확인하세요.","AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return;
        }
        if(MessageBox.Show(owner,"메이플스토리가 관리자 권한으로 실행 중일 때 동일한 권한의 키보드 훅이 필요할 수 있습니다.\n\nUAC에 동의하면 현재 AltTabFlow를 정상 종료하고 관리자 권한으로 다시 시작합니다. 설정은 유지됩니다.\n\n관리자 모드로 전환하시겠습니까?","게임 호환 모드",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {
                UseShellExecute=true,Verb="runas",Arguments="--background --takeover",WorkingDirectory=AppContext.BaseDirectory
            });
        } catch(Win32Exception ex) when(ex.NativeErrorCode==1223) {
            // UAC cancellation must leave the existing process running.
        } catch(Exception ex) {
            MessageBox.Show(owner,"관리자 권한 실행을 시작하지 못했습니다.\n"+ex.Message,"AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    // UAC is accepted before the old instance is shut down. The elevated
    // process then obtains the same per-user mutex; launching EXE twice while
    // the old non-elevated process lives must NOT silently reopen the old UI.
    public static bool TakeOver(Mutex mutex,Action requestOldQuit)
    {
        requestOldQuit();
        try { return mutex.WaitOne(TimeSpan.FromSeconds(12)); }
        catch(AbandonedMutexException) {return true;}
    }
}
