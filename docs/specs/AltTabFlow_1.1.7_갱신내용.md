# AltTabFlow 1.1.7 — 입력 수정과 설정 앱 아이콘

기준일: 2026-11-10. 비교 기준: 1.1.6. 이번 요청의 세 항목을 함께 반영했다.

## 1. 숨길 앱 Enter 줄바꿈

문제는 Multiline=true인 입력칸에 AcceptsReturn이 설정되지 않은 상태에서 Form.AcceptButton이 저장 버튼으로 지정된 것이었다. Enter가 기본 저장 버튼으로 넘어가 입력을 끝내고 창을 닫았다.

- 숨길 앱: Enter는 **줄바꿈만** 한다. 입력을 계속할 수 있다.
- Tab: 다음 컨트롤로 이동한다.
- 변경 사항 저장 버튼: 여러 줄의 이름을 저장하고 닫는다. `.exe` 제거·공백 정리·중복 제거는 기존 규칙을 유지한다.
- 취소/Esc: 원본 설정을 유지한다.
- 상단 정렬의 한 줄 수동 등록 입력칸: 기존처럼 Enter로 앱을 등록한다.

수정은 숨길 앱 TextBox에 `AcceptsReturn=true`, `AcceptsTab=false`를 명시했다. 실제 TextBox.PreProcessMessage와 WM_CHAR 경로로 기존 오류를 재현했으며 수정 전 3개 실패, 수정 후 줄바꿈·창 유지/저장 방지·명시적 다중 행 저장 3개 모두 통과했다. 검사 중 실제 settings.json이나 시작 프로그램은 변경하지 않는다.

## 2. Ctrl+Alt+Tab 고정 선택

| 조작 | 동작 |
| :--- | :--- |
| 일반 Alt+Tab | 모든 키를 놓으면 하이라이트된 창으로 전환하고 닫힘 |
| Ctrl+Alt+Tab | **키를 모두 놓아도 선택 화면 유지** |
| Ctrl+Alt+Tab 반복 | 다음 항목으로 이동. 열기/닫기 토글 아님 |
| Ctrl+Alt+Shift+Tab | 이전 항목으로 이동 |
| Alt+Tab 도중 Ctrl을 늦게 누름 | 현재 선택을 이동하지 않고 고정 모드로 변경 |
| Ctrl을 먼저 놓음 | 해당 제스처의 고정 상태 유지 |
| 방향키 / Tab | 이동 |
| 묶음에서 Enter | Chrome 등 다중 창 목록 펼치기 |
| Backspace | 전체 앱 목록으로 돌아가기 |
| 개별 창에서 Enter | 선택 창으로 전환하고 닫기 |
| Esc | 전환 없이 취소 |

이전에도 PinnedNext/Previous 연결은 있었지만 Tab-down 시점의 Ctrl만 검사했고, 키 해제는 고정 세션에도 Commit을 발행했다. 1.1.7에서는 ShortcutRouter의 pinnedSession을 분리하여 고정 세션에서는 Commit을 보내지 않는다. 늦은 Ctrl은 새로운 Pin 명령으로 현재 선택을 고정한다. Form의 persistent 가드도 유지한다.

Alt의 이벤트 문맥 플래그에 다른 키의 시점에서 읽은 실제 Alt 눌림을 보조로 사용한다. Alt 자신의 key-up은 이전 비동기 상태를 눌림으로 오인하지 않는다. 통계에 lastTabAltFlag/lastTabAltPhysical을 추가했다. 일시 중지와 설정 중 억제, 관리자 권한 게임 호환은 유지한다. 실제 사용 환경의 모든 실패를 이 플래그 한 가지 원인으로 확정한 것은 아니다.

## 3. 설정 앱 아이콘

상단 정렬의 앱 선택 행을 **체크 + 실제 앱 아이콘 + 앱 이름 + process.exe**로 변경했다. 체크 18px, 아이콘 28px, 행 48px이며 DPI 비율로 확대된다. 색상 미리보기의 Chrome 표시에도 같은 아이콘/대체 규칙을 사용한다.

1. 실행 중인 앱은 창 목록에서 확보한 아이콘 캐시를 즉시 사용한다.
2. 캐시에 없으면 기존 WindowCatalog.GetIcon을 작업 스레드에서 호출한다.
3. 실행하지 않은 등록 앱은 Windows HKCU/HKLM App Paths의 로컬 실행 파일 경로가 있으면 아이콘을 추출한다. 네트워크 경로나 디스크 전체를 탐색하지 않는다.
4. 실행 경로를 확인할 수 없으면 앱 이름의 첫 글자 배지로 표시하고 이름/프로세스명은 계속 보여준다.

아이콘 추출은 설정 표시 후 백그라운드에서 수행한다. 창을 닫으면 취소하고 닫힌 컨트롤을 갱신하지 않는다. 아이콘 이미지 수명은 기존 캐시가 관리한다. AppOption을 참조 동일성이 안정된 class로 유지하여 아이콘이 추가될 때 HashSet의 체크 선택이 사라지지 않게 했다.

## 4. 검증

| 검사 | 통과 |
| :--- | :--- |
| 기본 입력·선택 | 161개 |
| 실제 창 미리 표시 | 10개 |
| 메뉴 수명 | 7개 |
| 전경 권한·바쁜 대상 | 18개, 앞의 창 10개 중복 포함 |
| UI·줄바꿈·아이콘 | 71개 |
| 중복 제외 총합 | **257개** |

새 기본 44개는 Ctrl/Alt/Tab 6개 해제 순서, 정상 Alt+Tab으로 복귀, 늦은 Ctrl, Alt 보강, 목록·카드·아이콘의 실제 폼 열기/반복/역방향/묶음/확정/취소를 포함한다. 새 UI 9개는 Enter 3개와 아이콘 6개다. 테스트는 자체 폼·메시지·대체 활성화 함수를 사용하며 사용자 창에 키를 합성하지 않는다.

일반 권한 검증의 전경 검사에서 실패가 발생했으며 당시 실제 foreground는 관리자 권한 메이플스토리였다. 동일 바이너리의 단독 재검사는 통과했지만 일반 권한 전체 패키징에서 다시 발생하여 실패 기록을 보존했다. 최종 패키징은 설치 앱과 동일한 관리자 권한으로 **전체 검사를 다시 수행**했다. 실패 검사를 생략하거나 합격으로 바꾸지 않았다.

기존 사용자 설정, 추가 입력한 숨김 앱, 색상과 관리자 권한 실행 상태를 보존했다. 설정 파일의 교체 전후 SHA-256이 동일하다.

## 5. 파일

- 버전: **1.1.7**
- ZIP: `AltTabFlow-v1.1.7-win-x64-20261110-160900.zip`
- 크기: 70,593,050 bytes
- ZIP SHA-256: `A0BCF79D669C762223C028B6DF82B77A6E40C252520D013E1206E6DFAED7C146`
- EXE SHA-256: `F2F0F5D9A764081FEEBF30D0A8744E07933EE243882A941B1A6318D2DFF2BD37`
- 전체 명세: `AltTabFlow_재구현_완전명세.md` — 최신 요구사항과 29개 소스·자원 복원 데이터.
- 전체 명세 갱신: 2장 일반/고정 입력, 9.1 설정 줄바꿈·아이콘, 10.2 훅, 11.6 이번 수정, 파일/검사/이슈 표와 소스 부록.

