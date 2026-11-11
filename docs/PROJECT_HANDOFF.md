# AltTabFlow — 후속 세션 인계 / START HERE

## 1.1.15 회사 수정본 통합 — 2026-11-11

- 사용자 업로드 `AltTabFlow_1.1.9_갱신내용.md`, `AltTabFlow_1.1.10_갱신내용.md` 순서 통합. 업로드 파일은 공개 GitHub로 원문 업로드하지 않음. 이전 1.1.9/1.1.10이라는 버전 번호는 회사 로컬 기준이므로 GitHub 최신 버전은 **1.1.15**로 증가.
- 1.1.9: Excel A↔B 같은 프로세스의 독립 HWND MRU, 그룹/대표 분리, 제목 갱신 및 Enter/Backspace 선택 유지. Chrome에도 적용, owner/popup 체인 제외. 원본 창 키보드 훅과 MRU 기록 변경하지 않음. 실제 foreground 회귀 12건 추가.
- 1.1.10: 기본 MRU, 옵션 `SortAlphabetically`과 `PrioritizePinned` 각각 기본 false. 그룹 정렬은 최근 멤버의 `Windows[0].Order`; 선택 문서 HWND 변경으로 그룹 위치가 바뀌지 않음. UI 저장/취소 후 적용, 기존 설정 무손실. 신규 Ctrl+P/우클릭 등록 시 우선 표시 활성화.
- 기존 1.1.13 게임/프리뷰·1.1.14 48명령/8ms 큐 안정성 보존. CI 목표: 기본 **284** + UI **106** + updater22 + 관리자11 = **423**. 실제 Excel 파일 사용자 PC, 메이플 Alt+Enter, 전경/Peek/메뉴/UAC 검사는 무인 CI와 별도.
- 배포 전 새 PR Windows CI success, main 동일 SHA Windows CI success, 릴리스 ZIP/SHA256SUMS 게시를 각각 확인해야 함.



## 1.1.14 빠른 Alt+Tab 반복 입력·후속 먹통 방지 (2026-11-11)

- 기존 1.1.13 메이플 프리뷰/포커스 수정은 PR #11, CI #42855755651(335개) 성공 이후 main 병합 커밋 cde56a70.
- 추가 사용자 증상: Alt+Tab 빠른 반복 중 UI가 오래 남다가 사라진 뒤 다음 입력도 먹통처럼 보임.
- 정적 코드의 유력 위험: `SwitcherContext.DrainInput`이 큐가 빌 때까지 UI 루프를 무제한 점유하고, 매 처리마다 전체 대기 큐 `ToArray()` 복사 및 드레인 중복 `BeginInvoke` 가능. WinForms Paint/WM_TIMER/Commit 동작 지연으로 연결될 수 있음.
- 대응: UI 턴당 **최대 48명령 또는 8ms** 처리 후 잔여 FIFO 재예약; `drainPosted`는 드레인 종료 시 해제, `CommitFinished` 동기 재진입 보호. 이미 chooser가 foreground면 중복 활성화 Request 생략.
- PR 브랜치 `fix/v1.1.14-rapid-alt-tab-queue-stability`, CI 목표 기본 206 + UI 99 + updater 22 + 관리자 11 = **338건**.
- 실제 메이플 Alt+Enter, 키 연타 10~30회, 포커스/UAC/자동 EXE 교체는 Windows 사용자 환경에서 별도 검증.



## 1.1.13 사용자 실사용 증상과 패치 설계

