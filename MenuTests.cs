using System.Text.Json;

namespace AltTabFlow;

// Native menu lifetime regression. It filters a mouse message addressed only to
// our disposable form; no global input is injected and no user window is changed.
internal static class MenuTests
{
    public static void Run() {
        var tests=new List<object>();int failures=0;
        void Check(string name,bool passed){tests.Add(new{name,passed});if(!passed)failures++;}
        using var catalog=new CatalogService();
        var preferences=new Preferences{HighlightTarget=false,EmphasizeTarget=false,PeekTarget=false};
        nint activated=0;bool suppressed=false;
        var form=new SwitcherForm(preferences,catalog,h=>{activated=h;return true;}){InspectionMode=true};
        form.ShortcutSuppression=value=>suppressed=value;
        var rows=new List<WindowInfo>{
            new(101,"Test Chrome 1","Chrome","chrome","C:/MenuFixture/chrome.exe",0,false),
            new(102,"Test Chrome 2","Chrome","chrome","C:/MenuFixture/chrome.exe",1,false),
            new(201,"Test Excel","Excel","excel","C:/MenuFixture/excel.exe",2,false)};
        ContextMenuStrip? menu=null;
        try {
            form.PrepareNavigationTest(rows);form.Show();
            menu=form.TestItemMenu(0);
            Check("항목 메뉴가 실제로 표시되고 전환 단축키를 잠시 중지",menu.Visible&&suppressed&&form.IsConfiguring);
            // The same ModalMenuFilter -> SetVisibleCore path found in the crash log.
            var outside=Message.Create(form.Handle,0x201,1,(nint)((8<<16)|8));
            Application.FilterMessage(ref outside);
            Check("메뉴 밖 클릭 필터 처리 후 disposed 예외 없이 닫힘",!menu.Visible&&!menu.IsDisposed);
            Check("메뉴 닫기 후 설정 상태와 단축키 억제 해제",!suppressed&&!form.IsConfiguring);
            var original=menu;bool repeated=true;
            var reasons=new[]{ToolStripDropDownCloseReason.Keyboard,ToolStripDropDownCloseReason.AppClicked,ToolStripDropDownCloseReason.AppFocusChange};
            for(int i=0;i<30;i++){
                menu=form.TestItemMenu(i%2);
                repeated&=ReferenceEquals(menu,original)&&menu.Visible;
                menu.Close(reasons[i%reasons.Length]);
                repeated&=!menu.Visible&&!menu.IsDisposed&&!suppressed&&!form.IsConfiguring;
            }
            Check("Esc·외부 클릭·포커스 변경 닫기 30회 후 같은 메뉴 재사용",repeated);
            menu=form.TestItemMenu(0);menu.Items[1].PerformClick();menu.Close(ToolStripDropDownCloseReason.ItemClicked);
            Check("메뉴의 묶음 펼치기 뒤 창과 상세 모드 유지",form.NavigationState.Group!=null&&form.NavigationState.Count==2&&form.IsPersistent&&!menu.IsDisposed);
            form.TestKey(Keys.Back);
            menu=form.TestItemMenu(0);menu.Items[0].PerformClick();menu.Close(ToolStripDropDownCloseReason.ItemClicked);
            Check("메뉴의 창 전환 후 정상 종료와 단축키 복구",activated==101&&!form.Visible&&!suppressed&&!menu.IsDisposed);
            form.PrepareNavigationTest(rows);form.Show();menu=form.TestItemMenu(1);
            form.Shutdown();
            Check("메뉴가 열린 채 프로그램 정리해도 안전하게 해제",form.IsDisposed&&menu.IsDisposed);
        } catch(Exception ex) {
            failures++;tests.Add(new{name="실제 메뉴 수명 회귀 검사 예외",passed=false,error=ex.GetType().Name,message=ex.Message,stack=ex.StackTrace});
        } finally {
            if(!form.IsDisposed)form.Shutdown();
            if(menu!=null&&!menu.IsDisposed)menu.Dispose();
        }
        File.WriteAllText(Path.Combine(Preferences.DataDirectory,"menu-test.json"),JsonSerializer.Serialize(new{failures,tests},new JsonSerializerOptions{WriteIndented=true}));
        Environment.ExitCode=failures==0?0:1;
    }
}