참조: [TextBox.AcceptsReturn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.textbox.acceptsreturn?view=windowsdesktop-8.0), [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc). 구현과 검증의 1차 근거는 프로젝트 소스와 검증 JSON이다.

## 6. 1.1.6 대비 실제 변경 소스

아래는 이전 MD에서 복원한 1.1.6 소스와 비교한 9개 텍스트 파일이다. 독립 복원에는 전체 명세를 사용한다.

### AltTabFlow.csproj

~~~~~diff
--- 1.1.6/AltTabFlow.csproj
+++ 1.1.7/AltTabFlow.csproj
@@ -7,7 +7,7 @@
     <Nullable>enable</Nullable>
     <ApplicationManifest>app.manifest</ApplicationManifest>
     <AssemblyName>AltTabFlow</AssemblyName>
-    <Version>1.1.6</Version>
+    <Version>1.1.7</Version>
     <Company>Local Tools</Company>
     <Product>AltTabFlow</Product>
     <Description>앱별로 정리하는 창 전환기</Description>
~~~~~

### Program.cs

~~~~~diff
--- 1.1.6/Program.cs
+++ 1.1.7/Program.cs
@@ -127,6 +127,7 @@
             case SwitchCommand.Previous: form.OpenSwitcher(false,-1); break;
             case SwitchCommand.PinnedNext: form.OpenSwitcher(true,1); break;
             case SwitchCommand.PinnedPrevious: form.OpenSwitcher(true,-1); break;
+            case SwitchCommand.Pin: form.PinSelection(); break;
             case SwitchCommand.SameApp: form.OpenSwitcher(false,1,true); break;
             case SwitchCommand.SameAppPrevious: form.OpenSwitcher(false,-1,true); break;
             case SwitchCommand.Persistent: form.TogglePersistent(); break;
@@ -255,7 +256,7 @@
         commands.Clear();Route(0xA2,ctrl:true);Route(9,ctrl:false);Route(9,false);Route(0xA4,false);
         Check("Ctrl-up 누락 뒤 실제 Ctrl 해제 상태면 정상 빠른 전환",commands.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
         commands.Clear();for(int i=0;i<3;i++){Route(9,ctrl:true);Route(9,false,ctrl:true);}Route(0xA4,false);
-        Check("Ctrl+Alt+Tab 반복도 닫기 토글 없이 계속 이동",commands.SequenceEqual(new[]{SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.Commit}));
+        Check("Ctrl+Alt+Tab 반복은 계속 이동하며 키 해제에 Commit 없음",commands.SequenceEqual(new[]{SwitchCommand.PinnedNext,SwitchCommand.PinnedNext,SwitchCommand.PinnedNext}));
         commands.Clear();Route(9,shift:true);Route(9,false);Route(0xA4,false);
         Check("Shift 역방향 후 Alt 해제 확정",commands.SequenceEqual(new[]{SwitchCommand.Previous,SwitchCommand.Commit}));
         commands.Clear();Route(9);Route(9,false);Route(0x27);Route(0x27,false);Route(0xA4,false);
@@ -285,8 +286,8 @@
         Check("양쪽 Shift 중 하나만 놓아도 계속 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0xA1,false,shift:true);
         Check("현재 Shift-up 이벤트로 마지막 해제 판단",releaseCommands.Last()==SwitchCommand.Commit);
         BeginReleaseCase();Key(0xA2,true,true,ctrl:true);Key(0xA4,false,ctrl:true);Key(0xA2,false,ctrl:true,otherCtrl:true);
-        Check("양쪽 Ctrl 중 하나만 놓아도 계속 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0xA3,false,ctrl:true);
-        Check("현재 Ctrl-up 이벤트로 마지막 해제 판단",releaseCommands.Last()==SwitchCommand.Commit);
+        Check("AltTab 중 Ctrl을 누르면 고정 모드로 승격",releaseCommands.Last()==SwitchCommand.Pin);Key(0xA3,false,ctrl:true);
+        Check("고정 모드는 마지막 Ctrl 해제에도 Commit 없음",!releaseCommands.Contains(SwitchCommand.Commit));
         BeginReleaseCase();Key(0x27,true,true);Key(0x1B,true,true);Key(0x1B,false,true);Key(0xA4,false);Key(0x27,false);
         Check("Esc 취소 후 남은 키를 놓아도 전환하지 않음",releaseCommands.Last()==SwitchCommand.Cancel&&!releaseCommands.Contains(SwitchCommand.Commit));
         BeginReleaseCase();Key(0x0D,true,true);Key(0xA4,false);
@@ -295,6 +296,24 @@
         releaseCommands.Clear();releaseRouter=new ShortcutRouter(releaseCommands.Add);Key(0x27,true);Key(0xA4,true);Key(9,true,true);Key(9,false,true);Key(0xA4,false);
         Check("AltTab 전에 누른 화살표도 마지막 해제까지 기다림",!releaseCommands.Contains(SwitchCommand.Commit));Key(0x27,false);
         Check("시작 전에 누른 화살표 해제도 확정",releaseCommands.Last()==SwitchCommand.Commit);
+        Check("Alt 문맥 비트가 없어도 Tab에서 실제 Alt 눌림을 인식",KeyboardHook.AltSnapshot(9,0,true));
+        Check("Alt 자체 key-up은 이전 비동기 눌림으로 되살리지 않음",!KeyboardHook.AltSnapshot(0xA4,0,true));
+        Check("Alt 문맥·실제 눌림 모두 없으면 일반 Tab",!KeyboardHook.AltSnapshot(9,0,false));
+        foreach(var order in new[]{new uint[]{9,0xA4,0xA2},new uint[]{9,0xA2,0xA4},new uint[]{0xA4,9,0xA2},new uint[]{0xA4,0xA2,9},new uint[]{0xA2,9,0xA4},new uint[]{0xA2,0xA4,9}}){
+            var emitted=new List<SwitchCommand>();var pinnedRouter=new ShortcutRouter(emitted.Add);
+            pinnedRouter.Route(0xA2,true,false,false,false,true);pinnedRouter.Route(0xA4,true,false,true,false,true);
+            bool consumed=pinnedRouter.Route(9,true,KeyboardHook.AltSnapshot(9,0,true),true,false,true);
+            bool altHeld=true,ctrlHeld=true;
+            foreach(uint key in order){if(key==0xA4)altHeld=false;if(key==0xA2)ctrlHeld=false;pinnedRouter.Route(key,false,altHeld,ctrlHeld,false,true);}
+            Check("CtrlAltTab 해제 순서 "+string.Join("/",order)+"는 고정 열기만 전달",consumed&&emitted.SequenceEqual(new[]{SwitchCommand.PinnedNext}));
+            emitted.Clear();pinnedRouter.Route(9,true,true,false,false,true);pinnedRouter.Route(9,false,true,false,false,true);pinnedRouter.Route(0xA4,false,false,false,false,true);
+            Check("고정 제스처 이후 일반 AltTab 세션 초기화 "+string.Join("/",order),emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
+        }
+        var promoted=new List<SwitchCommand>();var promotedRouter=new ShortcutRouter(promoted.Add);
+        promotedRouter.Route(9,true,true,false,false,true);promotedRouter.Route(9,false,true,false,false,true);promotedRouter.Route(0xA2,true,true,false,false,true);
+        Check("Tab 다음 늦은 Ctrl-down은 이동 없이 Pin 명령",promoted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Pin}));
+        promotedRouter.Route(0xA2,false,true,true,false,true);promotedRouter.Route(9,true,true,false,false,true);promotedRouter.Route(9,false,true,false,false,true);promotedRouter.Route(0xA4,false,false,false,false,true);
+        Check("Ctrl 먼저 해제 후 Tab 반복도 고정 유지하며 확정하지 않음",promoted.Last()==SwitchCommand.PinnedNext&&!promoted.Contains(SwitchCommand.Commit));
         foreach(var viewport in new[]{new Size(4048,2048),new Size(1872,976),new Size(1318,704)}){
             foreach(int count in new[]{17,22}){
                 var layout=LayoutPlanner.Plan(count,viewport.Width,viewport.Height,false);
@@ -339,6 +358,32 @@
                     Check(mode+" 펼친 Chrome 목록에서는 하이라이트한 개별 창으로 전환",activated==102&&!form.Visible);
                     activated=0;form.PrepareNavigationTest(navigationRows,true);form.Show();form.DetailedNavigate(Keys.Down);form.NavigationReleased();
                     Check(mode+" 명시적 고정 모드에서는 키 해제 후에도 화면 유지",form.Visible&&form.IsPersistent&&activated==0);form.Dismiss(false);
+                    // Route the full Ctrl+Alt+Tab gesture through the real open,
+                    // repeat, release and group-navigation methods.
+                    activated=0;
+                    var pinnedFormRouter=new ShortcutRouter(command=>{
+                        switch(command){
+                            case SwitchCommand.PinnedNext:form.OpenSwitcher(true,1);break;
+                            case SwitchCommand.PinnedPrevious:form.OpenSwitcher(true,-1);break;
+                            case SwitchCommand.Next:form.OpenSwitcher(false,1);break;
+                            case SwitchCommand.Pin:form.PinSelection();break;
+                            case SwitchCommand.Commit:form.NavigationReleased();break;
+                            case SwitchCommand.Cancel:form.Dismiss(false);break;
+                        }
+                    });
+                    void PinnedGesture(bool shift=false){pinnedFormRouter.Route(9,true,true,true,shift,true);pinnedFormRouter.Route(9,false,true,true,shift,true);pinnedFormRouter.Route(0xA2,false,true,false,shift,true);pinnedFormRouter.Route(0xA4,false,false,false,shift,true);if(shift)pinnedFormRouter.Route(0xA0,false,false,false,false,true);}
+                    PinnedGesture();Check(mode+" CtrlAltTab은 숨김 상태에서 실제 고정 화면을 엶",form.Visible&&form.IsPersistent&&activated==0);
+                    form.NavigationReleased();Check(mode+" 고정 화면은 잔여 해제 명령에도 닫히지 않음",form.Visible&&activated==0);
+                    form.PrepareNavigationTest(navigationRows,true);PinnedGesture();Check(mode+" CtrlAltTab 반복은 닫지 않고 다음 앱 선택",form.Visible&&form.IsPersistent&&form.NavigationState.Selected==200&&activated==0);
+                    PinnedGesture(true);Check(mode+" CtrlAltShiftTab은 이전 앱 선택",form.Visible&&form.NavigationState.Selected==101);
+                    form.TestKey(Keys.Enter);form.NavigationReleased();Check(mode+" 고정 Chrome 묶음 Enter 진입 후에도 유지",form.Visible&&form.NavigationState.Count==4&&activated==0);
+                    form.TestKey(Keys.Back);form.TestKey(Keys.Enter);form.TestKey(Keys.Tab);form.TestKey(Keys.Enter);Check(mode+" 고정 화면 Enter로 선택 개별 창 확정",activated==102&&!form.Visible);
+                    activated=0;form.PrepareNavigationTest(navigationRows);form.Show();pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);var beforePin=form.NavigationState.Selected;
+                    pinnedFormRouter.Route(0xA2,true,true,false,false,true);pinnedFormRouter.Route(0xA4,false,false,true,false,true);pinnedFormRouter.Route(0xA2,false,false,false,false,true);
+                    Check(mode+" 늦게 누른 Ctrl도 현재 선택을 유지하며 고정",form.Visible&&form.IsPersistent&&form.NavigationState.Selected==beforePin&&activated==0);
+                    form.TestKey(Keys.Escape);Check(mode+" 고정 화면 Esc는 전환 없이 닫힘",!form.Visible&&activated==0);
+                    form.PrepareNavigationTest(navigationRows);form.Show();pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);pinnedFormRouter.Route(0xA4,false,false,false,false,true);
+                    Check(mode+" 고정 화면 취소 뒤 일반 AltTab은 키 해제로 전환",!form.Visible&&activated==200);
                 } finally {form.Shutdown();}
             }
         }
