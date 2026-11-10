# 아키텍처 및 회귀 방지 불변 조건

## 1.1.14 빠른 Alt+Tab 큐 안정성 불변 조건

- 키보드 훅 스레드 명령은 FIFO를 보존하고, WinForms UI 드레인은 한 메시지에서 최대 48명령 또는 8ms까지만 처리한 뒤 Paint/WM_TIMER/Commit 확인에 메시지 처리 기회를 주어야 합니다.
- `drainPosted` 플래그는 실제 드레인 종료 시점에 초기화하며 동기 `CommitFinished` 재진입/예외 시 잔여 큐를 누락하지 않도록 다시 게시합니다.
- 매 명령 큐 전체 `ToArray()` 금지. 이미 chooser 자체가 foreground이면 불필요한 `activation.Request(Handle)` 재시작 금지.
- 일반 마지막 key-up 한 번 Commit, 명시적 고정, ESC 취소, 게임 Z-order/Peek 복원 및 900ms 유한 활성화 제한 불변.



## 구성과 의존성

- **플랫폼:** Windows x64, .NET 8, WinForms·GDI. Windows 시스템 Alt+Tab 파일을 수정하지 않음.
- **입력:** `KeyboardHook`(Win32 low-level hook) → `ShortcutRouter`(순수 명령/키 상태) → `Program.cs` 명령 디스패치 → `SwitcherForm`.
- **화면:** `SwitcherForm`(항목/그룹·선택·FX), `LayoutPlanner`(배치), `TargetPeek`(실제 HWND), `TargetAura`(강조), `SettingsForm`+`UiControls`(테마형 설정).
- **창/전환:** `Models` 창 데이터·설정·아이콘 캐시, `Switching` 창 정체성·MRU·비동기 전경 활성화.
- **검증:** `Program --self-test`, `--peek-self-test`, `--menu-self-test`, `--focus-self-test`, `--ui-self-test` 및 `package.ps1`.

## 반드시 유지해야 하는 사용자 동작

### 일반 빠른 전환
- Alt+Tab은 직전 프로그램을 하이라이트하고, Alt 유지 중 Tab 반복/화살표 이동 가능.
- 마지막으로 눌린 관련 키(Alt, Tab, 화살표, Ctrl, Shift)가 **전부 해제된 시점** 한 번만 Commit; 남은 키가 있으면 유지.
- Chrome 묶음 전체는 제목·미리보기에 보이는 대표 창으로; Enter로 그룹을 펼친 경우 하이라이트한 개별 창으로.
- Esc 취소는 같은 입력 세션에서 이후 key-up으로 Commit이 재발행되지 않음.

### 일반 전환 종료 회복 (1.1.8 핵심)
- 과거 창/IME/훅 간 key-up을 놓쳐 남은 `knownDown` 기록이 다음 Alt+Tab을 무한히 막지 않도록 **실제로 놓인 비소비 키만** 정리합니다.
- 라우터가 소비한 Tab·방향키·그레이브 키는 `GetAsyncKeyState`의 0만으로 해제 판정하지 않고 대응 key-up을 기다립니다.
- 훅 스레드의 16ms `WM_TIMER`는 **활성 세션에만** 존재하며 종료 즉시 해제합니다. 정상적인 키 해제 Commit은 즉시, 누락된 Alt-up 복구는 마지막 물리 해제를 확인한 후 일반 모드에서 정확히 한 번만 수행합니다.
- 남아 있는 고정 상세 화면에서 **새 일반 Alt+Tab을 시작할 때 이전 persistent 상태를 상속하지 않습니다**.

### 고정 상세 (1.1.7 핵심)
- Ctrl+Alt+Tab은 고정 선택 화면을 열고 남은 키-up에서 Commit 하지 않음.
- Ctrl+Alt+Tab 반복은 **다음** 항목, Ctrl+Alt+Shift+Tab은 **이전** 항목; 토글 닫기가 아님.
- Alt+Tab 중 Ctrl을 뒤늦게 눌러도 이동 없이 현재 선택을 Pin.
- Ctrl을 먼저 떼어도 고정 세션은 유지. 명시적 Ctrl+Alt+Space 고정 상세와 검색/설정/항목 메뉴도 기존처럼 유지.
- Enter 확정/그룹 펼치기, Backspace 복귀, Esc 취소.

### 창 및 권한
- MRU는 실제 클릭 전경 창과 그 **owned popup** 관계를 고려.
- 대상 전경 전환 성공을 실제 HWND로 확인. 비동기/지연 대상에서도 UI가 오랫동안 블로킹되지 않아야 함.
- 관리자 권한 게임/메이플스토리 호환; 일반 권한의 전경 테스트 실패는 환경 조건과 분리해 진단.
- Peek 종료·취소 시 원래 TopMost/Z-order 복원; 디스플레이/다중 모니터·DPI.

