# 테스트·배포 매트릭스 (v1.1.10)

## 검사 수

| 검사 | CLI | 1.1.10 기대 수 | CI 자동화 | 실제 Windows 사용자 세션 |
|---|---|---:|---|---|
| 기본 키 입력·그룹·고정 세션 | `--self-test` | **206** | ✅ [PR #8 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112) 206/206 성공 | 별도 재검증 |
| UI·줄바꿈·아이콘·접근성·휠/업데이트 창 | `--ui-self-test` | **80** | ✅ [PR #8 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112) 80/80 성공 | 별도 재검증 |
| 실제 창 미리 표시/취소 | `--peek-self-test` | **10** | 무인 CI에서 보장 불가 | 필요 |
| 우클릭 메뉴 생명주기 | `--menu-self-test` | **7** | 무인 CI에서 보장 불가 | 필요 |
| 전경 권한·지연된 대상 | `--focus-self-test` | **18** | 무인 CI에서 보장 불가 | 필요; Peek 10개와 중복 |
| **원본 창전환 고유 302 + 신규 UI 9** | | **311** | 기본+UI=286개 CI 통과 | 나머지 고유 25개 및 물리 입력 필요 |

위 302개는 **업로드된 1.1.8 명세가 보고한 결과**이며 CI가 자동으로 302개를 재실행했다고 주장하지 않습니다. 정확한 통과 여부는 각 실행의 JSON `failures`, `tests[].passed`, 종료 코드와 Action 결론을 확인하세요.

## 1.1.10 신규 검사 및 실기기 검증

- UI 신규 **9개**(휠 일반/고정·마지막 키 해제 5, 독립 업데이트 창/토큰 마스킹/닫기 4), 기존 71→**80**
- `--startup-self-test` **11개**: 작업 이름 사용자별 분리, Run/Task 상호 배제, 보호된 Program Files 설치 경로, 백그라운드 기동 계획
- 기존 `--self-test` 206, `--updater-self-test` 11 유지. **자동 총합 = 206 + 80 + 11 + 11 = 308개**
- [ ] 설정 두 체크 후 UAC 승인 → 작업 스케줄러에 Interactive/Highest 작업 생성 → 일반 Run 키 제거 → 로그아웃/로그인 후 관리자 PID 확인
- [ ] UAC 거부 시 작업·설정 유지, 해제 시 스케줄러 작업 삭제·일반 Run 복원
- [ ] 관리자 예약 작업이 Downloads·AppData 실행 파일이 아닌 **Program Files 보호 폴더** 실행하는지 확인
- [ ] 업데이트 버튼 클릭 → **계속 보이는 별도 창**, 토큰 입력/오류/취소가 ESC 없이 정상 표시
- [ ] 저장된 PAT로 비공개 GitHub 실제 다운로드/교체·재시작 및 UAC/메이플스토리 실사용 확인
- [ ] 휠로 2~3개 선택 이동 후 Alt/Tab 마지막 해제 시 그 선택으로 전환, 핀 모드는 해제 후 유지

## 1.1.9 신규 업데이트·게임 호환 검증

- `--updater-self-test`: 버전 파싱, HTTPS URL/리디렉션 검사, ZIP 안전 추출 및 게임 스크립트 동일 폴더 여부 **11개** [PR #7 CI 성공](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42812687675). 기존 기본206/ UI71 검사에 추가하며 원래 고유 302개의 입력/전경 검사 합계와 혼동 금지.
- [ ] 일반 권한 프로그램 실행 → 설정/트레이 `게임 호환 관리자 권한으로 재시작` → **UAC 승인 후 기존 인스턴스 종료**와 새 관리자 프로세스 인계.
- [ ] UAC 취소 시 기존 일반 권한 프로그램과 설정 유지.
- [ ] 관리자 메이플스토리에서 실제 키 입력 Alt+Tab/반복/복귀 확인.
- [ ] 비공개 GitHub 최초 연결시 최소 읽기 토큰 권한과 Windows 자격 증명 저장, 이후 재입력 불필요.
- [ ] 최신 성공 `main` 버전 동일 시 갱신 불필요 메시지, 새 버전 시 자동 다운로드·교체·재시작.
- [ ] 업데이트 실패/파일 쓰기 거부/중단·고장난 ZIP 시 기존 EXE와 settings.json 보존, 백업 유지.
- [ ] `AltTabFlow.exe` + 게임 호환 스크립트 같은 디렉터리 패키징.

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

## 1.1.9 GitHub 자동 검사 결과

[PR #7 Windows CI #42812687675](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42812687675): 기본 206/206, UI 71/71, 신규 업데이터 11/11 = 총 288개 자동 검사 통과. 실행 파일 생성, SHA-256 매니페스트 검증, 실제 Windows 화면 캡처, EXE+게임 호환 스크립트 동일 폴더 ZIP까지 성공. **P4·메이플스토리 실기기 / UAC 승인·취소 / GitHub 토큰을 사용한 업데이트 종단 간 검사는 이 CI로 검증되지 않았습니다.**

## 1.1.10 Windows CI / 릴리스 후보 증거

[PR #8 빌드 #42832500112](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42832500112): SHA-256 매니페스트 일치, .NET 8 Windows 컴파일/self-contained EXE, 기본206/206 + UI80/80 + updater11/11 + startup11/11 = **308/308개** 통과. WinForms **메인·설정·독립 업데이트 창** 실제 화면 캡처, EXE·게임 CMD/PS1 동일 폴더 ZIP 업로드 성공.

**실사용 PC 별도:** 예약 작업 등록/UAC/로그오프 후 로그인 최고 권한 확인, 메이플스토리 후킹/전경, GitHub 읽기 토큰을 통한 실제 다운로드·EXE 교체/롤백, Peek/Menu/Focus 25개 고유 회귀는 이번 무인 CI의 검증 범위가 아님.