~~~~~

### Models.cs

~~~~~diff
--- 1.1.6/Models.cs
+++ 1.1.7/Models.cs
@@ -69,6 +69,7 @@
     private static readonly ConcurrentDictionary<string, Bitmap> Icons = new(StringComparer.OrdinalIgnoreCase);
     private static readonly Bitmap FallbackIcon = SystemIcons.Application.ToBitmap();
     public static Bitmap CachedIcon(WindowInfo w) => Icons.GetValueOrDefault(w.GroupKey, FallbackIcon);
+    public static Bitmap? CachedAppIcon(WindowInfo w)=>Icons.GetValueOrDefault(w.GroupKey);
     private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) {
         ["EXCEL"]="Excel", ["chrome"]="Chrome", ["msedge"]="Edge", ["explorer"]="파일 탐색기",
         ["slack"]="Slack", ["olk"]="Outlook", ["OUTLOOK"]="Outlook", ["ChatGPT"]="Codex / ChatGPT",
~~~~~

### KeyboardHook.cs

~~~~~diff
--- 1.1.6/KeyboardHook.cs
+++ 1.1.7/KeyboardHook.cs
@@ -3,14 +3,14 @@
 
 namespace AltTabFlow;
 
-internal enum SwitchCommand { Next, Previous, PinnedNext, PinnedPrevious, SameApp, SameAppPrevious, Commit, Persistent, Pause, Left, Right, Up, Down, Cancel }
+internal enum SwitchCommand { Next, Previous, PinnedNext, PinnedPrevious, SameApp, SameAppPrevious, Commit, Persistent, Pause, Left, Right, Up, Down, Cancel, Pin }
 
 // Pure routing: modifier values are a fresh snapshot for this shortcut, never a
 // latch seeded at startup (a missed Ctrl-up used to turn Tab into a close toggle).
 internal sealed class ShortcutRouter
 {
     private readonly Action<SwitchCommand> post;
-    private bool session, swallowedTab, swallowedGrave, swallowedSpace, swallowedPause, swallowedEscape;
+    private bool session, pinnedSession, swallowedTab, swallowedGrave, swallowedSpace, swallowedPause, swallowedEscape;
     private readonly HashSet<uint> heldKeys=new();
     private readonly HashSet<uint> knownDown=new();
     private int swallowedArrows;
@@ -28,9 +28,12 @@
         if(!down)heldKeys.Remove(key);
         else if(session&&!isAlt&&!isCtrl&&!isShift)heldKeys.Add(key);
         void FinishIfReleased(){
-            if(session&&!altHeld&&!ctrlHeld&&!shiftHeld&&heldKeys.Count==0){session=false;post(SwitchCommand.Commit);}
+            if(session&&!altHeld&&!ctrlHeld&&!shiftHeld&&heldKeys.Count==0){session=false;bool commit=!pinnedSession;pinnedSession=false;if(commit)post(SwitchCommand.Commit);}
         }
-        if(key==0x1B&&down&&session){session=false;heldKeys.Clear();swallowedEscape=true;post(SwitchCommand.Cancel);return true;}
+        // Ctrl can arrive just after the first Alt+Tab. Promote without moving
+        // the selection, and never downgrade this gesture when Ctrl is released.
+        if(session&&isCtrl&&down&&altHeld&&!pinnedSession){pinnedSession=true;post(SwitchCommand.Pin);}
+        if(key==0x1B&&down&&session){session=pinnedSession=false;heldKeys.Clear();swallowedEscape=true;post(SwitchCommand.Cancel);return true;}
         if(key==0x1B&&!down&&swallowedEscape){swallowedEscape=false;return true;}
         if(key is >=0x25 and <=0x28) {
             int bit=1<<((int)key-0x25);
@@ -39,16 +42,17 @@
         }
         if(key==0x7B&&ctrl&&alt&&down){if(!swallowedPause)post(SwitchCommand.Pause);swallowedPause=true;return true;}
         if(key==0x7B&&!down&&swallowedPause){swallowedPause=false;return true;}
-        if(key==0x20&&ctrl&&alt&&down){if(!swallowedSpace){session=false;heldKeys.Clear();post(SwitchCommand.Persistent);}swallowedSpace=true;return true;}
+        if(key==0x20&&ctrl&&alt&&down){if(!swallowedSpace){session=pinnedSession=false;heldKeys.Clear();post(SwitchCommand.Persistent);}swallowedSpace=true;return true;}
         if(key==0x20&&!down&&swallowedSpace){swallowedSpace=false;return true;}
         if(enabled&&(alt||session)&&down&&key==9){
-            if(!session){heldKeys.Clear();heldKeys.UnionWith(knownDown);}
+            if(!session){pinnedSession=false;heldKeys.Clear();heldKeys.UnionWith(knownDown);}
             session=true;heldKeys.Add(key);swallowedTab=true;
-            post(ctrl?(shift?SwitchCommand.PinnedPrevious:SwitchCommand.PinnedNext):(shift?SwitchCommand.Previous:SwitchCommand.Next));
+            pinnedSession|=ctrl;
+            post(pinnedSession?(shift?SwitchCommand.PinnedPrevious:SwitchCommand.PinnedNext):(shift?SwitchCommand.Previous:SwitchCommand.Next));
             return true;
         }
         if(key==9&&!down&&swallowedTab){swallowedTab=false;FinishIfReleased();return true;}
-        if(enabled&&alt&&!ctrl&&down&&key==0xC0){if(!session){heldKeys.Clear();heldKeys.UnionWith(knownDown);}session=true;heldKeys.Add(key);swallowedGrave=true;post(shift?SwitchCommand.SameAppPrevious:SwitchCommand.SameApp);return true;}
+        if(enabled&&alt&&!ctrl&&down&&key==0xC0){if(!session){pinnedSession=false;heldKeys.Clear();heldKeys.UnionWith(knownDown);}session=true;heldKeys.Add(key);swallowedGrave=true;post(shift?SwitchCommand.SameAppPrevious:SwitchCommand.SameApp);return true;}
         if(key==0xC0&&!down&&swallowedGrave){swallowedGrave=false;FinishIfReleased();return true;}
         if(!down)FinishIfReleased();
         return false;
@@ -67,8 +71,9 @@
     private uint threadId;
     private Exception? failure;
     private long eventCount, tabCount;
-    private bool lastTabAlt, lastTabCtrl;
-    public object Statistics => new {present=handle!=0,foregroundPresent=foregroundHook!=0,alive=thread.IsAlive,events=Interlocked.Read(ref eventCount),tabs=Interlocked.Read(ref tabCount),lastTabAlt,lastTabCtrl};
+    private bool lastTabAlt, lastTabCtrl, lastTabAltFlag,lastTabAltPhysical;
+    public object Statistics => new {present=handle!=0,foregroundPresent=foregroundHook!=0,alive=thread.IsAlive,events=Interlocked.Read(ref eventCount),tabs=Interlocked.Read(ref tabCount),lastTabAlt,lastTabCtrl,lastTabAltFlag,lastTabAltPhysical};
+    internal static bool AltSnapshot(uint key,uint flags,bool physicalAlt)=> (flags&0x20)!=0 || (key is not (0x12 or 0xA4 or 0xA5)&&physicalAlt);
     public volatile bool Enabled = true;
     public KeyboardHook(Action<SwitchCommand> post,Action<nint,uint>? foreground=null) {
         router = new ShortcutRouter(post); callback = Callback;
@@ -99,11 +104,12 @@
             bool down = (uint)wp is 0x100 or 0x104;
             bool up = (uint)wp is 0x101 or 0x105;
             if (!down && !up) return Native.CallNextHookEx(handle, code, wp, lp);
-            // LLKHF_ALTDOWN belongs to this event. GetAsyncKeyState is used only
-            // for OTHER keys, whose earlier transitions have already completed.
-            bool alt = (k.Flags & 0x20) != 0;
+            // The event context is supplemented by the OTHER key's fresh state.
+            // For Alt's own transitions, Route uses down/up plus the other side.
+            bool physicalAlt=Native.GetAsyncKeyState(0x12)<0;
+            bool alt = AltSnapshot(k.Key,k.Flags,physicalAlt);
             bool ctrl = Native.GetAsyncKeyState(0x11)<0, shift = Native.GetAsyncKeyState(0x10)<0;
-            if(k.Key==9&&down){Interlocked.Increment(ref tabCount);lastTabAlt=alt;lastTabCtrl=ctrl;}
+            if(k.Key==9&&down){Interlocked.Increment(ref tabCount);lastTabAlt=alt;lastTabCtrl=ctrl;lastTabAltFlag=(k.Flags&0x20)!=0;lastTabAltPhysical=physicalAlt;}
             bool otherAltDown=k.Key==0xA4?Native.GetAsyncKeyState(0xA5)<0:k.Key==0xA5&&Native.GetAsyncKeyState(0xA4)<0;
             bool otherCtrlDown=k.Key==0xA2?Native.GetAsyncKeyState(0xA3)<0:k.Key==0xA3&&Native.GetAsyncKeyState(0xA2)<0;
             bool otherShiftDown=k.Key==0xA0?Native.GetAsyncKeyState(0xA1)<0:k.Key==0xA1&&Native.GetAsyncKeyState(0xA0)<0;
~~~~~

### SwitcherForm.cs

~~~~~diff
--- 1.1.6/SwitcherForm.cs
+++ 1.1.7/SwitcherForm.cs
@@ -216,6 +216,7 @@
     }
     private void HideTargetVisuals(){visualTimer.Stop();aura.Hide();dimmer.Hide();peek.Restore();}
     public void NavigationReleased(long timestamp=0) { releaseTimestamp=persistent?0:timestamp; if (Visible && !persistent && !configuring && !committing) Choose(); }
+    public void PinSelection(){if(Visible&&!configuring&&!committing){persistent=true;releaseTimestamp=0;Invalidate();}}
     public void TogglePersistent() { if (Visible) Dismiss(true); else OpenSwitcher(true, 0); }
     public void Dismiss(bool restore) {
         bool wasCommitting=committing;
@@ -285,7 +286,7 @@
         if (key == Keys.Back && search.TextLength==0 && !ctrl) { if(group!=null)GoBack(); return true; }
         if (key == Keys.Home && search.TextLength==0) { selected=0;EnsurePage();UpdateAura();Invalidate();return true; }
         if (key == Keys.End && search.TextLength==0) { selected=Math.Max(0,entries.Count-1);EnsurePage();UpdateAura();Invalidate();return true; }
-        if (key == Keys.Tab) { MoveSelection(shift ? -1 : 1); return true; }
+        if (key == Keys.Tab) { if(ctrl&&(keyData&Keys.Alt)!=0)PinSelection();MoveSelection(shift ? -1 : 1); return true; }
         if(key is Keys.Up or Keys.Down or Keys.Left or Keys.Right) {
             if((key is Keys.Left or Keys.Right)&&search.Focused&&search.TextLength>0){Invalidate();return base.ProcessCmdKey(ref msg,keyData);}
             NavigateArrow(key);return true;
~~~~~

### SettingsForm.cs

~~~~~diff
--- 1.1.6/SettingsForm.cs
+++ 1.1.7/SettingsForm.cs
@@ -14,7 +14,7 @@
     private readonly FlowCheck peek=new(){Text="선택한 창을 앞으로 미리 표시",Description="창의 실제 내용을 확인하며 전환할 수 있습니다."};
     private readonly FlowSelect mode=new(),size=new();
     private readonly FlowAppList apps=new();
-    private readonly TextBox excluded=new(){Multiline=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
+    private readonly TextBox excluded=new(){Multiline=true,AcceptsReturn=true,AcceptsTab=false,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
     private readonly TextBox manual=new(){PlaceholderText="프로세스 이름 · 예: chrome.exe",BorderStyle=BorderStyle.None};
     private readonly Panel sample=new();
     private readonly Label registered=new(){AutoSize=false};
@@ -26,10 +26,19 @@
     private readonly Label footer=new(){Text="변경 사항은 저장 후 적용됩니다.",AutoSize=false};
     private ThemePalette palette;
     private int currentPage;
+    private readonly CancellationTokenSource iconLoading=new();
+    private bool iconsStarted,iconsDisposed;
     internal bool PreviewRendering {get;set;}
     internal bool PreviewOnly {get;set;}
     protected override bool ShowWithoutActivation=>PreviewRendering||base.ShowWithoutActivation;
-    private sealed record AppOption(string Process,string Name){public override string ToString()=>Name.Equals(Process,StringComparison.OrdinalIgnoreCase)?Name:$"{Name}  ·  {Process}";}
+    // Reference identity stays stable when a background icon arrives. A mutable
+    // record hash would lose checked entries from FlowAppList's HashSet.
+    private sealed class AppOption(string process,string name,WindowInfo? window=null):IAppListItem{
+        public string Process=>process;public string Name=>name;public WindowInfo? Window=>window;
+        public string AppName=>Name;public string ProcessName=>Process;
+        public Image? AppIcon{get;set;}=window==null?null:WindowCatalog.CachedAppIcon(window);
+        public override string ToString()=>Name.Equals(Process,StringComparison.OrdinalIgnoreCase)?Name:$"{Name}  ·  {Process}";
+    }
     private Font MakeFont(float size,bool bold=false){var f=new Font("맑은 고딕",size,bold?FontStyle.Bold:FontStyle.Regular);ownedFonts.Add(f);return f;}
     public SettingsForm(Preferences prefs,IEnumerable<WindowInfo> windows){
         this.prefs=prefs;palette=ThemePalette.Resolve(prefs);
@@ -49,7 +58,7 @@
         var behaviors=Card(general,0,184,598,360);var checks=new[]{replace,grouped,preview,primary,monitor,startup};for(int i=0;i<checks.Length;i++)Place(behaviors,checks[i],8,6+i*58,582,58);
         LabelAt(general,"Alt+Tab → 방향키로 이동 → 키를 모두 놓으면 전환\nCtrl+Alt+Space로 열면 키를 놓아도 선택 화면을 유지합니다.",4,558,590,46,9,false,true);
         var ordering=pages[1];PageHeading(ordering,"상단 정렬","등록한 앱을 먼저, 각 그룹 안에서는 A~Z → 가나다순으로 표시합니다.");
-        foreach(var w in windows.GroupBy(x=>x.ProcessName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))choices.Add(new(w.ProcessName,w.AppName));
+        foreach(var w in windows.GroupBy(x=>x.ProcessName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))choices.Add(new(w.ProcessName,w.AppName,w));
         foreach(string process in prefs.Pinned)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase)))choices.Add(new(process,WindowCatalog.AppName(process)));
         var listCard=Card(ordering,0,74,598,342);Place(listCard,apps,10,12,578,318);FillChoices(prefs.Pinned);
         var entry=Card(ordering,0,430,598,78);Place(entry,manual,16,27,438,26);manual.AccessibleName="상단 정렬에 등록할 프로세스 이름";
@@ -76,18 +85,37 @@
     private FlowCard Card(Control host,int x,int y,int w,int h){var c=new FlowCard();Place(host,c,x,y,w,h);return c;}
     private void SelectPage(int index){currentPage=index;for(int i=0;i<pages.Count;i++){pages[i].Visible=i==index;navigation[i].Selected=i==index;navigation[i].Invalidate();}pages[index].BringToFront();}
     protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData is (Keys.Control|Keys.Tab) or (Keys.Control|Keys.Shift|Keys.Tab)){SelectPage((currentPage+((keyData&Keys.Shift)!=0?3:1))%4);navigation[currentPage].Focus();return true;}return base.ProcessCmdKey(ref msg,keyData);}
-    private void ApplyColors(){var p=palette;BackColor=p.Bg;ForeColor=p.Ink;void Style(Control host){foreach(Control c in host.Controls){c.ForeColor=Equals(c.Tag,"muted")?p.Muted:p.Ink;c.BackColor=host is FlowCard?p.Surface:p.Bg;if(c is FlowCard card){card.Palette=p;c.BackColor=p.Surface;}if(c is FlowButton button)button.Palette=p;if(c is FlowCheck check)check.Palette=p;if(c is FlowAppList list){list.Palette=p;list.BackColor=p.Surface;list.ItemHeight=(int)(40*DeviceDpi/96f);}Style(c);c.Invalidate();}}Style(this);foreach(var button in swatches)button.Selected=button.Swatch==p;sample.Invalidate();UiPaint.TitleBar(this,p);Invalidate();}
+    private void ApplyColors(){var p=palette;BackColor=p.Bg;ForeColor=p.Ink;void Style(Control host){foreach(Control c in host.Controls){c.ForeColor=Equals(c.Tag,"muted")?p.Muted:p.Ink;c.BackColor=host is FlowCard?p.Surface:p.Bg;if(c is FlowCard card){card.Palette=p;c.BackColor=p.Surface;}if(c is FlowButton button)button.Palette=p;if(c is FlowCheck check)check.Palette=p;if(c is FlowAppList list){list.Palette=p;list.BackColor=p.Surface;list.ItemHeight=(int)(48*DeviceDpi/96f);}Style(c);c.Invalidate();}}Style(this);foreach(var button in swatches)button.Selected=button.Swatch==p;sample.Invalidate();UiPaint.TitleBar(this,p);Invalidate();}
     protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UiPaint.TitleBar(this,palette);}
     protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=DeviceDpi/96f;using var pen=new Pen(UiPaint.Border(palette));e.Graphics.DrawLine(pen,24*s,ClientSize.Height-76*s,ClientSize.Width-24*s,ClientSize.Height-76*s);}
