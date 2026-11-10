# AltTabFlow

**세션 간 후속 작업 시작점: [PROJECT_HANDOFF.md](docs/PROJECT_HANDOFF.md)** · [전체 개발 이력](docs/DEVELOPMENT_HISTORY.md) · [동작 불변 조건](docs/ARCHITECTURE_AND_INVARIANTS.md) · [검증 매트릭스](docs/TEST_MATRIX.md)

Windows 10/11 x64용 Alt+Tab 대체 창 전환기입니다. 현재 소스 버전은 **1.1.9**입니다.

> **검증 분리:** 첨부된 1.1.8 인계 명세는 기본 206 + UI 71 + Peek 10 + 메뉴 7 + Focus 18(이미 포함된 Peek 10 중복) = **고유 302개 검사 통과**를 보고합니다. GitHub CI에서는 기본 206개+UI 71개를 별도 검증하며, 실제 전경·메뉴·게임 회귀 검사는 구분합니다.

## 주요 기능

- 앱별 묶음 목록, 카드, 아이콘 전환 (Ctrl+1/2/3)
- MRU 기반 직전 프로그램 선택과 Alt 유지 중 Tab·방향키 탐색, 마지막 키 해제 시 전환
- 실제 대상 창을 일시적으로 앞으로 보여 주는 Peek, 순환 아우라와 주변 딤드
- 주 모니터 중심 배치, 다중 모니터 대상 선택, 7개 테마
- 앱 상단 등록, 숨기기, 검색, 로그인 자동 시작 설정 저장
- 메이플스토리처럼 관리자 권한으로 실행된 앱을 위한 `Game-compat-mode.cmd`
- 1.1.2: 우클릭 메뉴 수명 관리 오류 수정, `--menu-self-test` 추가
- 1.1.9: 게임 호환 관리자 권한으로 안전하게 재시작(UAC/기존 인스턴스 인계), EXE에서 GitHub 업데이트 확인·자동 다운로드·백업 후 교체·재시작
- 1.1.8: 일반 Alt+Tab 종료 누락 복구 — 오래된 키 눌림 기록 정리, 세션 중 누락된 key-up 확인, 일반 재시작 시 이전 고정 모드 초기화
- 1.1.7: 숨길 앱 Enter 줄바꿈, Ctrl+Alt+Tab 고정 화면 유지·반복/역방향/늦은 Ctrl 승격, 상단 정렬 앱 아이콘 및 비동기 로딩
- 1.1.6: 설정 화면을 왼쪽 메뉴/오른쪽 카드/하단 고정 저장으로 전면 개편. 테마형 버튼·체크·선택 필드·우클릭 메뉴·제목 표시줄 적용, 마우스 up 확정 및 이탈 취소
- 1.1.5: Alt+Tab 이후 방향키로 이동해도 고정하지 않으며 누른 키를 모두 놓았을 때 현재 하이라이트 창으로 확정. Esc는 취소하고 명시적 상세 모드는 유지
- 1.1.4: 실제 클릭한 창·P4 소유 팝업 MRU 왕복, 전경 전환 완료 확인, 비동기 활성화 및 입력 최적화

## 실행 / 게임 호환 / 자동 업데이트

**메이플스토리처럼 관리자 권한으로 실행한 게임에서는 AltTabFlow도 관리자 권한이 필요할 수 있습니다.** 기존 일반 실행본이 남아 있으면 새 관리자 실행본이 무시되는 현상을 수정했습니다.

1. 오른쪽 아래 트레이의 AltTabFlow 아이콘을 우클릭해 **게임 호환: 관리자 권한으로 다시 시작**을 누르거나, 설정의 **기본 동작** 아래쪽에서 같은 버튼을 누릅니다.
2. Windows UAC 요청에 동의하면 기존 프로그램이 정상 종료되고 관리자 모드가 실행됩니다. 취소하면 원래 실행본을 유지합니다.
3. 프로그램이 여전히 Windows Alt+Tab을 표시하면 설정의 **Alt+Tab 전환 사용**, 일시 중지 상태 및 관리자 여부를 확인합니다. 게임·후킹 실기기 검사는 별도 진행해야 합니다.

