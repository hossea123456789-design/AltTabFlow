# AltTabFlow 1.1.8 — 일반 Alt+Tab 종료 누락 수정

기준일: 2026-11-10(KST). 비교 기준: 1.1.7.

## 확인한 증상

일반 Alt+Tab인데 Ctrl+Alt+Tab처럼 화면이 남는 문제다. 실제 실행 중 상태는 `visible=true`, `persistent=false`, `lastTabCtrl=false`였다. 최근 명령에는 Next가 반복되고 Commit이 없었다. 이 관측은 Ctrl 오인식보다 키 해제 후 종료 명령 누락을 가리킨다. 당시 버전에는 입력 라우터의 남은 키 목록이 없어, 최초 사용자 증상을 일으킨 정확한 가상 키는 확정하지 않는다.

## 수정한 경로

1. **놓은 키의 과거 기록 정리.** 다른 창에서의 key-up을 놓치면 knownDown이 다음 Alt+Tab의 heldKeys로 넘어가 종료를 막았다. 현재 이벤트의 키와 가로챈 키를 제외한 기록은 Windows의 현재 눌림과 대조해 정리한다. 실제로 계속 누르는 키는 유지한다.
2. **마지막 해제 통지 누락 복구.** 입력 세션 중에만 훅 스레드의 16ms WM_TIMER로 상태를 확인한다. Alt·Ctrl·Shift와 나머지 키가 실제로 모두 해제됐을 때 일반 모드만 한 번 Commit한다. 정상 key-up은 즉시 처리하며 타이머를 기다리지 않는다. 세션이 끝나면 타이머를 해제한다.
3. **새 일반 Alt+Tab이 이전 고정 상태를 상속하지 않음.** 이미 화면이 열려 있어도 이번 명령의 pinned 값으로 모드를 정한다. 이전 Ctrl+Alt+Tab 화면 위에서 새 일반 Alt+Tab을 누르면 빠른 전환으로 돌아간다.

가로챈 Tab·방향키·같은 앱 전환 키는 Windows 비동기 값이 0일 수 있으므로 그 값을 해제 근거로 쓰지 않는다. 대응 key-up까지 기다린다. 시간이 지났다는 이유로 누른 키를 강제로 해제 처리하지 않는다.

## 최종 조작 규칙

| 입력 | 결과 |
| :--- | :--- |
| 일반 Alt+Tab → 키 전체 해제 | 하이라이트된 창으로 전환하고 닫힘 |
| 일반 Alt+Tab → 방향키 이동 → 키 전체 해제 | 현재 선택 창으로 전환하고 닫힘 |
| Chrome 묶음에서 키 전체 해제 | 표시된 대표 창으로 전환 |
| Ctrl+Alt+Tab → 키 전체 해제 | 고정 화면 유지 |
| 고정 화면이 남은 상태 → 새 일반 Alt+Tab | 빠른 전환으로 변경, 전체 해제 시 닫힘 |
| 고정 화면에서 Enter / Esc | 선택 창 전환 / 취소 |

이전 1.1.7의 숨길 앱 Enter 줄바꿈, 앱 아이콘, 설정은 유지한다.

## 검증과 범위