-    private void PaintSample(Graphics g){var p=palette;int D(float n)=>(int)Math.Round(n*DeviceDpi/96f);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(p.Bg);UiPaint.Box(g,new Rectangle(1,1,sample.Width-3,sample.Height-3),p.Surface,D(12),UiPaint.Border(p));UiPaint.Text(g,"미리보기  ·  "+p.Name,Font,new Rectangle(D(18),D(12),sample.Width-D(36),D(24)),p.Muted);var r=new Rectangle(D(14),D(46),sample.Width-D(28),D(54));UiPaint.Box(g,r,p.Selected,D(9),p.Accent);using(var b=new SolidBrush(p.Accent))g.FillEllipse(b,D(30),D(63),D(20),D(20));UiPaint.Text(g,"Chrome",Font,new Rectangle(D(64),D(51),D(150),D(24)),p.Ink);UiPaint.Text(g,"작업 중인 창 제목 · 선택한 창으로 전환",Font,new Rectangle(D(64),D(76),sample.Width-D(144),D(22)),p.Muted);UiPaint.Check(g,new Rectangle(sample.Width-D(48),D(63),D(20),D(20)),p.Accent);}
+    private void PaintSample(Graphics g){var p=palette;int D(float n)=>(int)Math.Round(n*DeviceDpi/96f);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(p.Bg);UiPaint.Box(g,new Rectangle(1,1,sample.Width-3,sample.Height-3),p.Surface,D(12),UiPaint.Border(p));UiPaint.Text(g,"미리보기  ·  "+p.Name,Font,new Rectangle(D(18),D(12),sample.Width-D(36),D(24)),p.Muted);var r=new Rectangle(D(14),D(46),sample.Width-D(28),D(54));UiPaint.Box(g,r,p.Selected,D(9),p.Accent);AppIconDrawing.Paint(g,new Rectangle(D(26),D(59),D(28),D(28)),choices.FirstOrDefault(x=>x.Process.Equals("chrome",StringComparison.OrdinalIgnoreCase))?.AppIcon,"Chrome",Font,p);UiPaint.Text(g,"Chrome",Font,new Rectangle(D(64),D(51),D(150),D(24)),p.Ink);UiPaint.Text(g,"작업 중인 창 제목 · 선택한 창으로 전환",Font,new Rectangle(D(64),D(76),sample.Width-D(144),D(22)),p.Muted);UiPaint.Check(g,new Rectangle(sample.Width-D(48),D(63),D(20),D(20)),p.Accent);}
     private void FillChoices(IEnumerable<string> selected){var set=selected.ToHashSet(StringComparer.OrdinalIgnoreCase);apps.BeginUpdate();apps.Items.Clear();apps.ResetChecks();foreach(var option in choices.OrderBy(x=>x.Name,AppNameComparer.Instance)){apps.Items.Add(option);apps.SetChecked(option,set.Contains(option.Process));}apps.EndUpdate();}
