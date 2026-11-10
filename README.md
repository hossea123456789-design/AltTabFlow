# AltTabFlow

Windows 10/11 x64용 Alt+Tab 대체 창 전환기입니다. 현재 소스 버전은 **1.1.5**입니다.

> **검증 상태:** 1.1.5 갱신 명세에는 기본 입력·Form 연동 117개, 실제 창 10개, 메뉴 7개, 전경·지연 대상 18개(실제 창 10개 중복)를 합쳐 **고유 142개 검사 통과 이력**이 있습니다. 이는 명세 작성자의 Windows 환경 기록이며 GitHub Actions의 빌드 및 사용자 PC의 실기기 테스트와 구분합니다.

## 주요 기능

- 앱별 묶음 목록, 카드, 아이콘 전환 (Ctrl+1/2/3)
- MRU 기반 직전 프로그램 선택과 Alt 유지 중 Tab·방향키 탐색, 마지막 키 해제 시 전환
- 실제 대상 창을 일시적으로 앞으로 보여 주는 Peek, 순환 아우라와 주변 딤드
- 주 모니터 중심 배치, 다중 모니터 대상 선택, 7개 테마
- 앱 상단 등록, 숨기기, 검색, 로그인 자동 시작 설정 저장
- 메이플스토리처럼 관리자 권한으로 실행된 앱을 위한 `Game-compat-mode.cmd`
- 1.1.2: 우클릭 메뉴 수명 관리 오류 수정, `--menu-self-test` 추가
- 1.1.5: Alt+Tab 이후 방향키로 이동해도 고정하지 않으며 누른 키를 모두 놓았을 때 현재 하이라이트 창으로 확정. Esc는 취소하고 명시적 상세 모드는 유지
- 1.1.4: 실제 클릭한 창·P4 소유 팝업 MRU 왕복, 전경 전환 완료 확인, 비동기 활성화 및 입력 최적화

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
```

네 검사 모두 종료 코드 0 및 `%LOCALAPPDATA%\AltTabFlow\*-test.json`의 `failures=0`을 확인해야 합니다. `--peek-self-test`는 테스트 창을 표시할 수 있습니다. 자동 배포용 `package.ps1`은 검사 실패 시 최신 배포 ZIP을 갱신하지 않습니다.

## GitHub 관리 원칙

1. 버그 수정 및 기능 제안은 **Issues**로 기록합니다.
2. 변경 코드는 별도 브랜치에서 수정하고 **Pull Request**로 `main`에 병합합니다.
3. GitHub Actions는 **컴파일/게시 가능 여부만 확인**합니다. 실제 창, 메뉴, 게임 모드는 로컬 Windows에서 별도 검증해야 합니다.
4. 142개 고유 테스트의 검증 결과를 확보한 후에만 `v1.1.x` 태그 및 **Releases**에 완성 ZIP을 등록합니다.
5. 상태 파일이나 창 제목이 포함된 로그는 저장소에 커밋하지 않습니다. 오류 보고 시에도 민감한 창 제목과 경로를 삭제합니다.

자세한 변경 이력은 [CHANGELOG.md](CHANGELOG.md), 인수 점검은 [docs/ROADMAP.md](docs/ROADMAP.md), 1.1.5 변경 근거는 [docs/1.1.5-변경명세.md](docs/1.1.5-변경명세.md)를 확인합니다.

**배포 주의:** CI에서 생성한 후보 EXE는 실사용 창 제어 검사 전까지 검증 완료 릴리스로 배포하지 않습니다. 이 프로그램은 Windows 시스템 파일을 수정하지 않으며 정상 종료하면 원래 Alt+Tab이 돌아옵니다.