- 수정 전 같은 문제를 만드는 11개 검사가 실패했고 수정 후 통과했다. 과거 키 기록 4개, 진행 중 IME 처리 키 기록 1개, 세 보기의 고정 모드 상속과 종료 6개다.
- 기본 검사 45개를 추가했다. 실제 누른 다른 키 보호, 소비한 키 보호, Alt 해제 통지 누락, 고정 세션 보호, 늦은 key-up 중복 확정 방지를 포함한다.
- 기본 206개 + 실제 창 10개 + 메뉴 7개 + 전경 18개 + UI 71개가 통과했다. 전경 검사에 포함된 실제 창 10개 중복을 제외하면 **302개**다.
- 전체 MD의 소스·자원 29개를 복원한 별도 프로젝트도 재빌드·검증했다.
- 최종 빌드 적용 후 사용자가 일반 Alt+Tab 2~3회와 Ctrl+Alt+Tab을 직접 확인하고 **“둘 다 정상 동작함”**이라고 답했다. 일반 모드 종료와 Ctrl 조합 유지의 실사용 확인을 완료했다.
- 실제 자동 입력에서는 마지막 Alt-up이 전달되지 않은 상태를 관측했다. 관리자 권한 창과 입력 도구 사이의 영향 가능성이 있어 사용자 물리 키 증상과 원인이 같다고 단정하지 않는다. 실제 화면 검증 결과는 VERIFICATION.md와 verification-release.json에 별도로 기록한다.
- 진단에는 session, pinnedSession, 남은 가상 키, 정리 횟수, 해제 복구 횟수, 현재 Alt/Ctrl/Shift 눌림을 추가했다. 전체 타이핑을 별도 로그로 기록하지 않는다.
- 배포 전후 사용자 설정 파일 SHA-256 일치: True.

## 배포 파일

- ZIP: `AltTabFlow-v1.1.8-win-x64-20261110-181845.zip`
- ZIP SHA-256: `7BC446AC0BB43F26EE3E2354DBDC023F34AAC8027C2BBF07C327FFC43CA5650C`
- EXE SHA-256: `6ABD2FA306B0106F3D1ED6296A0E0276CD5BE039380CD761D9C0C856F0021F0C`
- 전체 명세: `AltTabFlow_재구현_완전명세.md`, 2.2·10.2·11.7장과 소스 부록 갱신.

참조: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate).

## 1.1.7 대비 실제 변경 소스

아래 6개 텍스트 파일의 변경을 담았다. 독립 복원은 전체 명세를 사용한다.

### AltTabFlow.csproj

~~~~~diff
--- 1.1.7/AltTabFlow.csproj
+++ 1.1.8/AltTabFlow.csproj
@@ -7,7 +7,7 @@
     <Nullable>enable</Nullable>
     <ApplicationManifest>app.manifest</ApplicationManifest>
     <AssemblyName>AltTabFlow</AssemblyName>
-    <Version>1.1.7</Version>
+    <Version>1.1.8</Version>
     <Company>Local Tools</Company>
     <Product>AltTabFlow</Product>
     <Description>앱별로 정리하는 창 전환기</Description>
~~~~~

### Program.cs

~~~~~diff
--- 1.1.7/Program.cs
+++ 1.1.8/Program.cs
@@ -314,6 +314,78 @@
         Check("Tab 다음 늦은 Ctrl-down은 이동 없이 Pin 명령",promoted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Pin}));
         promotedRouter.Route(0xA2,false,true,true,false,true);promotedRouter.Route(9,true,true,false,false,true);promotedRouter.Route(9,false,true,false,false,true);promotedRouter.Route(0xA4,false,false,false,false,true);
         Check("Ctrl 먼저 해제 후 Tab 반복도 고정 유지하며 확정하지 않음",promoted.Last()==SwitchCommand.PinnedNext&&!promoted.Contains(SwitchCommand.Commit));