-    private void AddManual(){var names=Preferences.NormalizeApps(manual.Text.Split(new[]{',','\r','\n'},StringSplitOptions.RemoveEmptyEntries));var selected=apps.CheckedItems.Cast<AppOption>().Select(x=>x.Process).Concat(names).ToList();foreach(string process in names)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase)))choices.Add(new(process,WindowCatalog.AppName(process)));FillChoices(selected);manual.Clear();UpdateCount();}
+    private void AddManual(){var names=Preferences.NormalizeApps(manual.Text.Split(new[]{',','\r','\n'},StringSplitOptions.RemoveEmptyEntries));var selected=apps.CheckedItems.Cast<AppOption>().Select(x=>x.Process).Concat(names).ToList();var added=new List<AppOption>();foreach(string process in names)if(!choices.Any(x=>x.Process.Equals(process,StringComparison.OrdinalIgnoreCase))){var item=new AppOption(process,WindowCatalog.AppName(process));choices.Add(item);added.Add(item);}FillChoices(selected);manual.Clear();UpdateCount();if(!PreviewRendering)_=LoadIcons(added.ToArray());}
     private void UpdateCount()=>registered.Text=$"상단 등록 {apps.CheckedItems.Count()}개 · 등록 순서와 무관하게 이름순으로 정렬합니다.";
