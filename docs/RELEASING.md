# 릴리스 절차

main 브랜치의 소스와 CI 컴파일 성공만으로 Windows 창 제어가 검증됐다고 보지 않습니다.

1. 변경 사항을 Pull Request로 검토 후 main에 병합합니다.
2. 잠금 해제된 Windows PC에서 Windows-Build.cmd 또는 package.ps1 -RuntimeVersion 8.0.32를 실행합니다.
3. 기본 206개, UI 85개, 실제 창 미리 표시 10개, 메뉴 7개, 전경/지연 대상 18개(실제 창 10개 중복) — 새 UI 포함 고유 311개 창/메뉴/전경 검사에서 실패 없이 종료하는지 확인합니다.
4. 추가 공개 릴리스 updater 22개 및 관리자 시작 11개 및 GitHub 자격 증명/자동 업데이트 UAC·파일교체 실기기 검증 후, releases/latest.json의 testsPassed, nativeTestsPassed, menuTestsPassed, focusTestsPassed, uiTestsPassed, ZIP SHA-256을 확인합니다.
5. 실사용 Alt+Tab 왕복, 창 묶기, 취소 후 TopMost 복원, 게임용 관리자 모드를 수동 확인합니다.
6. 검증 완료 시 해당 커밋에 버전 태그를 만들고, 성공한 배포 ZIP을 GitHub Release에 첨부합니다.

~~~powershell
git tag -a v1.1.10 -m 'AltTabFlow v1.1.10: Windows regression verified'
git push origin v1.1.10
gh release create v1.1.10 '.\releases\AltTabFlow-latest-win-x64.zip' --title 'AltTabFlow 1.1.10' --notes-file CHANGELOG.md --verify-tag
~~~

실기기 검증 전에는 태그와 완성 릴리스 배포를 하지 않습니다. 개인 설정, 창 목록 및 진단 로그는 저장소와 릴리스에 포함하지 않습니다.

## 공개 배포 저장소 일회성 설정 (1.1.11 이후)

1. 소스 저장소 `hossea123456789-design/AltTabFlow`는 **Private 유지**.
2. GitHub 계정에서 `AltTabFlow-Downloads` 이름의 **Public** 새 저장소 생성(공개 ZIP만 게시; 원본 소스 복사/공개 금지).
3. Fine-grained Personal Access Token을 **오직 공개 `AltTabFlow-Downloads` 저장소** 대상으로 `Contents: Read and write` 권한으로 만들고, 비공개 원본 저장소 Settings → Secrets and variables → Actions → New repository secret의 `ALTTABFLOW_PUBLIC_RELEASE_TOKEN`에 등록. 토큰은 앱 사용자에게 요구하지 않으며 채팅/Git 이슈에 붙이지 않음.
4. 비공개 Windows CI `windows-build.yml` 성공 후 `publish-public-binaries.yml` 배포 작업 실행 결과 확인. 공개 릴리스 `v1.1.11` 첨부 파일이 **`AltTabFlow-win-x64.zip` + `SHA256SUMS.txt`만**인지 검증.
5. 공개 GitHub API의 Release Asset `sha256:` digest, Windows ZIP의 exe 버전, 실제 로그인 없는 앱 업데이트 설치를 별도 Windows PC에서 확인. 파일 서명 및 실사용 게임/관리자 예약 시작 검증 전까지 **Preview**임을 표시.

공개 저장소나 배포 Secret이 아직 준비되지 않으면 배포 Actions는 경고와 함께 **게시하지 않음**으로 기록하며, 이를 공개 업데이트 성공으로 보고하지 않습니다.