+        // A hook may miss an unrelated key-up across an IME/desktop/hook handoff.
+        // Historical presses must not become permanent blockers for a new gesture.
+        foreach(uint staleKey in new uint[]{0x41,0x5B,0xE5,0x7B}){
+            var emitted=new List<SwitchCommand>();var physical=new HashSet<uint>();
+            var reconciled=new ShortcutRouter(emitted.Add,physical.Contains);
+            physical.Add(staleKey);reconciled.Route(staleKey,true,false,false,false,true);
+            physical.Clear(); // release occurred but its callback was not delivered
+            reconciled.Route(9,true,true,false,false,true);reconciled.Route(9,false,true,false,false,true);
+            reconciled.Route(0xA4,false,false,false,false,true);
+            Check($"과거 키 0x{staleKey:X2} 해제 누락이 일반 AltTab 종료를 막지 않음",emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
+        }
+        {
+            var emitted=new List<SwitchCommand>();var physical=new HashSet<uint>();
+            var reconciled=new ShortcutRouter(emitted.Add,physical.Contains);
+            reconciled.Route(9,true,true,false,false,true);reconciled.Route(9,false,true,false,false,true);
+            physical.Add(0xE5);reconciled.Route(0xE5,true,true,false,false,true);physical.Clear();
+            reconciled.Route(0xA4,false,false,false,false,true);
+            Check("전환 도중 놓친 IME 키 해제도 마지막 Alt-up에서 정리",emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
+        }
+        {
+            var emitted=new List<SwitchCommand>();var physical=new HashSet<uint>{0x41};
+            var reconciled=new ShortcutRouter(emitted.Add,physical.Contains);
+            reconciled.Route(0x41,true,false,false,false,true);reconciled.Route(9,true,true,false,false,true);reconciled.Route(9,false,true,false,false,true);
+            reconciled.Route(0xA4,false,false,false,false,true);
+            Check("실제로 계속 누른 다른 키는 성급하게 해제로 처리하지 않음",!emitted.Contains(SwitchCommand.Commit));
+            physical.Clear();reconciled.Route(0x41,false,false,false,false,true);
+            Check("실제 다른 키를 마지막에 놓으면 한 번 확정",emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
+        }
+        foreach(uint swallowed in new uint[]{9,0x25,0x26,0x27,0x28,0xC0}){
+            var emitted=new List<SwitchCommand>();var reconciled=new ShortcutRouter(emitted.Add,_=>false);
+            reconciled.Route(9,true,true,false,false,true);reconciled.Route(9,false,true,false,false,true);
+            reconciled.Route(swallowed,true,true,false,false,true);reconciled.Route(0xA4,false,false,false,false,true);
+            Check($"가로챈 키 0x{swallowed:X2}의 비동기 0을 해제로 오인하지 않음",!emitted.Contains(SwitchCommand.Commit));
+            reconciled.Route(swallowed,false,false,false,false,true);
+            Check($"가로챈 키 0x{swallowed:X2}는 실제 key-up으로 확정",emitted.Count(c=>c==SwitchCommand.Commit)==1);
+        }
+        {
+            var emitted=new List<SwitchCommand>();var recovered=new ShortcutRouter(emitted.Add,_=>false);
+            recovered.Route(9,true,true,false,false,true);recovered.Route(9,false,true,false,false,true);
+            recovered.ReconcileReleased(true,false,false);
+            Check("해제 복구는 실제 Alt를 누르고 있으면 기다림",!emitted.Contains(SwitchCommand.Commit));
+            recovered.ReconcileReleased(false,true,false);recovered.ReconcileReleased(false,false,true);
+            Check("해제 복구는 실제 Ctrl 또는 Shift를 누르고 있어도 기다림",!emitted.Contains(SwitchCommand.Commit));
+            recovered.ReconcileReleased(false,false,false);
+            Check("마지막 Alt-up 통지가 누락돼도 실제 전체 해제 후 확정",emitted.SequenceEqual(new[]{SwitchCommand.Next,SwitchCommand.Commit}));
+            recovered.ReconcileReleased(false,false,false);recovered.Route(0xA4,false,false,false,false,true);
+            Check("복구 뒤 늦은 key-up이나 타이머가 중복 확정하지 않음",emitted.Count(c=>c==SwitchCommand.Commit)==1);
+        }
+        {
+            var emitted=new List<SwitchCommand>();var recovered=new ShortcutRouter(emitted.Add,_=>false);
+            recovered.Route(9,true,true,true,false,true);recovered.Route(9,false,true,true,false,true);
+            recovered.ReconcileReleased(false,false,false);
+            Check("고정 모드 해제 통지가 누락돼도 복구는 화면 확정 명령을 보내지 않음",emitted.SequenceEqual(new[]{SwitchCommand.PinnedNext})&&!recovered.IsActive);
+            recovered.Route(9,true,true,false,false,true);recovered.Route(9,false,true,false,false,true);recovered.ReconcileReleased(false,false,false);
+            Check("고정 해제 복구 뒤 새 일반 AltTab은 정상 확정",emitted.SequenceEqual(new[]{SwitchCommand.PinnedNext,SwitchCommand.Next,SwitchCommand.Commit}));
+        }
+        foreach(uint swallowed in new uint[]{9,0x25,0x26,0x27,0x28,0xC0}){
+            var emitted=new List<SwitchCommand>();var recovered=new ShortcutRouter(emitted.Add,_=>false);
+            recovered.Route(9,true,true,false,false,true);recovered.Route(9,false,true,false,false,true);
+            recovered.Route(swallowed,true,true,false,false,true);recovered.ReconcileReleased(false,false,false);
+            Check($"해제 복구도 소비한 키 0x{swallowed:X2}의 key-up을 기다림",!emitted.Contains(SwitchCommand.Commit));
+            recovered.Route(swallowed,false,false,false,false,true);
+            Check($"해제 복구 중 키 0x{swallowed:X2}를 놓으면 한 번 확정",emitted.Count(c=>c==SwitchCommand.Commit)==1);
+        }
+        {
+            var emitted=new List<SwitchCommand>();var physical=new HashSet<uint>();var recovered=new ShortcutRouter(emitted.Add,physical.Contains);
+            recovered.Route(9,true,true,false,false,true);recovered.Route(9,false,true,false,false,true);
+            physical.Add(0x41);recovered.Route(0x41,true,true,false,false,true);recovered.ReconcileReleased(false,false,false);
+            Check("해제 복구에서도 실제 일반 키를 누르면 대기",!emitted.Contains(SwitchCommand.Commit));
+            physical.Clear();recovered.ReconcileReleased(false,false,false);
+            Check("일반 키와 Alt 해제 모두 통지 누락이어도 실제 상태로 복구",emitted.Count(c=>c==SwitchCommand.Commit)==1);
+        }
         foreach(var viewport in new[]{new Size(4048,2048),new Size(1872,976),new Size(1318,704)}){
             foreach(int count in new[]{17,22}){
                 var layout=LayoutPlanner.Plan(count,viewport.Width,viewport.Height,false);
@@ -384,6 +456,11 @@
                     form.TestKey(Keys.Escape);Check(mode+" 고정 화면 Esc는 전환 없이 닫힘",!form.Visible&&activated==0);
                     form.PrepareNavigationTest(navigationRows);form.Show();pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);pinnedFormRouter.Route(0xA4,false,false,false,false,true);
                     Check(mode+" 고정 화면 취소 뒤 일반 AltTab은 키 해제로 전환",!form.Visible&&activated==200);
+                    activated=0;form.PrepareNavigationTest(navigationRows,true);form.Show();
+                    pinnedFormRouter.Route(9,true,true,false,false,true);pinnedFormRouter.Route(9,false,true,false,false,true);
+                    Check(mode+" 고정 화면이 남아 있어도 새 일반 AltTab은 빠른 전환 모드",form.Visible&&!form.IsPersistent);
+                    pinnedFormRouter.Route(0xA4,false,false,false,false,true);
+                    Check(mode+" 남아 있던 고정 화면도 일반 AltTab 해제에 선택 전환 후 닫힘",!form.Visible&&activated==200);
                 } finally {form.Shutdown();}
             }
         }
~~~~~

### Native.cs

~~~~~diff
--- 1.1.7/Native.cs
+++ 1.1.8/Native.cs
@@ -41,6 +41,8 @@
     [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from,uint to,bool attach);
     [DllImport("user32.dll")] public static extern bool IsHungAppWindow(nint hwnd);
     [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
+    [DllImport("user32.dll")] public static extern nuint SetTimer(nint hwnd,nuint id,uint milliseconds,nint callback);
+    [DllImport("user32.dll")] public static extern bool KillTimer(nint hwnd,nuint id);
     [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int w,int h,uint flags);
     [DllImport("user32.dll")] public static extern nint WindowFromPoint(Point point);
     [StructLayout(LayoutKind.Sequential,Pack=1)] public struct BlendFunction {public byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat;}
~~~~~

### KeyboardHook.cs

~~~~~diff
--- 1.1.7/KeyboardHook.cs
+++ 1.1.8/KeyboardHook.cs
@@ -10,11 +10,29 @@
 internal sealed class ShortcutRouter
 {
     private readonly Action<SwitchCommand> post;
+    private readonly Func<uint,bool>? keyIsDown;
     private bool session, pinnedSession, swallowedTab, swallowedGrave, swallowedSpace, swallowedPause, swallowedEscape;
     private readonly HashSet<uint> heldKeys=new();
     private readonly HashSet<uint> knownDown=new();
     private int swallowedArrows;
-    public ShortcutRouter(Action<SwitchCommand> post) { this.post=post; }
+    private long reconciledKeys;
+    private long recoveredReleases;
+    public ShortcutRouter(Action<SwitchCommand> post,Func<uint,bool>? keyIsDown=null) { this.post=post;this.keyIsDown=keyIsDown; }
+    public bool IsActive=>session;
+    public object DiagnosticState()=>new{session,pinnedSession,heldKeys=heldKeys.Order().Select(k=>$"0x{k:X2}").ToArray(),knownDownCount=knownDown.Count,reconciledKeys,recoveredReleases};
+    private bool IsCaptured(uint key)=>key==9&&swallowedTab||key==0xC0&&swallowedGrave||key==0x20&&swallowedSpace||key==0x7B&&swallowedPause||key==0x1B&&swallowedEscape||(key is >=0x25 and <=0x28)&&(swallowedArrows&(1<<((int)key-0x25)))!=0;
+    // Called on the hook thread after callbacks, when Windows has updated async
+    // state. Never infer release of intercepted keys from their async state.
+    public void ReconcileReleased(bool alt,bool ctrl,bool shift){
+        if(!session)return;
+        if(keyIsDown!=null){
+            bool Stale(uint key)=>!IsCaptured(key)&&!keyIsDown(key);
+            reconciledKeys+=knownDown.RemoveWhere(Stale);heldKeys.RemoveWhere(Stale);
+        }
+        if(alt||ctrl||shift||heldKeys.Count!=0)return;
+        session=false;bool commit=!pinnedSession;pinnedSession=false;recoveredReleases++;
+        if(commit)post(SwitchCommand.Commit);
+    }
     public bool Route(uint key,bool down,bool alt,bool ctrl,bool shift,bool enabled,bool otherAltDown=false,bool otherCtrlDown=false,bool otherShiftDown=false) {
         bool isAlt=key is 0xA4 or 0xA5 or 0x12;
         bool isCtrl=key is 0xA2 or 0xA3 or 0x11;
@@ -24,6 +42,15 @@
         bool altHeld=isAlt?down||otherAltDown:alt;
         bool ctrlHeld=isCtrl?down||otherCtrlDown:ctrl;
         bool shiftHeld=isShift?down||otherShiftDown:shift;
+        // Prior key-down callbacks are observations, not proof a key is still held.
+        // Reconcile OTHER uncaptured keys against Windows before starting/ending
+        // a gesture. Captured keys can have no OS down-state: only their own up
+        // event may release them. Never poll or sleep in the input callback.
+        if(keyIsDown!=null){
+            bool Stale(uint k)=>k!=key&&!IsCaptured(k)&&!keyIsDown(k);
+            reconciledKeys+=knownDown.RemoveWhere(Stale);
+            heldKeys.RemoveWhere(Stale);
+        }
         if(!isAlt&&!isCtrl&&!isShift){if(down)knownDown.Add(key);else knownDown.Remove(key);}
         if(!down)heldKeys.Remove(key);
         else if(session&&!isAlt&&!isCtrl&&!isShift)heldKeys.Add(key);
@@ -68,15 +95,17 @@
     private readonly ManualResetEventSlim ready = new();
     private nint handle;
     private nint foregroundHook;
+    private nuint releaseTimer;
     private uint threadId;
     private Exception? failure;
     private long eventCount, tabCount;
     private bool lastTabAlt, lastTabCtrl, lastTabAltFlag,lastTabAltPhysical;
-    public object Statistics => new {present=handle!=0,foregroundPresent=foregroundHook!=0,alive=thread.IsAlive,events=Interlocked.Read(ref eventCount),tabs=Interlocked.Read(ref tabCount),lastTabAlt,lastTabCtrl,lastTabAltFlag,lastTabAltPhysical};
+    private object routingState=new{session=false};
+    public object Statistics => new {present=handle!=0,foregroundPresent=foregroundHook!=0,alive=thread.IsAlive,events=Interlocked.Read(ref eventCount),tabs=Interlocked.Read(ref tabCount),lastTabAlt,lastTabCtrl,lastTabAltFlag,lastTabAltPhysical,routing=Volatile.Read(ref routingState),physicalAlt=Native.GetAsyncKeyState(0x12)<0,physicalCtrl=Native.GetAsyncKeyState(0x11)<0,physicalShift=Native.GetAsyncKeyState(0x10)<0};
     internal static bool AltSnapshot(uint key,uint flags,bool physicalAlt)=> (flags&0x20)!=0 || (key is not (0x12 or 0xA4 or 0xA5)&&physicalAlt);
     public volatile bool Enabled = true;
     public KeyboardHook(Action<SwitchCommand> post,Action<nint,uint>? foreground=null) {
-        router = new ShortcutRouter(post); callback = Callback;
+        router = new ShortcutRouter(post,key=>Native.GetAsyncKeyState((int)key)<0); callback = Callback;
         foregroundCallback=(_,_,hwnd,_,_,_,time)=> {
             // Delayed notifications describe history, not necessarily current focus.
             if(hwnd!=0&&hwnd==Native.GetForegroundWindow())foreground?.Invoke(hwnd,time);
@@ -93,8 +122,19 @@
         if (handle == 0) failure = new Win32Exception(Marshal.GetLastWin32Error());
         ready.Set();
         if (handle == 0) return;
-        try { while (Native.GetMessage(out _, 0, 0, 0) > 0) { } }
-        finally { Native.UnhookWindowsHookEx(handle); handle = 0;if(foregroundHook!=0)Native.UnhookWinEvent(foregroundHook);foregroundHook=0; }
+        try {
+            while (Native.GetMessage(out var message, 0, 0, 0) > 0) {
+                if(message.Msg==0x113&&releaseTimer!=0&&message.WParam==releaseTimer){
+                    router.ReconcileReleased(Native.GetAsyncKeyState(0x12)<0,Native.GetAsyncKeyState(0x11)<0,Native.GetAsyncKeyState(0x10)<0);
+                    Volatile.Write(ref routingState,router.DiagnosticState());UpdateReleaseTimer();
+                }
+            }
+        }
+        finally { if(releaseTimer!=0)Native.KillTimer(0,releaseTimer);Native.UnhookWindowsHookEx(handle); handle = 0;if(foregroundHook!=0)Native.UnhookWinEvent(foregroundHook);foregroundHook=0; }
+    }
+    private void UpdateReleaseTimer(){
+        if(router.IsActive&&releaseTimer==0)releaseTimer=Native.SetTimer(0,0,16,0);
+        else if(!router.IsActive&&releaseTimer!=0){Native.KillTimer(0,releaseTimer);releaseTimer=0;}
     }
     private nint Callback(int code, nint wp, nint lp) {
         if (code < 0) return Native.CallNextHookEx(handle, code, wp, lp);
@@ -113,7 +153,11 @@
             bool otherAltDown=k.Key==0xA4?Native.GetAsyncKeyState(0xA5)<0:k.Key==0xA5&&Native.GetAsyncKeyState(0xA4)<0;
             bool otherCtrlDown=k.Key==0xA2?Native.GetAsyncKeyState(0xA3)<0:k.Key==0xA3&&Native.GetAsyncKeyState(0xA2)<0;
             bool otherShiftDown=k.Key==0xA0?Native.GetAsyncKeyState(0xA1)<0:k.Key==0xA1&&Native.GetAsyncKeyState(0xA0)<0;
-            if(router.Route(k.Key,down,alt,ctrl,shift,Enabled,otherAltDown,otherCtrlDown,otherShiftDown))return 1;
+            bool wasActive=router.IsActive;
+            bool captured=router.Route(k.Key,down,alt,ctrl,shift,Enabled,otherAltDown,otherCtrlDown,otherShiftDown);
+            if(wasActive||router.IsActive||k.Key==9)Volatile.Write(ref routingState,router.DiagnosticState());
+            UpdateReleaseTimer();
+            if(captured)return 1;
         } catch { /* A hook must never propagate exceptions into user input. */ }
         return Native.CallNextHookEx(handle, code, wp, lp);
     }
~~~~~

### SwitcherForm.cs

~~~~~diff
--- 1.1.7/SwitcherForm.cs
+++ 1.1.8/SwitcherForm.cs
@@ -133,7 +133,9 @@
     public void TrackForeground(nint hwnd)=>TrackForeground(hwnd,unchecked((uint)Environment.TickCount));
     public void OpenSwitcher(bool pinned, int direction = 1, bool sameApp = false) {
         if(committing)return;
-        if (Visible) { persistent |= pinned; activation.Request(Handle); MoveSelection(direction); return; }
+        // Each Alt+Tab gesture selects its own lifetime. A previously pinned UI
+        // must not prevent a new plain gesture from committing on key release.
+        if (Visible) { persistent = pinned; releaseTimestamp=0;if(!pinned)ActiveControl=null;activation.Request(Handle); MoveSelection(direction); return; }
         opening = Stopwatch.StartNew();
         PrepareSwitcher(pinned,direction,sameApp);
         ApplyBounds();EnsurePage(); ApplyTheme();
~~~~~

### 사용법.txt

~~~~~diff
--- 1.1.7/사용법.txt
+++ 1.1.8/사용법.txt
@@ -1,4 +1,11 @@
-AltTabFlow 1.1.7 · Windows x64
+AltTabFlow 1.1.8 · Windows x64
+
+[1.1.8 일반 Alt+Tab 종료 수정]
+일반 Alt+Tab은 키를 모두 놓으면 선택 창으로 전환하고 닫힙니다.
+이전에 Ctrl+Alt+Tab으로 고정 화면을 열어 두었어도 새 일반 Alt+Tab은 빠른 전환으로 돌아갑니다.
+다른 앱에서 이미 놓은 키의 기록이 남아 종료를 막던 경로를 정리했습니다.
+실제로 누르고 있는 Tab·방향키 등은 마지막 키를 놓을 때까지 기다립니다.
+Ctrl+Alt+Tab 고정 모드는 계속 키를 놓아도 유지되며 Enter로 전환, Esc로 취소합니다.
 
 [1.1.7 입력·앱 식별 개선]
 숨길 앱 입력칸의 Enter는 줄바꿈입니다. 등록을 마치면 변경 사항 저장을 누르세요.
~~~~~
