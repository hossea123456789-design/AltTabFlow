# AltTabFlow — 후속 세션 인계 / START HERE

> **이 문서가 프로젝트의 단일 인계 시작점입니다.** 새로운 ChatGPT/개발 세션에서는 이 문서와 실제 `main` 코드, 최근 PR/CI, Issue #1을 먼저 읽으세요. 이전 채팅 기록이나 특정 컴퓨터의 파일 경로가 없어도 이어서 개발할 수 있어야 합니다.

## 저장소 및 현재 기준

- 저장소: [hossea123456789-design/AltTabFlow](https://github.com/hossea123456789-design/AltTabFlow) (**Private**)
- 기본 브랜치: `main`; 변경은 `update/vX.Y.Z-...` 브랜치와 Pull Request로 진행.
- **업데이트 기준:** 1.1.7 `main` 커밋 `a5ae270`. **1.1.8은 업데이트 브랜치 `update/v1.1.8-missed-keyup-commit`에서 검증 중**입니다. 다음 세션에서는 최신 `main`과 PR을 조회해 병합 여부 및 확정 커밋부터 다시 확인하세요.
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

## 1.1.8 종료 누락 수정 / 새로운 세션에서 반드시 확인할 내용

- 관측: 일반 Alt+Tab 화면 `visible=true`, `persistent=false`, `lastTabCtrl=false` 상태에서 `Next`는 반복되고 `Commit`만 누락. 정확한 원인 가상 키는 확인되지 않았습니다.
- 해결: 훅이 놓친 이전 key-up 기록(knownDown)을 실제 Windows 눌림 상태와 대조해 정리하고, 입력 세션이 살아 있을 때에만 훅 스레드 16ms WM_TIMER로 해제 여부를 보조 확인합니다. **가로챈 Tab·방향키 등은 async 0으로 놓였다고 단정하지 않습니다.**
- 고정 모드 Ctrl+Alt+Tab은 키를 놓아도 유지하되, **그 뒤 새 일반 Alt+Tab은 일반 모드로 전환**해 마지막 키 해제 시 닫습니다.
- 원본 1.1.8 명세: **기본 206 + UI 71 + Peek 10 + 메뉴 7 + Focus 18(중복10) = 고유 302개** 통과 기록. 명세 작성자가 실제 사용자 입력에서 일반 2~3회와 고정 조합 정상 동작을 확인했다고 기재했습니다. 이 별도 Windows 환경의 사실을 GitHub CI/현재 세션 실기기 통과와 섞지 마세요.
- 최신 CI에서 **기본 206/206 + UI 71/71**가 성공했는지 확인한 뒤 PR을 병합하세요. 다른 고유 25개 및 실제 관리자 게임·P4 테스트는 별도로 관리합니다.

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
