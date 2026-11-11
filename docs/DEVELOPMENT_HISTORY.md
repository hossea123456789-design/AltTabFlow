## 1.1.16 · 회사 로컬 1.1.11 Alt+Tab 잔류 수정 병합 (2026-11-11)

- 비교 기준 GitHub v1.1.15. 회사 관찰로 `pinnedSession=false`, `heldKeys=[0x5D]`인데 일반 Alt+Tab `Commit` 미발생 확인. 로컬 회사 1.1.11 버전명은 과거 GitHub 1.1.11과 무관.
- 세 메뉴/IME 전달키(0x5D/0xE5/0xE7)를 `knownDown`/ `heldKeys` 종료 게이트에서 제외(원래 OS 이벤트 전달). 500ms 이상 대기 로그·보조키 다운 카운터·명시적 pinned 상태 기록. 테스트 28개 추가, 목표 기본314/UI106/updater22/startup11=453.
- 기존 회사 사용자 실사용 정상 확인은 그 로컬 EXE에만 해당하고, GitHub 최신 사용자 Windows 실제 동작은 별도 판단. 이전 공개 v1.1.15 자동 업데이트 기능 유지.

## 1.1.15 · 회사 파일 1.1.9/1.1.10 수정 병합 (2026-11-11)

- 사용자 첨부 `AltTabFlow_1.1.9_갱신내용.md` 및 `AltTabFlow_1.1.10_갱신내용.md`를 기존 공개 1.1.14 소스와 비교 후 통합. 회사 로컬 1.1.9/1.1.10 소스는 GitHub 기존 같은 번호의 변경과 다른 기능임.
- `WindowCatalog.InitialSelection`은 그룹 인덱스 대신 실제 최상위 HWND 후보로 판단. `Entry.SelectWindow`로 선택 대표 분리·그룹/갱신/Backspace 생존.
- MRU 기본 순서, 독립 SortAlphabetically/PrioritizePinned(둘 다 false 기본), 등록 앱 우선 토글 UI, Ctrl+P/우클릭 우선 표시, 새 ZIP 임시 잠금 3회 재시도 구현.
- 기존 메이플 전체화면/빠른 연타/업데이터/관리자 자동 시작 보존. 신규 기본 +78, UI +7(총 286/106), Focus 추가 12 물리 창 검증. 사용자 회사 테스트 통과 사실과 이 GitHub 최신 브랜치의 CI 결과는 별도로 취급.

## 1.1.14 · 빠른 Alt+Tab 연타 시 UI 지연·이후 입력 먹통 방지 (2026-11-11)

- 사용자 보고: Alt+Tab을 빠르게 여러 번 반복하면 선택 UI가 남았다가 뒤늦게 닫히고 그 후 Alt+Tab이 먹통처럼 동작.
- `SwitcherContext.DrainInput`은 UI 스레드에서 큐 전체를 반복 `ToArray()`하고 큐가 빌 때까지 루프. 입력 폭주에서 WinForms Paint·Commit watchdog 굶김 가능. 처리 중 중복 `BeginInvoke`도 원인 후보.
- v1.1.14 패치: 최대 48명령/8ms로 처리 분할 및 나머지 FIFO 재예약, `drainPosted` 해제/재게시 보장, 전환기 이미 foreground일 때 불필요한 activation 작업 억제.
- 기본206+UI99+updater22+startup11=338개 CI 목표. 실제 메이플 Alt+Enter/물리 키 반복은 사용자 Windows 별도 검증.

## 1.1.13 · 메이플 Alt+Enter 전환기 가려짐/미리보기 겹침 수정 (2026-11-11)

- 사용자가 MapleStory 확대(Alt+Enter) 후 전환 목록에서 MapleStory 항목을 한 번 지나가면 전환 UI가 사라졌다가 2회 정도 다른 창을 오가면 다시 표시된다고 보고. 메이플 창 미리보기 카드 하단 안내/창 제목 겹침 스크린샷 제공.
- `TargetPeek`의 game hwnd 임시 topmost 승격을 막도록 MapleStory 선택 시 외부 Peek/Dim/Aura를 건너뜀 (DWM 썸네일은 지속).
- 일시적인 게임 전경 재획득 시 일반 Alt+Tab 세션을 물리 Alt-held 및 최근 입력(220ms) 동안 유지. 고정 상태에서는 기존 포커스 이탈 종료 유지.
- 미리보기 오른쪽 카드 중복 안내 라벨 삭제, 타이틀 영역과 실행 버튼 겹침 방지용 논리 높이 배치.
- CI 목표 335건(206+96+22+11). 실제 독점 전체화면과 메이플 전경 동작의 재현·검증은 사용자 Windows PC가 필요.

## 1.1.12 · 단일 Public 저장소 방식 (2026-11-11)

