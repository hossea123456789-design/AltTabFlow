# 테스트·배포 매트릭스 (v1.1.8)

## 검사 수

| 검사 | CLI | 1.1.8 기대 수 | CI 자동화 | 실제 Windows 사용자 세션 |
|---|---|---:|---|---|
| 기본 키 입력·그룹·고정 세션 | `--self-test` | **206** | ✅ [PR #6 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910) 206/206 성공 | 별도 재검증 |
| UI·줄바꿈·아이콘·접근성 | `--ui-self-test` | **71** | ✅ [PR #6 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910) 71/71 성공 | 별도 재검증 |
| 실제 창 미리 표시/취소 | `--peek-self-test` | **10** | 무인 CI에서 보장 불가 | 필요 |
| 우클릭 메뉴 생명주기 | `--menu-self-test` | **7** | 무인 CI에서 보장 불가 | 필요 |
| 전경 권한·지연된 대상 | `--focus-self-test` | **18** | 무인 CI에서 보장 불가 | 필요; Peek 10개와 중복 |
| **고유 합계** | | **302** | 기본+UI=277개 이번 CI 통과 | 나머지 고유 25개 및 물리 입력 필요 |

위 302개는 **업로드된 1.1.8 명세가 보고한 결과**이며 CI가 자동으로 302개를 재실행했다고 주장하지 않습니다. 정확한 통과 여부는 각 실행의 JSON `failures`, `tests[].passed`, 종료 코드와 Action 결론을 확인하세요.

## 실제 장치 점검 시나리오

- 일반 Alt+Tab: Alt→화살표→Tab 등 키 해제 순서 6종 이상, 마지막 key-up에서 한 번만 Commit. **과거 key-up 누락 뒤 재진입**, 실제 키 유지, Alt-up 누락 복구, 중복 커밋 방지 검사 포함.
- Ctrl+Alt+Tab: 눌린 키를 모두 놓고도 보이는지, 반복 다음, Shift 역방향, 늦은 Ctrl Pin, Ctrl 먼저 release. **고정 화면 위에서 새 일반 Alt+Tab → 마지막 해제 시 정상 종료** 확인.
- Chrome 그룹 펼치기·대표/개별 전환, Esc 취소 후 무효 key-up, 명시적 고정/검색/설정 유지.
- 실제 P4 메인 HWND와 소유 팝업 전경 관계, 실제 마우스 클릭 MRU 왕복.
- 일반 권한 vs 관리자 게임/메이플스토리 전경 활성화. 일치하지 않으면 검증 실패로 표시하고 현재 foreground/권한 맥락 수집.
- 숨길 앱 Enter 줄바꿈과 여러 줄 저장. 취소 후 설정 파일 동일.
- 앱 아이콘 캐시·App Paths·없는 앱 글자 배지·비동기 취소, 체크된 앱 지속성.
- 제목 표시줄/우클릭 메뉴·스크롤/저장 버튼·7종 테마와 다중 DPI/모니터·화면 크기.
- ZIP 추출 뒤 다른 폴더에서 실행, 기본 자동 시작 비활성 선택 유지, settings.json 덮어쓰기 금지.

## 빌드/검증 실행

```powershell
# repo root, Windows 10/11 x64
dotnet restore .\AltTabFlow.csproj --runtime win-x64 -p:PublishReadyToRun=true
dotnet build .\AltTabFlow.csproj -c Release
# EXE 경로는 actual build configuration에 맞춰 지정
& .\bin\Release\net8.0-windows\AltTabFlow.exe --self-test
& .\bin\Release\net8.0-windows\AltTabFlow.exe --ui-self-test
& .\bin\Release\net8.0-windows\AltTabFlow.exe --menu-self-test
& .\bin\Release\net8.0-windows\AltTabFlow.exe --peek-self-test
& .\bin\Release\net8.0-windows\AltTabFlow.exe --focus-self-test
```

`package.ps1`은 이 검사들을 포함한 검증형 패키징입니다. Windows 잠금 화면, foreground 관리 권한 및 관리자 게임이 회귀 검사에 영향을 줄 수 있습니다. 실패를 지나치지 않고 **원인/조건/재시험 결과**까지 기록합니다.

## 자동 CI의 한계

- **1.1.8 PR CI 확인 완료:** [Windows CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42792830910)에서 SHA-256 manifest, .NET 컴파일, self-contained EXE, 기본 206/206, UI 71/71, 실제 메인·설정 WinForms 화면 캡처 성공.
- CI 스크린샷은 실제 WinForms 화면이지만 메이플스토리·P4 프로그램이 설치되어 있다는 뜻이 아님.
- 302개 전체 실기기 확인 전에는 CI 후보 ZIP을 정식 사용자 검증 릴리스로 승격하지 않음.
- 빌드 아티팩트는 만료될 수 있음. 장기 추적의 진실 공급원은 **Git commit + PR + 테스트 체크리스트**. 검증 완료된 릴리스만 GitHub Release에 등록.