-    protected override void OnShown(EventArgs e){base.OnShown(e);if(PreviewRendering)return;var work=Owner==null?Screen.FromControl(this).WorkingArea:Screen.FromControl(Owner).WorkingArea;Height=Math.Min(Height,work.Height-24);Location=new Point(Math.Clamp((Owner?.Left??work.Left)+((Owner?.Width??work.Width)-Width)/2,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(work.Top+(work.Height-Height)/2,work.Top,Math.Max(work.Top,work.Bottom-Height)));}
+    protected override void OnShown(EventArgs e){base.OnShown(e);if(PreviewRendering)return;if(!iconsStarted){iconsStarted=true;_=LoadIcons(choices.ToArray());}var work=Owner==null?Screen.FromControl(this).WorkingArea:Screen.FromControl(Owner).WorkingArea;Height=Math.Min(Height,work.Height-24);Location=new Point(Math.Clamp((Owner?.Left??work.Left)+((Owner?.Width??work.Width)-Width)/2,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(work.Top+(work.Height-Height)/2,work.Top,Math.Max(work.Top,work.Bottom-Height)));}
+    private async Task LoadIcons(AppOption[] items){
+        var token=iconLoading.Token;
+        foreach(var item in items){if(token.IsCancellationRequested||IsDisposed)return;if(item.AppIcon!=null)continue;
+            Image? icon=null;try{icon=await Task.Run(()=>{
+                if(token.IsCancellationRequested)return null;
+                var window=item.Window;
+                if(window==null){string? path=RegisteredAppPath(item.Process);if(path==null)return null;window=new WindowInfo(0,item.Name,item.Process,item.Name,path,0,false);}
+                return WindowCatalog.GetIcon(window);
+            },token);}catch(OperationCanceledException){return;}catch{ }
+            if(token.IsCancellationRequested||IsDisposed)return;if(icon!=null){item.AppIcon=icon;apps.Invalidate();sample.Invalidate();}
+        }
+    }
+    internal static string? RegisteredAppPath(string process){
+        foreach(var hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine}){
+            try{using var key=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+process+".exe");if(key?.GetValue("") is string value){string path=Environment.ExpandEnvironmentVariables(value).Trim('"');if(!path.StartsWith(@"\\")&&File.Exists(path))return path;}}catch{ }
+        }
+        return null;
+    }
     private void Save(){try{if(!PreviewOnly)Preferences.StartupEnabled=startup.Checked;ApplyValues();DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,"설정을 저장하지 못했습니다.\n"+ex.Message,"AltTabFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
     private void ApplyValues(){prefs.StartWithWindows=startup.Checked;prefs.View=mode.SelectedItem?.ToString()??"목록";prefs.UiScale=int.Parse((size.SelectedItem?.ToString()??"100%").TrimEnd('%'));prefs.ReplaceAltTab=replace.Checked;prefs.GroupApps=grouped.Checked;prefs.ShowPreview=preview.Checked;prefs.CurrentMonitorOnly=monitor.Checked;prefs.OnPrimaryMonitor=primary.Checked;prefs.ColorPreset=palette.Id;prefs.LightTheme=palette.Light;prefs.HighlightTarget=highlight.Checked;prefs.EmphasizeTarget=emphasis.Checked;prefs.PeekTarget=peek.Checked;prefs.Excluded=Preferences.NormalizeApps(excluded.Text.Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries));prefs.Pinned=apps.CheckedItems.Cast<AppOption>().OrderBy(x=>x.Name,AppNameComparer.Instance).Select(x=>x.Process).ToList();}
     internal void TestPage(int page,ThemePalette? p=null){if(p!=null){palette=p;ApplyColors();}SelectPage(page);}
     internal void TestApplyValues()=>ApplyValues();
+    internal Task TestLoadIcons()=>LoadIcons(choices.ToArray());
     internal Panel TestCurrentPage=>pages[currentPage];
-    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing){foreach(var font in ownedFonts)font.Dispose();ownedFonts.Clear();}}
+    protected override void Dispose(bool disposing){if(disposing&&!iconsDisposed){iconsDisposed=true;iconLoading.Cancel();iconLoading.Dispose();}base.Dispose(disposing);if(disposing){foreach(var font in ownedFonts)font.Dispose();ownedFonts.Clear();}}
 }