- 사용자가 기존 AltTabFlow 저장소 자체를 Public으로 전환해 다운로드 저장소·PAT를 없애기로 결정.
- `GitHubUpdater.PublicRepo`를 원본 저장소로 바꾸고, 퍼블리셔를 `contents:write` **기본 GITHUB_TOKEN**으로 변경. CI 성공 SHA 확인·공개 전용 검사·ZIP+SHA256SUMS 공개 릴리스 방식.
- Chat GitHub 연결에는 관리 권한(visibility PATCH)이 없으므로 사용자 GitHub 웹 UI에서 `Change repository visibility → Public` 한 번만 수행. 변경 전 코드는 테스트 가능하지만 업데이트 릴리스는 미게시.
- 기존 Alt+Enter 메이플 화면·전경 개선(1.1.11)을 유지, 무인 Windows CI와 실제 게임 사용 검증은 분리.

# AltTabFlow 개발·운영 이력 (2026-11-11)

> 최신 결과: [1.1.12 PR #10 병합](https://github.com/hossea123456789-design/AltTabFlow/pull/10), [324/324 Windows CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42850336840), [동일 Public 저장소 자동 릴리스 v1.1.12 게시](https://github.com/hossea123456789-design/AltTabFlow/releases/tag/v1.1.12). 실제 MapleStory/UAC/인앱 EXE 교체 미검증.

이 기록은 **대화 요약이 아니라 GitHub에 남기는 장기 추적 로그**입니다. 실제 검증과 첨부 문서에 기록된 검증을 혼동하지 않습니다. 소스의 진실 공급원은 항상 해당 시점의 Git commit이며, 요구사항의 진실 공급원은 버전별 원문 명세입니다.

## 버전별 타임라인

| 버전 | 요청/구현의 핵심 | 테스트 수 (중복 제외, 명세 기록) | GitHub에서 별도 검증한 범위 |
|---|---|---:|---|
| **1.1.11** | 메이플 Alt+Enter 확장 후 TopMost 전환기가 가려지고 포커스가 게임에 잔류하는 회귀: 전환기 Z-order 재주장, 기원 게임이 포커스를 되잡더라도 즉시 해제 금지, Peek 복구를 실제 포커스 확인 뒤로 지연, 850/900ms 제한 시간. 소스는 비공개 유지, 공개 바이너리 저장소의 Releases를 인증 없이 자동 조회/무결성 검증 | 입력206+UI85+Updater22+Startup11=CI 목표324 | [PR #9 병합](https://github.com/hossea123456789-design/AltTabFlow/pull/9), [CI #42842547985 324/324 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42842347985). 실제 게임/독점 전체화면/공개 저장소 초기 설정·온라인 자동 갱신은 미완료 |
| **1.1.10** | 관리자 자동 시작을 Run 키에서 Task Scheduler Interactive/Highest로 전환, Program Files 보호 설치; 업데이트 창 사라짐·ESC 복구 보고에 대응해 독립 화면/취소 UI; 휠 강제 persistent 버그 제거 | 기본206+UI80+Updater11+Startup11=CI 목표308; Peek/Menu/Focus 추가25 | [PR #8](https://github.com/hossea123456789-design/AltTabFlow/pull/8), [Windows CI 308개 + 3종 실제 UI 캡처 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112), [main 병합](https://github.com/hossea123456789-design/AltTabFlow/commit/0841c00da08fa5287e971197894dfd2987b4ac14). UAC·로그인·게임·온라인 업데이트 실기기 검증은 별도 |
| 1.1.1 | 원본 완전 명세와 복원 스크립트, WinForms 25파일, MRU·Peek·테마·게임 호환 | 기본 72 + Peek 10 | Linux 작업 환경에서 소스·SHA 검증. Windows EXE 생성/실동작 재검증은 하지 않음 |
| 1.1.2 | ContextMenuStrip Closed/Dispose 생명주기 오류 수정, 초기화/런타임 오류 분리, 메뉴 7개 검사 | 89 (72+10+7) | 소스/패치 복원 후 비공개 GitHub 저장소 최초 등록. 초기 Windows CI 컴파일+실제 UI screenshot 성공 |
| 1.1.3 | 전경 전환 회귀 발생, 1.1.4에서 해결하도록 갱신 명세가 지시 | 별도 최종판 아님 | 이 저장소의 정식 안정 버전으로 취급하지 않음 |
| 1.1.4 | P4 소유 팝업·실제 클릭 창 기준 MRU, 전경 활성화 확인, QuickGesture/FX 최적화, Switching.cs 추가 | 112 (기본87+Peek10+메뉴7+Focus18−중복10) | [PR #2](https://github.com/hossea123456789-design/AltTabFlow/pull/2), [Windows 성공 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42764686888), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/b2e5aa57d031d7201d40788fa6bcf7db290509a1) |
| 1.1.5 | 일반 Alt+Tab 방향키 탐색 시 자동 고정 제거, 마지막 key-up 커밋·Esc 취소 | 142 (117+10+7+18−10) | [PR #3](https://github.com/hossea123456789-design/AltTabFlow/pull/3), [Windows CI 기본117](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42766936582), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/c26cc1237c1920d374487a91f154c4c8284f2694) |
| 1.1.6 | 설정 좌측 메뉴/우측 카드/하단 고정 저장, 7종 테마, owner-draw UI, mouse-up 커밋, UI 62개 | 204 (117+10+7+18+62−10) | [PR #4](https://github.com/hossea123456789-design/AltTabFlow/pull/4), [Windows CI 기본117+UI62](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42768914976), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/93fb3164a1945016926526730b791ffb1f6f4140) |
| **1.1.9** | 게임 권한 mismatch/asInvoker, ZIP 내 EXE와 게임 스크립트가 서로 다른 폴더, 일반 권한 단일 인스턴스 문제 개선. UAC 후 관리자 인계 및 비공개 GitHub Actions 기반 자동 업데이트·자격 증명 보호/검증·업데이터 자체 검사 11개 | 입력·UI 기존 고유 302 + 별도 Updater 11 | [PR #7](https://github.com/hossea123456789-design/AltTabFlow/pull/7), [CI 206+71+11 및 동일 폴더 배포 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42812687675), [main 병합](https://github.com/hossea123456789-design/AltTabFlow/commit/51175f30dd9c0e0aa8792593e3956703f7f24e8d). 물리 게임/UAC/다운로드·교체 종단 간 검증 별도 |
| **1.1.8** | 일반 Alt+Tab 화면 잔류: 오래된 해제 누락 키 기록 정리, 세션 중 16ms WM_TIMER로 마지막 키 해제 보조 복구, 기존 고정 화면 위 일반 전환 재시작 시 상태 초기화 | **302 (206+71+10+7+18−10)** | [PR #6](https://github.com/hossea123456789-design/AltTabFlow/pull/6), [Windows CI 무결성/컴파일/206+71 통과](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910), [main 병합](https://github.com/hossea123456789-design/AltTabFlow/commit/8fca8193761bca80812b20c188350d7e4165f34f). 제공 문서에 일반/고정 실사용 확인 기록, 물리 게임·P4는 별도 |
| **1.1.7** | 숨길 앱 Enter 줄바꿈, Ctrl+Alt+Tab 고정·반복·늦은 Ctrl 승격, 상단 등록 앱 아이콘·폴백·비동기 로딩 | **257 (161+10+7+18+71−10)** | [PR #5](https://github.com/hossea123456789-design/AltTabFlow/pull/5), [CI 기본161+UI71 및 체크섬·컴파일 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42777258155), [main 병합](https://github.com/hossea123456789-design/AltTabFlow/commit/da05572fe9bbd23fcd8bccfdd677805cdfb87582). 사용자 Windows PC 전경·게임 입력 검증은 별도 |

## GitHub 전환과 관리 결정

- 비공개 저장소 `hossea123456789-design/AltTabFlow`를 초기화했습니다.
- 초기 원본과 1.1.2 변경을 Git으로 등록하고, GitHub Actions Windows 빌드·테스트 후보 EXE 업로드, 메인/설정 화면 캡처 자동화 및 Issues/PR 템플릿을 추가했습니다.
- 이후 요청은 GitHub 별도 브랜치 → PR → CI → `main` 병합 흐름으로 관리. CI 컴파일과 실사용 회귀 테스트는 구별했습니다.
- [Issue #1](https://github.com/hossea123456789-design/AltTabFlow/issues/1)은 **버전마다 갱신하는 실사용 검증의 단일 체크리스트**입니다. 검증 미완료 상태에서 정식 GitHub Release를 만들지 않습니다.
- 버전별 원문 명세를 `docs/` 아래에 보관. 1.1.1 완전 원문은 `docs/1.1.1-완전명세-아카이브.md`, 후속 패치는 `docs/1.1.2-변경명세.md`, `docs/1.1.4-변경명세.md`, `docs/1.1.5-변경명세.md`, `docs/1.1.6-UI개선-변경명세.md`, `docs/specs/AltTabFlow_1.1.7_갱신내용.md`, `docs/specs/AltTabFlow_1.1.8_갱신내용.md`입니다.

## 추적해야 할 중요한 회귀/설계 결정

1. **우클릭 메뉴**: 반복 close/open 중 `ContextMenuStrip`을 `Closed`에서 Dispose 하면 충돌. 메뉴를 컨트롤 소유로 재사용.
2. **원래 창의 Z-order**: 전환 대상 Peek는 진짜 HWND를 다루며 Escape/취소 시 원래 TopMost 등 상태를 복원해야 함.
3. **P4/메이플스토리**: HWND·소유 관계·권한과 실제 `GetForegroundWindow` 확인이 중요. 요청만 성공했다고 전환 성공 처리하지 않음.
4. **키보드 단축키**: 일반 마지막 키 해제 커밋과 고정 Ctrl+Alt+Tab 제스처는 독립 상태. 누르는 순서·좌우 수정키·Esc 취소를 매번 회귀 테스트.
5. **설정 저장**: 미리보기로 즉시 `settings.json`을 변경하거나 자동 실행 Registry 상태를 바꾸면 안 됨. 숨길 앱 입력 Enter는 **줄바꿈**, 수동 앱 등록 입력 Enter는 **등록**.
6. **앱 아이콘**: 비동기 아이콘 추출 중 UI를 막지 않으며, 종료된 폼에 BeginInvoke/Invalidate 하지 않음. 체크 선택의 객체 참조 동일성 유지.
7. **Windows UI**: 1.1.6 이후 도입된 owner-draw/GDI 테마와 사용자 키보드 접근성 유지. 96 DPI 검증과 혼합 DPI 실측은 구분.
8. **EXE 이력**: 명세에 언급된 배포 ZIP/EXE 해시는 타 환경의 빌드 지문. GitHub CI로 재빌드한 파일과 당연히 일치하지 않을 수 있음.

## 1.1.8 문제 진단 및 재발 방지

- 빠른 전환 종료 누락 시 Ctrl 오인식만 가정하지 말 것. 최근 라우터 명령에 `Next` 반복 + `Commit` 부재, `persistent=false`를 근거로 **남은 눌린 키의 상태**부터 확인.
- 일반 키 `knownDown`의 **과거 기록**은 물리 상태와 대조해 정리하되, **훅에서 소비한 키**의 비동기 상태가 0이라는 이유만으로 임의 해제 금지.
- `KeyboardHook`은 16ms 확인 타이머를 **입력 세션에서만** 설치/해제. 실제 키가 모두 놓인 경우 일반 세션 Commit은 정확히 한 번, 고정 세션 Commit은 발행하지 않음.
- 이전 고정 UI가 보이더라도 새 일반 Alt+Tab의 인자로 `persistent=false`로 전환.
- 명세는 **자동 입력 테스트 도중 마지막 Alt-up이 전달되지 않던 현상**을 관찰했으나 사용자 물리 키 문제와 같은 원인이라 단정하지 않음. 관리자 메이플스토리·자동화 도구·권한 조건을 결과에 남김.
- 명세의 로컬 배포 ZIP SHA-256: `7BC446AC0BB43F26EE3E2354DBDC023F34AAC8027C2BBF07C327FFC43CA5650C`; EXE SHA-256: `6ABD2FA306B0106F3D1ED6296A0E0276CD5BE039380CD761D9C0C856F0021F0C`. **실제 바이너리는 이 대화에 업로드되지 않았고 GitHub CI에서 빌드한 후보와 지문이 다를 수 있음.**
- 다음 세션에서 `docs/specs/AltTabFlow_1.1.8_갱신내용.md`의 실제 6파일 패치와 테스트를 참조.

## 남은 실제 검증

- 첨부 1.1.7 문서에는 일반 권한에서 관리자 메이플스토리가 foreground를 차지하여 전경 회귀 검사가 실패한 기록이 있습니다. 관리자 권한 **전체 재검사**가 성공했다는 기록은 있지만, 사용자 PC에서 동일 재현을 완료한 것은 아닙니다.
- 실제 **P4 메인/소유 팝업**, 관리자 게임에서 Alt+Tab/Ctrl+Alt+Tab 물리 키 반복, 다중 모니터·DPI, Peek 취소 복원을 Windows 실기기에서 확인해야 합니다.
- 검증 결과를 기록할 때 “인계 명세의 통과 이력 / GitHub CI 재실행 / 사용자 실기기 재실행”을 각각 분리해야 합니다.
- 성능 p50/p95/max, 비정상 종료 시 TopMost 복원, .NET 8 지원 종료 후 업그레이드 계획 등은 [ROADMAP](ROADMAP.md)의 후속 후보입니다.

## 새 세션 작업자에게

빠른 시작은 [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md). 구현 변경은 [ARCHITECTURE_AND_INVARIANTS.md](ARCHITECTURE_AND_INVARIANTS.md), 테스트는 [TEST_MATRIX.md](TEST_MATRIX.md). 명세와 현재 소스의 충돌이 생기면 최신 사용자 요청 → 최신 version patch → Git commit/실제 테스트 로그 순으로 분리하여 근거를 남깁니다. ChatGPT 대화 메시지를 유일한 변경 근거로 쓰지 마세요.