- 사용자 스크린샷 (사용자 개인 업로드이며 GitHub에는 업로드하지 않음): 메이플 목록에서 MapleStory 항목을 거친 다음 전환 UI가 1~2회의 Alt+Tab 동안 가려졌다가 복구됨. 오른쪽 썸네일 카드의 `선택한 창 미리보기` 라벨과 창 제목 `MapleStory`의 글자 겹침.
- 이전 구현의 `TargetPeek.Select`가 선택된 **MapleStory 게임 HWND 자체를 chooser 뒤의 topmost Z-order 밴드로 임시 승격**. Alt+Enter 후 game fullscreen이 focus/desktop composition을 되잡는 상황의 유력 원인 후보. 선택 시 MapleStory 외부 Peek/Aura/Dim 억제, DWM 썸네일은 유지. 실제 실행 대상 전환은 기존 `Native.Activate`를 사용.
- `SwitcherForm.Deactivate`는 origin으로 돌아온 경우 외에는 즉시 종료했기 때문에 게임이 타 창에서 재활성화돼도 종료됨. 실제 Alt 물리 키가 눌렸거나 마지막 전환 입력 220ms 이내의 **일반** Alt+Tab 세션을 잠시 보호(고정 세션 제외).
- 미리보기 하단 안내는 창 제목 영역과 중복되어 제거. `LayoutPreviewSidebar`는 제목이 버튼 위로 오도록 가용 높이 조절. 확대 UI 580/650/735/950 logical viewport 검사.
- 신규 UI 검사 11개, 목표: 기본206 + UI96 + updater22 + elevated startup11 = 총 **335**.
- 메이플 Alt+Enter 실사용·독점 전체화면 표시는 CI에서 검증할 수 없음. 안전하게 기존 1.1.12 유지한 채 1.1.13 릴리스 설치 후 직접 전환 비교.

## 1.1.12 최신 확정 배포 결과 (2026-11-11)