**자동 업데이트:** 트레이의 **업데이트 확인** 또는 설정의 **업데이트 확인 · 자동 설치**를 누릅니다. GitHub 저장소가 비공개이므로 **최초 1회** `hossea123456789-design/AltTabFlow`에 한정된 Fine-grained PAT(Contents: Read-only, Actions: Read-only)를 입력해야 합니다. 인증 정보는 Windows **자격 증명 관리자**에만 저장하고 `settings.json`이나 GitHub에 기록하지 않습니다. 이후 최신 성공한 `main` Windows Actions 빌드를 확인하고, 새 버전이면 파일을 내려받아 검증하고 자동 교체 후 재시작합니다. ZIP 직접 풀기·개별 파일 복사는 필요하지 않습니다.

**주의:** 업데이트 대상은 실사용 검증 완료 정식 릴리스가 아니라 CI를 통과한 **실행 후보**입니다. 관리자 게임 검증을 의미하지 않습니다. 기존 **1.1.8에서 1.1.9로 바꾸는 첫 업데이트 한 번은** 기능을 탑재한 1.1.9 실행 파일을 내려받아야 하며, 이후 버전부터 버튼으로 갱신할 수 있습니다. 기존 설치 경로가 쓰기 제한된 폴더이면 UAC가 추가로 필요할 수 있습니다.

## 실행·빌드

Windows에서 `Windows-Build.cmd`를 실행합니다. .NET 8 SDK가 없으면 설치 여부를 묻고, 소스에서 `package.ps1`을 호출합니다. 성공하면 `releases/AltTabFlow-latest-win-x64.zip`을 생성합니다. 완성 EXE를 이 저장소의 소스 코드와 혼동하지 마세요.

기존 SDK로 직접 빌드하려면:

```powershell
dotnet restore .\AltTabFlow.csproj
dotnet build .\AltTabFlow.csproj -c Release
```

검증은 **로그인한 Windows 사용자 세션에서 잠금을 해제한 상태**로 실행합니다.

```powershell
$exe = '.in\Release
et8.0-windows\AltTabFlow.exe'
& $exe --self-test
& $exe --peek-self-test
& $exe --menu-self-test
& $exe --focus-self-test
& $exe --ui-self-test
```

다섯 검사 모두 종료 코드 0 및 `%LOCALAPPDATA%\AltTabFlow\*-test.json`의 `failures=0`을 확인해야 합니다. `--peek-self-test`는 테스트 창을 표시할 수 있습니다. 자동 배포용 `package.ps1`은 검사 실패 시 최신 배포 ZIP을 갱신하지 않습니다.

## GitHub 관리 원칙

1. 버그 수정 및 기능 제안은 **Issues**로 기록합니다.
2. 변경 코드는 별도 브랜치에서 수정하고 **Pull Request**로 `main`에 병합합니다.
3. GitHub Actions는 **컴파일/게시 가능 여부만 확인**합니다. 실제 창, 메뉴, 게임 모드는 로컬 Windows에서 별도 검증해야 합니다.
4. 302개 고유 테스트의 검증 결과를 확보한 후에만 `v1.1.x` 태그 및 **Releases**에 완성 ZIP을 등록합니다.
5. 상태 파일이나 창 제목이 포함된 로그는 저장소에 커밋하지 않습니다. 오류 보고 시에도 민감한 창 제목과 경로를 삭제합니다.

자세한 변경 이력은 [CHANGELOG.md](CHANGELOG.md), 인수 점검은 [docs/ROADMAP.md](docs/ROADMAP.md), 1.1.8 변경 근거는 [docs/specs/AltTabFlow_1.1.8_갱신내용.md](docs/specs/AltTabFlow_1.1.8_갱신내용.md)를 확인합니다.

**배포 주의:** CI에서 생성한 후보 EXE는 실사용 창 제어 검사 전까지 검증 완료 릴리스로 배포하지 않습니다. 이 프로그램은 Windows 시스템 파일을 수정하지 않으며 정상 종료하면 원래 Alt+Tab이 돌아옵니다.