~~~~~

### UiControls.cs

~~~~~diff
--- 1.1.6/UiControls.cs
+++ 1.1.7/UiControls.cs
@@ -96,7 +96,8 @@
     public ThemePalette Palette {get;set;}=ThemePalette.All[0];private readonly HashSet<object> checks=new();
     public event EventHandler? ChecksChanged;
     public IEnumerable<object> CheckedItems=>Items.Cast<object>().Where(checks.Contains);
-    public FlowAppList(){DrawMode=DrawMode.OwnerDrawFixed;BorderStyle=BorderStyle.None;IntegralHeight=false;ItemHeight=40;AccessibleName="상단 정렬 앱 · Space로 등록 또는 해제";}
+    private Font? processFont;
+    public FlowAppList(){DrawMode=DrawMode.OwnerDrawFixed;BorderStyle=BorderStyle.None;IntegralHeight=false;ItemHeight=48;AccessibleName="상단 정렬 앱 · Space로 등록 또는 해제";}
     public void SetChecked(object item,bool value){if(value)checks.Add(item);else checks.Remove(item);Invalidate();ChecksChanged?.Invoke(this,EventArgs.Empty);}
     public void ResetChecks()=>checks.Clear();
     internal void TestKey(Keys key)=>OnKeyDown(new KeyEventArgs(key));
@@ -113,5 +114,25 @@
     }
     protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);int i=IndexFromPoint(e.Location);if(e.Button==MouseButtons.Left&&i>=0)SetChecked(Items[i],!checks.Contains(Items[i]));}
     protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space&&SelectedIndex>=0){SetChecked(Items[SelectedIndex],!checks.Contains(Items[SelectedIndex]));e.Handled=e.SuppressKeyPress=true;}base.OnKeyDown(e);}
-    protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;var p=Palette;var g=e.Graphics;int D(float x)=>(int)Math.Round(x*DeviceDpi/96f);bool active=(e.State&DrawItemState.Selected)!=0,on=checks.Contains(Items[e.Index]);using(var b=new SolidBrush(active?p.Selected:p.Surface))g.FillRectangle(b,e.Bounds);g.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(e.Bounds.X+D(10),e.Bounds.Y+(e.Bounds.Height-D(18))/2,D(18),D(18));UiPaint.Box(g,r,on?p.Accent:p.Raised,D(4),on?p.Accent:UiPaint.Border(p));if(on)UiPaint.Check(g,r,UiPaint.OnAccent(p));UiPaint.Text(g,Items[e.Index].ToString()??"",Font,new Rectangle(e.Bounds.X+D(42),e.Bounds.Y,e.Bounds.Width-D(52),e.Bounds.Height),p.Ink,TextFormatFlags.VerticalCenter);if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(e.Bounds,-2,-2),p.Accent,p.Surface);}
+    protected override void OnDrawItem(DrawItemEventArgs e){
+        if(e.Index<0)return;var p=Palette;var g=e.Graphics;int D(float x)=>(int)Math.Round(x*DeviceDpi/96f);bool active=(e.State&DrawItemState.Selected)!=0,on=checks.Contains(Items[e.Index]);
+        using(var b=new SolidBrush(active?p.Selected:p.Surface))g.FillRectangle(b,e.Bounds);g.SmoothingMode=SmoothingMode.AntiAlias;
+        var r=new Rectangle(e.Bounds.X+D(10),e.Bounds.Y+(e.Bounds.Height-D(18))/2,D(18),D(18));UiPaint.Box(g,r,on?p.Accent:p.Raised,D(4),on?p.Accent:UiPaint.Border(p));if(on)UiPaint.Check(g,r,UiPaint.OnAccent(p));
+        var item=Items[e.Index] as IAppListItem;string title=item?.AppName??Items[e.Index].ToString()??"";
+        var icon=new Rectangle(e.Bounds.X+D(42),e.Bounds.Y+(e.Bounds.Height-D(28))/2,D(28),D(28));AppIconDrawing.Paint(g,icon,item?.AppIcon,title,Font,p);
+        var text=new Rectangle(e.Bounds.X+D(82),e.Bounds.Y+D(item==null?0:4),e.Bounds.Width-D(94),item==null?e.Bounds.Height:D(23));UiPaint.Text(g,title,Font,text,p.Ink,TextFormatFlags.VerticalCenter);
+        if(item!=null){processFont??=new Font(Font.FontFamily,8.5f);UiPaint.Text(g,item.ProcessName+".exe",processFont,new Rectangle(text.X,e.Bounds.Y+D(27),text.Width,D(18)),p.Muted);}
+        if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(e.Bounds,-2,-2),p.Accent,p.Surface);
+    }
+    protected override void Dispose(bool disposing){if(disposing)processFont?.Dispose();base.Dispose(disposing);}
 }