- **main 1.1.12**: [PR #10 병합](https://github.com/hossea123456789-design/AltTabFlow/pull/10), [main Windows CI #42850336840 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42850336840): 기본206 + UI85 + updater22 + 관리자 시작11 = **324/324 자동 검사 통과**.
- **단일 Public 저장소**: `hossea123456789-design/AltTabFlow` 공개 상태 확인. 두 번째 저장소/PAT/배포 Secret 필요 없음.
- **자동 공개 릴리스 게시 실제 성공**: [Actions #42850768898](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42850768898), [v1.1.12 Release](https://github.com/hossea123456789-design/AltTabFlow/releases/tag/v1.1.12) 두 자산 확인: `AltTabFlow-win-x64.zip` (70,529,517바이트, SHA-256 `54c7755a7d40c9fa4f95ba31bfb7b2b1a39e2906843c2156a7062f936cf97ea4`) 및 `SHA256SUMS.txt`.
- **사용자 최초 전환**: 1.1.11 이하에서는 이 1.1.12 공개 ZIP을 직접 받아 기존 실행 파일을 완전히 종료하고 한 번 교체. 이후 내장 '업데이트 확인'은 같은 Public Releases에서 토큰 없이 동작하도록 구현됨.
- **미검증**: 실제 메이플스토리 Alt+Enter 독점 전체화면/UAC·예약 작업, 다음 공개 버전으로 인앱 다운로드→파일 교체→재시작 종단간. WinForms CI 시뮬레이션과 혼동 금지.
- 아래의 구버전 기록은 *당시 상태의 히스토리*이므로 현재 배포 안내보다 우선하지 않음.


> **이 문서가 프로젝트의 단일 인계 시작점입니다.** 새로운 ChatGPT/개발 세션에서는 이 문서와 실제 `main` 코드, 최근 PR/CI, Issue #1을 먼저 읽으세요. 이전 채팅 기록이나 특정 컴퓨터의 파일 경로가 없어도 이어서 개발할 수 있어야 합니다.

## 저장소 및 현재 기준

- 저장소: [hossea123456789-design/AltTabFlow](https://github.com/hossea123456789-design/AltTabFlow) (**Private**)
- 기본 브랜치: `main`; 변경은 `update/vX.Y.Z-...` 브랜치와 Pull Request로 진행.
- **1.1.13 수정 진행:** `fix/v1.1.13-maple-fullscreen-peek-preview-layout`에서 MapleStory Alt+Enter 후 전환창이 1~2회 가려지는 현상 및 오른쪽 제목 겹침 수정. CI/병합/릴리스 검증 전이며 **1.1.12 공개 배포 성공 기록은 유지**.

- **1.1.12 단일 Public 저장소 전환 완료 (기존 작성 당시 PR 진행):** `feature/v1.1.12-single-public-repo-auto-release`에서 별도 `AltTabFlow-Downloads` 및 배포 PAT 의존성 제거, 동일 저장소 Release API + GITHUB_TOKEN 배포. GitHub 연결로 repo visibility 변경은 불가능하므로 **사용자의 일회성 Public 전환 필요**. 승인 전 실제 공개 릴리스가 생성됐다고 주장하지 말 것.
- **직전 `main`: 1.1.11.** [PR #9](https://github.com/hossea123456789-design/AltTabFlow/pull/9) [성공 Windows CI #42842347985](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42842347985) 검증 후 [커밋 93fd8ce](https://github.com/hossea123456789-design/AltTabFlow/commit/93fd8ceb7e4fef0114a7ba941b9a1c78100493f7)로 병합 완료. 기본206 + UI85 + 업데이터22 + 관리자 시작11 = **324/324개 자동 검사 통과**. 실행 후보 ZIP·메인/설정/업데이트 WinForms 캡처 업로드 성공. **공개 실행 파일 저장소 미생성·실제 MapleStory/온라인 갱신 검증 미실행은 별도**.
- **이전 기준 1.1.10:** [PR #8](https://github.com/hossea123456789-design/AltTabFlow/pull/8)을 [Windows 성공 CI #42832500112](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112) 이후 [커밋 0841c00](https://github.com/hossea123456789-design/AltTabFlow/commit/0841c00da08fa5287e971197894dfd2987b4ac14)로 병합했습니다. SHA-256·컴파일·단일 EXE·기본206/UI80/업데이터11/관리자 시작11 = **308개 CI 검사 성공**, 실제 메인·설정·업데이트 창 캡처 및 ZIP 업로드 성공.
- **1.1.9 과거 기준:** 1.1.9, [PR #7](https://github.com/hossea123456789-design/AltTabFlow/pull/7)을 [Windows 성공 CI #42812687675](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42812687675) 이후 [커밋 51175f3](https://github.com/hossea123456789-design/AltTabFlow/commit/51175f30dd9c0e0aa8792593e3956703f7f24e8d)로 병합했습니다. 체크섬·컴파일·단일 EXE 게시·기본 206/206·UI 71/71·Updater 11/11·실제 메인/설정 캡처 및 동일 폴더 배포 후보 업로드까지 성공했습니다.
- **직전 기준 버전 `main`: 1.1.8**, 소스 병합 커밋 [`8fca819`](https://github.com/hossea123456789-design/AltTabFlow/commit/8fca8193761bca80812b20c188350d7e4165f34f) ([PR #6](https://github.com/hossea123456789-design/AltTabFlow/pull/6)). 새 세션에서는 최신 `main` HEAD와 PR/Actions를 다시 확인하세요.
- **1.1.8 변경:** 일반 Alt+Tab 종료 누락 복구, 16ms 활성 세션 복구 타이머, 오래된 키 기록 정리, 이전 고정 UI 재시작 상태 초기화. [PR #6 Windows CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910)에서 SHA-256·.NET 컴파일·단일 EXE·기본 206개·UI 71개·메인/설정 스크린샷 성공.
- **1.1.7 변경:** 9개 소스 파일 변경, 전체 인계 문서 추가, 줄바꿈 정책·SHA-256 검증 자동화. [Windows PR CI #42777258155](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42777258155)에서 체크섬·컴파일·EXE 게시·기본 161개·UI 71개·실제 WinForms 메인/설정 화면 캡처 성공. 새 세션에서는 최신 `main`/PR/Actions를 다시 확인.
- 주 이슈: [#1 실사용 검증 체크리스트](https://github.com/hossea123456789-design/AltTabFlow/issues/1).
- 상세 기록: [개발 이력](DEVELOPMENT_HISTORY.md), [변경 불가 동작 계약](ARCHITECTURE_AND_INVARIANTS.md), [검증 매트릭스](TEST_MATRIX.md), [1.1.8 원문 변경 명세](specs/AltTabFlow_1.1.8_갱신내용.md).
- 이 문서의 기준 시점: **2026-11-10**. 이후 결과는 PR, CI 로그, 이슈에 반드시 추가하세요.

## 애플리케이션 목적

.NET 8 / Windows x64 / WinForms·GDI 기반 Windows Alt+Tab 대체 창 전환기. 빠른 직전 앱 MRU 전환, 앱별 묶기, 목록/카드/아이콘, Peek·아우라·딤드, 7종 색상·설정, 시스템 트레이, 로그인 자동 실행, 관리자 권한 게임/메이플스토리 호환을 제공. 외부 웹 프레임워크 없이 로컬에서 동작.

### 주요 소스 안내

| 파일 | 역할 |
|---|---|
| `Program.cs` | 앱 시작, 명령 라우팅, 자체 회귀 검사 |
| `KeyboardHook.cs` | 저수준 키보드 훅, `ShortcutRouter`, 눌린 키·고정 세션 구별 |
| `SwitcherForm.cs` | 실제 전환 UI, 그룹 탐색, Commit/Cancel, 마우스 클릭 |
| `Switching.cs` | 전경 창·HWND 관계, MRU 및 실제 창 활성화 |
| `Models.cs` | `Preferences`, 창 목록, 아이콘 캐시·설정 관리 |
| `SettingsForm.cs`, `UiControls.cs` | 테마형 설정 폼, 앱 목록·아이콘·선택 UI |
| `UiTests.cs`, `MenuTests.cs`, `PeekTests.cs` | UI, 메뉴, 실제 창/전경 회귀 테스트 |
| `TargetPeek.cs`, `TargetAura.cs`, `LayoutPlanner.cs` | 실제 창 Peek, FX, 배치 |
| `package.ps1`, `Windows-Build.cmd` | 실기기에서 런타임 회귀 검사 포함 패키징 |
| `.github/workflows/windows-build.yml` | CI 컴파일/EXE 후보와 기본·UI 자체 검사 |
| `docs/specs/` 및 `docs/1.1.x-...` | 버전별 *요구사항의 원문*과 정밀 diff |

## 최종 동작 계약: 일반 vs 고정

- **일반 Alt+Tab:** Tab/방향키를 탐색하고 Alt/Tab/방향키/수정키를 모두 놓는 마지막 key-up에서 현재 하이라이트 창을 한 번 전환. Esc는 취소하며 잔여 키 해제에 재전환하지 않음. Chrome 등 묶음은 표시된 대표 창; Enter로 펼친 경우 개별 하이라이트 창.
- **Ctrl+Alt+Tab (1.1.7):** 키를 모두 놓아도 **화면을 유지**; 반복 시 다음, Ctrl+Alt+Shift+Tab 시 이전. Alt+Tab 중 Ctrl을 뒤늦게 누르면 현재 선택을 유지한 채 고정 모드로 승격. 방향키·Backspace 탐색, Enter 확정, Esc 취소. Ctrl+Alt+Space 명시적 고정 화면도 유지.
- **P4 및 소유 팝업:** 실제로 클릭한 전경 창과 소유 관계를 고려해 MRU 왕복. 실제 전경 활성화 성공을 확인한 뒤 전환기 종료.
- **1.1.6 UI:** 좌측 메뉴/설정 카드/하단 저장, 7종 테마, 모니터·크기·보기·제외 목록; 미리보기·취소는 원본 설정 파일을 변경하지 않음.
- **1.1.7 설정:** 숨길 앱 입력창 Enter는 줄바꿈; 수동 상단 등록 앱 이름 입력 Enter는 등록. 상단 등록 앱 행에는 체크+실제 아이콘+앱/프로세스 이름, 아이콘 없으면 글자 배지.

## 1.1.11: 메이플 전체화면/포커스와 공개 바이너리

- 사용자 증상: 메이플에서 `Alt+Enter`로 화면을 확장하면 게임이 화면·포커스를 점유, AltTabFlow 선택 UI는 뒤에 가려지고 다른 앱 선택 뒤에도 Maple이 남아 있음.
- 코드 조사: 이전 `SwitcherForm.Deactivate`는 게임이 원래 전경을 되찾으면 화면을 즉시 `Dismiss(false)`했음. `SwitcherForm.Choose`는 대상 HWND의 진짜 활성화 이전에 `TargetPeek.Accept/Restore`를 실행, 전환을 위한 임시 Z-order를 조기 해제. `Native.Activate`는 `SetForegroundWindow`의 bool을 실제 활성화로 신뢰하고, `WindowActivation` 250ms·`SwitcherForm` 250ms에서 전환 확인을 끝냈음.
- 개선: AltTabFlow 선택 UI에 `HWND_TOPMOST`를 비활성화 없이 재적용, 게임 원래 창이 전경을 되찾으면 선택 UI를 즉시 종료하지 않음. 실제 foreground 확인과 bounded 850/900ms 활성화 재시도. 전환 대상의 `peek` Z-order는 실제 활성화 확인 뒤 복원. 원래 게임 창 강제 최소화/TopMost 영구 변경 금지.
- **독점 전체화면 한계:** DirectX exclusive fullscreen 또는 게임 보호 기능에서는 일반 TopMost 오버레이/전경 활성화 보장이 불가능. 사용자 PC의 게임 전체화면 창 모드 vs 독점 전체화면을 구분해 실험.
- 배포: `AltTabFlow` 소스 저장소 PRIVATE 유지. 실행 파일만 `AltTabFlow-Downloads` PUBLIC Release(프리릴리스, ZIP+SHA256SUMS)로 배포. 클라이언트 인증 UI/PAT 저장·비공개 Actions 조회 제거, 공개 Releases API에서 버전·digest 검증 후 업그레이드.
- 한 번 필요한 외부 설정: **PUBLIC `AltTabFlow-Downloads` 저장소 생성** 및 PRIVATE 소스 repo Actions Secret `ALTTABFLOW_PUBLIC_RELEASE_TOKEN`(대상 공개 저장소 Contents:RW 범위만). 현재 GitHub 커넥터가 저장소 생성과 Secrets 편집을 제공하지 않으므로 자동화 워크플로는 등록하되 배포가 **실제로 공개됐다고 말하지 않음**.
- 자동 검사 목표: 기본206 + UI85(+5 game overlay) + 업데이트22(+11 public releases) + 관리자 시작11 = **324개**. 물리 메이플/P4·전경/Peek·실제 공개 다운로드 및 Windows 자동 교체는 별도 검증.

## 1.1.10 사용자 보고 버그·후속 개발

1. 1.1.9까지는 Windows Run 키가 `asInvoker` EXE를 실행하므로 재부팅/로그인 후 관리자 권한을 유지하지 못함. 설정 체크+저장 → UAC 1회 → Program Files 보호 설치 → 작업 스케줄러 현재 사용자 Interactive+Highest 로그온 작업으로 등록. 일반 Run 키와 중복 시작 금지, 해제 시 작업 제거.
2. 1.1.9에서 '업데이트 확인' 후 정체불명 작은 창이 곧 사라지고 UI가 먹통(ESC로 복구)이라는 보고. 원인을 단정하지 않되 기존 `PromptToken`의 중첩 모달/TopMost 소유 관계를 제거하고 `UpdateWindow`에서 계속 보이는 토큰·진행·취소 UI로 전환. 숨은 팝업 없이 오류·취소도 창에서 처리.
3. 기존 `SwitcherForm.MouseWheel`에서 `persistent=true`를 무조건 설정한 명확한 버그. 휠은 `MoveSelection`만 수행, 일반 빠른 Alt+Tab의 마지막 key-up에서 하이라이트된 창 Commit. 명시적 Ctrl+Alt+Tab 고정에서만 해제 후 화면 유지.
4. [PR #8 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112)에서 기본206, UI80, updater11, elevated startup11 = 총 **308개 자동 검사 통과**. 기존 Peek10/Menu7/Focus18(Peek10 중복)와 실제 로그인/UAC·메이플스토리·온라인 교체는 별도. 정식 Release 전 실측.
5. 새 API/테스트: `ElevatedStartup.cs`, `UpdateWindow.cs`, `--startup-self-test`, `--update-preview` 및 CI 화면 캡처.

## 1.1.9 게임 관리자 모드 / EXE 자체 업데이트

- 문제: 기존 manifest는 `asInvoker`, CI ZIP에서 `AltTabFlow.exe`는 `artifacts/ci-publish/` 아래, 게임 호환 스크립트는 ZIP root. EXE를 관리자 권한으로 재실행해도 동일 사용자 mutex의 일반 권한 인스턴스가 열려 있으면 새 관리자 인스턴스가 바로 종료될 수 있음.
- 수정: 트레이/설정의 게임 관리자 다시 시작 → UAC 승인 후 기존 프로세스 `--quit` IPC → per-user Mutex takeover → 관리자 `--background` 실행. UAC 취소는 기존 프로세스 보존.
- 자동 업데이트: 트레이/설정 확인 → 첫 1회 Fine-grained PAT(Contents:read, Actions:read, 전용 저장소만), Windows Credential Manager 보관 → `main` 최신 성공 CI 버전 확인 → ZIP 자동 다운로드/검증 → 별도 updater EXE가 기존 프로세스 종료 후 백업/교체 → 재시작. `settings.json` 수정 금지.
- GitHub Actions 실행 후보 아티팩트 이름은 **`AltTabFlow-win-x64-CANDIDATE`**로 안정화, EXE와 호환 실행 스크립트 동일 폴더 배치. 이전 1.1.8 사용자는 1.1.9를 **처음 한 번만 수동으로 설치**해야 함.
- 현재 Github repo는 **비공개**이며 GitHub Releases는 아직 검증 완료 상태로 게시하지 않았습니다. 따라서 최초 자동 업데이트는 정식 Release API가 아니라 Actions 최신 성공 후보를 조회하며, 실제 Windows 관리자 게임/UAC 검증 완료를 의미하지 않습니다.
- 신규 `GitHubUpdater.cs`, `GameCompatibility.cs`. `--updater-self-test` 11개 추가. 기존 기본 206·UI 71 검사는 유지.
- 독립 Windows PC에서 반드시 검증할 사항: UAC 수락/취소, 기존 일반 인스턴스 인계, 관리자 게임 내 실제 Alt+Tab, 저장된 토큰/권한, 자동 다운로드·교체·롤백·게임 스크립트 동봉.

## 1.1.8 종료 누락 수정 / 새로운 세션에서 반드시 확인할 내용

- 관측: 일반 Alt+Tab 화면 `visible=true`, `persistent=false`, `lastTabCtrl=false` 상태에서 `Next`는 반복되고 `Commit`만 누락. 정확한 원인 가상 키는 확인되지 않았습니다.
- 해결: 훅이 놓친 이전 key-up 기록(knownDown)을 실제 Windows 눌림 상태와 대조해 정리하고, 입력 세션이 살아 있을 때에만 훅 스레드 16ms WM_TIMER로 해제 여부를 보조 확인합니다. **가로챈 Tab·방향키 등은 async 0으로 놓였다고 단정하지 않습니다.**
- 고정 모드 Ctrl+Alt+Tab은 키를 놓아도 유지하되, **그 뒤 새 일반 Alt+Tab은 일반 모드로 전환**해 마지막 키 해제 시 닫습니다.
- 원본 1.1.8 명세: **기본 206 + UI 71 + Peek 10 + 메뉴 7 + Focus 18(중복10) = 고유 302개** 통과 기록. 명세 작성자가 실제 사용자 입력에서 일반 2~3회와 고정 조합 정상 동작을 확인했다고 기재했습니다. 이 별도 Windows 환경의 사실을 GitHub CI/현재 세션 실기기 통과와 섞지 마세요.
- **이번 GitHub CI에서 기본 206/206 + UI 71/71 성공을 확인하고 PR #6을 `main`에 병합**했습니다. [CI 기록](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910). 나머지 고유 25개 및 실제 관리자 게임·P4 입력은 별도 검증 대상으로 유지합니다.

## 과거 1.1.7 검증 범위와 주의

1.1.7 인계 문서에는 **고유 257개** 통과 기록(기본 161 + Peek 10 + 메뉴 7 + Focus 18 중 Peek 10 중복 + UI 71)이 있습니다. **이는 인계 문서 작성 환경의 기록**으로, GitHub CI 및 실제 사용자 PC 재검증과 구별해야 합니다.

이전 1.1.6 GitHub CI는 기본 117개 + UI 62개 = 179개 성공 ([PR #4 검증 실행](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42768914976), [main 실행](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42769488092)). 1.1.7은 [PR #5](https://github.com/hossea123456789-design/AltTabFlow/pull/5)를 **CI 161/161 + UI 71/71 통과 후 병합**했습니다. 후속 버전 역시 새로운 CI와 Windows 실기기 검사로 별도 검증하세요. 실제 Peek/전경/메뉴/게임·혼합 DPI 검증은 CI 화면 캡처만으로 완료 처리하지 마세요.

인계 문서에는 **일반 권한으로 전체 전경 테스트 실행 시 관리자 메이플스토리가 전경을 점유해 실패가 반복되고, 관리자 권한 전체 재검사에서 통과**한 사례가 있습니다. 실패를 은폐하거나 임의로 테스트 합격으로 바꾸지 마세요. 조건·권한·foreground HWND를 기록하고 재현하세요.

## 새 세션의 첫 작업 순서

1. GitHub 저장소의 `main`, 최근 PR, Actions, Issue #1 및 이 문서를 조회해 실제 반영 버전/검증 상태를 확인합니다.
2. 변경 대상 버전의 **원본 명세**와 직전 소스를 함께 읽습니다. 새 파일이 unified diff인 경우 문맥·개수·인코딩(BOM)을 검증 후 적용합니다.
3. 이슈에 재현 절차를 기록하고 별도 브랜치를 만듭니다. 코드만 수정하지 말고 테스트/CHANGELOG/원본 명세/인계 문서까지 함께 갱신합니다.
4. Windows CI 컴파일 및 기본·UI 검사 통과 후 PR 병합. Windows 실사용 제스처·권한·실제 창 테스트는 별개로 체크합니다.
5. 검증 전에는 정식 배포 버전 태그/Release를 만들지 않습니다. CI EXE는 **후보**이며 원본 명세가 언급한 EXE와 SHA-256이 다를 수 있습니다.
6. 최종 응답에는 커밋·PR·Actions URL, 실행한 테스트 수, **실행하지 못한 테스트**, 다음 작업 항목을 남깁니다.

## 안전·운영

- `settings.json`, 창 제목·프로세스 경로·사용자 로그, CI 밖의 개인 환경 진단을 GitHub에 올리지 않습니다.
- 기본 설정·기존 로그인 자동 실행 선택·관리자 실행 여부를 함부로 초기화하지 않습니다.
- `Publish-To-GitHub.cmd`는 **새 저장소를 만들 때만** 쓰는 초기화 도구입니다. 이미 존재하는 이 저장소를 덮어쓰는 용도로 쓰면 안 됩니다.
- 무인 Windows Server의 screenshot과 컴파일 성공 ≠ 물리 키보드/메이플스토리/P4 실기기 성공.

**다음 세션에서 그대로 사용할 요청 예시:** “GitHub `hossea123456789-design/AltTabFlow`의 `docs/PROJECT_HANDOFF.md`, `docs/DEVELOPMENT_HISTORY.md`, Issue #1과 최신 PR·Actions를 읽고 마지막 안정 상태부터 이어서 작업해 줘. 먼저 현재 버전과 남은 테스트를 보고해 줘.”
