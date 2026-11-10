# 아키텍처 및 회귀 방지 불변 조건

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

## 변경 절차

1. 새 요구사항은 버전 패치 원본을 `docs/specs/`에 보관.
2. 해당 브랜치의 이전 버전 소스에 patch를 **문맥으로 적용**. 인코딩/BOM과 파일 기존 newline을 보존.
3. 핵심 입력/설정/전경 메커니즘을 무심코 변경하지 않고 변경한 계약마다 회귀 검사 추가.
4. CI 결과와 실기기 테스트 보고서를 별도 기록. PR 병합은 빌드/자동 검사 성공 후.
5. 실사용 검증 전 **정식 Release/검증 완료 문구 사용 금지**.