+internal interface IAppListItem {string AppName{get;}string ProcessName{get;}Image? AppIcon{get;set;}}
+internal static class AppIconDrawing
+{
+    public static void Paint(Graphics g,Rectangle bounds,Image? icon,string name,Font font,ThemePalette p){
+        if(icon!=null){g.DrawImage(icon,bounds);return;}
+        UiPaint.Box(g,bounds,p.Raised,Math.Max(4,bounds.Width/4));
+        string initial=string.IsNullOrWhiteSpace(name)?"?":System.Globalization.StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
+        UiPaint.Text(g,initial,font,bounds,p.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
+    }
+}
~~~~~

### UiTests.cs

~~~~~diff
--- 1.1.6/UiTests.cs
+++ 1.1.7/UiTests.cs
@@ -10,6 +10,19 @@
         string output=Path.Combine(Preferences.DataDirectory,"ui-previews");Directory.CreateDirectory(output);
         var windows=new List<WindowInfo>{new(101,"검토 중인 문서","chrome","Chrome","chrome.exe",0,false),new(102,"개발 작업","Code","Visual Studio Code","Code.exe",1,false),new(103,"일정 및 메시지","slack","Slack","slack.exe",2,false)};
         int dpi=96;
+        // Drive native TextBox message preprocessing, including the form's real
+        // AcceptButton. No SendInput, user settings or startup registration.
+        var editPrefs=new Preferences{StartWithWindows=false,Excluded=new(){"original_app"}};
+        using(var editForm=new SettingsForm(editPrefs,windows){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
+            editForm.Show();editForm.TestPage(3);var editor=Walk(editForm).OfType<TextBox>().Single(t=>t.Multiline);
+            editor.Text="chrome.exe";editor.SelectionStart=editor.TextLength;editor.Focus();
+            var enter=Message.Create(editor.Handle,0x100,(nint)13,0);bool consumed=editor.PreProcessMessage(ref enter);
+            if(!consumed&&!editor.IsDisposed)Native.SendMessageTimeout(editor.Handle,0x102,(nint)13,0,2,200,out _);
+            Check("hidden-app Enter inserts native newline",!editor.IsDisposed&&editor.Text=="chrome.exe\r\n");
+            Check("hidden-app Enter keeps dialog open without saving",editForm.Visible&&editForm.DialogResult==DialogResult.None&&editPrefs.Excluded.SequenceEqual(new[]{"original_app"}));
+            if(!editor.IsDisposed){editor.AppendText("EXCEL.exe");Walk(editForm).OfType<FlowButton>().Single(b=>b.Primary).PerformClick();}
+            Check("explicit save applies multiple hidden app lines",editPrefs.Excluded.SequenceEqual(new[]{"chrome","EXCEL"}));
+        }
         foreach(var p in ThemePalette.All){
             var prefs=new Preferences{ColorPreset=p.Id,LightTheme=p.Light,StartWithWindows=false,GroupApps=false,ShowPreview=true,UiScale=115,Pinned=new(){"chrome","missing_app"},Excluded=new(){"hidden_app"}};
             string original=JsonSerializer.Serialize(prefs);
@@ -36,6 +49,23 @@
         using(var selector=new FlowSelect()){
             selector.Items.AddRange(new object[]{"목록","카드","아이콘"});selector.SelectedIndex=0;selector.TestKey(Keys.Right);Check("view selector arrow selects next value",Equals(selector.SelectedItem,"카드"));selector.TestKey(Keys.Left);Check("view selector arrow selects previous value",Equals(selector.SelectedItem,"목록"));selector.TestKey(Keys.Left);Check("selector boundary stays valid",selector.SelectedIndex==0);
         }
+        var iconPrefs=new Preferences{StartWithWindows=false,Pinned=new(){"chrome"}};
+        using(var form=new SettingsForm(iconPrefs,windows){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)})using(var icon=new Bitmap(28,28)){
+            using(var g=Graphics.FromImage(icon))g.Clear(Color.Coral);
+            form.Show();form.TestPage(1);var list=Walk(form).OfType<FlowAppList>().Single();var item=list.Items.Cast<IAppListItem>().Single(x=>x.ProcessName=="chrome");item.AppIcon=icon;
+            Check("loading icon preserves checked app identity",list.CheckedItems.Contains(item));
+            using(var bitmap=new Bitmap(list.Width,list.Height)){list.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int D(float v)=>(int)Math.Round(v*list.DeviceDpi/96f);Check("app selection row draws actual supplied icon",bitmap.GetPixel(D(56),D(24)).ToArgb()==Color.Coral.ToArgb());}
+            form.TestApplyValues();Check("saving after icon load preserves pinned app",iconPrefs.Pinned.SequenceEqual(new[]{"chrome"}));
+            item.AppIcon=null;using(var bitmap=new Bitmap(40,40)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Magenta);AppIconDrawing.Paint(g,new Rectangle(2,2,28,28),null,"Chrome",form.Font,ThemePalette.All[0]);}Check("missing app icon draws identifying fallback badge",bitmap.GetPixel(5,5).ToArgb()!=Color.Magenta.ToArgb());}form.Close();
+        }
+        var ownIconWindow=new WindowInfo(0,"Icon fixture","icon_fixture","Icon fixture",Environment.ProcessPath!,0,false);
+        using(var form=new SettingsForm(new Preferences{StartWithWindows=false},new[]{ownIconWindow}){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
+            form.Show();var job=form.TestLoadIcons();var deadline=Environment.TickCount64+3000;while(!job.IsCompleted&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(1);}
+            var list=Walk(form).OfType<FlowAppList>().Single();Check("background icon extraction fills settings item",job.IsCompletedSuccessfully&&((IAppListItem)list.Items[0]).AppIcon!=null);form.Close();
+        }
+        using(var form=new SettingsForm(new Preferences{StartWithWindows=false},new[]{ownIconWindow with{ProcessName="icon_cancel",Path=Environment.ProcessPath!+".missing"}}){PreviewRendering=true,PreviewOnly=true,TopMost=false,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)}){
+            form.Show();var job=form.TestLoadIcons();form.Close();var deadline=Environment.TickCount64+3000;while(!job.IsCompleted&&Environment.TickCount64<deadline){Application.DoEvents();Thread.Sleep(1);}Check("closing settings safely cancels pending icon load",form.IsDisposed&&job.IsCompletedSuccessfully);
+        }
         using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ShowInTaskbar=false})using(var selector=new FlowSelect{Palette=ThemePalette.All[3],Width=272,Height=40}){
             form.Controls.Add(selector);form.CreateControl();selector.CreateControl();selector.Items.AddRange(new object[]{"목록","카드","아이콘"});selector.SelectedIndex=0;
             var menu=selector.TestMenu();Check("selector opens a themed native menu",menu.Visible&&menu.Items.Count==3&&menu.BackColor==ThemePalette.All[3].Surface);
~~~~~

### 사용법.txt

~~~~~diff
--- 1.1.6/사용법.txt
+++ 1.1.7/사용법.txt
@@ -1,4 +1,13 @@
-AltTabFlow 1.1.6 · Windows x64
+AltTabFlow 1.1.7 · Windows x64
+
+[1.1.7 입력·앱 식별 개선]
+숨길 앱 입력칸의 Enter는 줄바꿈입니다. 등록을 마치면 변경 사항 저장을 누르세요.
+Ctrl+Alt+Tab은 키를 모두 놓아도 닫히지 않는 고정 선택 화면을 엽니다.
+반복 Ctrl+Alt+Tab / Ctrl+Alt+Shift+Tab은 다음 / 이전 항목, 방향키는 이동, Enter는 펼치기·전환, Esc는 취소입니다.
+Alt+Tab 중 Ctrl을 늦게 누른 경우에도 현재 선택을 그대로 고정합니다.
+일반 Alt+Tab은 기존대로 키를 모두 놓으면 전환합니다.
+설정의 상단 정렬 목록에 앱 아이콘·앱 이름·실행 파일 이름을 함께 표시합니다.
+실행하지 않은 등록 앱은 Windows App Paths에서 파일을 찾으면 아이콘을 표시하고, 찾지 못하면 앱 이름의 첫 글자로 구분합니다.
 
 [1.1.6 화면 개선]
 설정은 왼쪽 메뉴에서 기본 동작 / 상단 정렬 / 색상·효과 / 숨길 앱을 선택합니다.
~~~~~