### 설정/UI
- 색상 7종·보기/크기/모니터/정렬/제외/자동 실행 옵션·기존 Preferences 키 유지.
- 테마 미리보기만으로 prefs·settings.json·Windows Run 키 변경 금지. **저장 버튼만 최종 반영**, Esc/취소 시 원본 보존.
- 숨길 앱 다중 행은 Enter가 `\r\n` 줄바꿈, Tab은 컨트롤 이동. 상단 정렬 앱 이름 칸은 Enter로 등록.
- 상단 정렬 목록은 체크, 앱 아이콘(28px), 이름, process.exe; 미존재 경로/아이콘은 첫 글자 배지. 비동기 아이콘 로딩과 cancellation, 체크 선택 보존.
- 테마형 버튼은 mouse-down이 아닌 같은 hit rectangle 안 mouse-up에서 동작, 밖에서 놓으면 취소. 메뉴는 Closed에서 dispose 금지.
- 접근성: Tab, Shift+Tab, Space, 키보드 방향키, Ctrl+Tab 페이지 전환, Esc.

## v1.1.9 보안·업데이트 계약

- 일반 실행은 `asInvoker`로 유지하며, 사용자의 명시적인 게임 호환 버튼 입력에만 `runas` 승격을 요청. UAC 취소 시 이전 프로세스 종료 금지.
- 관리자 인스턴스는 같은 per-user Mutex를 확보하기 위해 기존 프로세스에 정상 `quit`을 요청하고 종료까지 기다림. 강제 종료를 통해 Peek/TopMost 복원을 건너뛰지 않음.
- EXE 및 `Game-compat-mode.cmd`, `Run-GameCompat.ps1`은 같은 폴더에서 배포.
- GitHub 비공개 토큰은 Windows Credential Manager에 저장, UI 설정 JSON/원격 로그/소스에 절대 포함하지 않음. Actions·Contents 읽기 이외의 권한을 요구하지 않음.
- updater는 최신 성공한 `main` 커밋의 아티팩트만 허용하며 버전 상승과 ZIP/실행 파일 검증을 수행, 위험한 경로를 ZIP에서 직접 풀지 않음.
- 실행 중 EXE를 직접 덮어쓰지 않고 **다른 임시 EXE**가 이전 프로세스 종료 후 백업/교체/재시작. 사용자 설정 경로 유지.
- 게임 관리자 모드와 자체 업데이트는 **CI 시뮬레이션만으로 전체 UAC·서명·P4·메이플스토리·교체/재실행 성공으로 단정하지 말 것**.

## 1.1.10 신규 불변 조건

- 일반 Alt+Tab에서 휠을 움직여도 `SwitcherForm.persistent`가 true로 바뀌면 안 됩니다. 선택을 바꾼 뒤 **마지막 물리 키 해제 Commit**. Ctrl+Alt+Tab/명시적 고정에서만 휠 뒤에도 UI 유지.
- 자동 시작은 설정의 `StartWithWindows`와 `ElevatedStartup`으로 결정. 일반 Run 키와 관리자 예약 작업은 동시 사용 금지.
- Highest 작업은 현재 사용자 Interactive Token(다른 계정/SYSTEM 서비스 사용 금지)으로 설정. 1회 UAC 승인을 거쳐 **보호된 Program Files** 설치 경로만 실행 대상으로 등록해야 함.
- UAC 취소/실패 시 기존 설정 파일과 Run 키 보존. 관리자 시작 해제는 예약 작업 제거 및 일반 시작으로 복원.
- 업데이트 버튼은 반드시 **독립된 보이는 Form**을 열고 토큰/취소/오류 상태를 사용자에게 노출. 일시적 `PromptToken` 중첩 모달로 다시 바꾸지 않음.
- 네트워크 다운로드는 cancellation 지원, 사용자 취소 시 실행 파일 교체/프로세스 종료 금지. 업데이트 교체 성공 경로에서만 종료를 허용.

## 1.1.11 전체화면 게임 오버레이 및 전경 전환 계약

- 최대화된 원래 게임이 `GetForegroundWindow()`에서 다시 전경을 점유해도 **AltTabFlow가 화면을 열어 둔 Alt 제스처 도중에는 즉시 Dismiss하지 않습니다.** 원래 게임이 아닌 다른 앱으로 사용자가 명시적으로 이동할 때만 일반 포커스 해제 절차 적용.
- `SwitcherForm`의 표시된 HWND만 `HWND_TOPMOST`로 `SWP_NOACTIVATE` 및 제한된 표시 세션에서 재주장. 게임 HWND/대상 HWND의 TopMost 플래그를 영구 변경하지 말 것.
- `SetForegroundWindow` bool은 실제 활성화 확정과 별개. `GetForegroundWindow`의 owner/root 검증을 확인. 전경 활성화 재시도는 UI 스레드 이외에서 실행하고 짧은 마감 시간(850~900ms)으로 제한.
- `TargetPeek.Accept`/Z-order 원복은 전환 대상의 실제 foreground 활성화를 확인한 뒤에만 처리. 실패하면 Restore를 수행하고 검사 가능한 로그 남기기.
- Alt+Enter가 실제 DXGI 독점 전체화면인 경우 WinForms TopMost 오버레이가 항상 위에 표시된다고 보장 불가. 이 제한을 사용자에게 숨기거나 방어적인 anti-cheat 우회 기법을 도입하지 말 것.
- Chrome/P4 owner popup, 일반 Alt+Tab 마지막 key-up, Ctrl+Alt+Tab 고정 세션, Peek 복원 및 복수 모니터를 반드시 기존 테스트와 함께 재검증.

## 1.1.11 공개 바이너리 배포·인증 계약

- `AltTabFlow` 코드·Actions 원본 소스 저장소는 **Private**, 별도 `AltTabFlow-Downloads`는 **Public (실행 파일 배포 전용)**.
- 앱은 인증이 필요 없는 공개 GitHub Releases API/바이너리 URL만 호출합니다. 토큰 입력 UI, PAT 저장, 비공개 저장소 API 호출을 다시 도입하지 말 것.
- 공개 ZIP은 GitHub Release의 `sha256:` digest, 버전 태그와 EXE 자체 파일 버전을 확인. 모호한 릴리스/경로/무결성 오류는 fail closed.
- 공개 배포 Actions가 원본의 성공 Windows CI를 참조하고 **EXE, 게임 호환 CMD/PS1, 사용법, SHA256SUMS**만 올리며 개인 설정·소스·검사 보고·로그는 공개하지 않음.
- Public repo/쓰기 범위 최소화된 배포 Secret은 별도 초기 설정이 필요. 실제로 Release가 게시되기 전에는 토큰 없는 업데이트를 출시 완료했다고 주장하면 안 됩니다.

## 변경 절차

1. 새 요구사항은 버전 패치 원본을 `docs/specs/`에 보관.
2. 해당 브랜치의 이전 버전 소스에 patch를 **문맥으로 적용**. 인코딩/BOM과 파일 기존 newline을 보존.
3. 핵심 입력/설정/전경 메커니즘을 무심코 변경하지 않고 변경한 계약마다 회귀 검사 추가.
4. CI 결과와 실기기 테스트 보고서를 별도 기록. PR 병합은 빌드/자동 검사 성공 후.
5. 실사용 검증 전 **정식 Release/검증 완료 문구 사용 금지**.

## 1.1.12 단일 Public 저장소 불변 조건

- 앱 `PublicRepo`는 `hossea123456789-design/AltTabFlow` 자체를 가리키며 사용자 인증 없이 공개 Releases만 읽습니다.
- 릴리스 퍼블리셔는 **같은 저장소**의 `github.token`에 `contents:write`/ `actions:read`만 요청. 배포용 PAT/Secret 및 두 번째 저장소를 도입하지 않습니다.
- Private 상태에서는 공개 릴리스를 절대로 생성하지 않습니다. Public 전환은 GitHub Admin의 명시적 승인 필요.
- 신규 릴리스는 반드시 `main` 동일 SHA의 성공한 Windows CI 아티팩트에서만 생성. 배포 파일 화이트리스트 4개와 체크섬, 중복 태그 덮어쓰기 금지.

## 1.1.13 메이플 확대 화면 무작위 가려짐·미리보기 글자 겹침 회귀 계약

- **선택 리스트를 탐색하는 행위 자체가 MapleStory의 실제 HWND TopMost/Z-order를 변경하면 안 됩니다.** `SwitcherForm.ShouldAvoidGamePeek` 판정의 게임은 `TargetPeek.Select`, `TargetDimmer.Select`, `TargetAura.Select`를 모두 생략하되 오른쪽 DWM 썸네일은 계속 표시합니다.
- 일반 Alt+Tab 세션에서 사용자가 물리 Alt를 계속 누르는 동안 또는 마지막 탐색 키 이벤트 후 220ms 동안의 Deactivate는 전환 UI를 즉시 해제하지 않습니다. 이전 기원 창 재획득 시 유지를 허용하지만, 핀 세션에서 진짜 다른 창으로 포커스 이동하면 닫을 수 있어야 합니다.
- 오른쪽 미리보기의 창 제목 영역은 하단 전환 버튼과 절대 중첩하지 않고 '선택한 창 미리보기' 안내를 중복 배치하지 않습니다. 580/650/735/950 logical height 테스트와 UI Scale 회귀를 함께 확인합니다.
- 메이플 게임으로 실제 Commit을 수행할 때는 기존 네이티브 activation을 그대로 사용합니다. 게임 포커스 보호를 위해 무한 반복/포커스 강제 점유/게임 강제 최소화를 도입하지 않습니다.
- 실제 DXGI 독점 전체화면은 CI WinForms 가짜 창으로 검증할 수 없으므로, GitHub CI 성공과 사용자의 메이플 실측을 분리해 기록합니다.
