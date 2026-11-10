# 릴리스 절차

main 브랜치의 소스와 CI 컴파일 성공만으로 Windows 창 제어가 검증됐다고 보지 않습니다.

1. 변경 사항을 Pull Request로 검토 후 main에 병합합니다.
2. 잠금 해제된 Windows PC에서 Windows-Build.cmd 또는 package.ps1 -RuntimeVersion 8.0.32를 실행합니다.
3. 기본 206개, UI 96개, 실제 창 미리 표시 10개, 메뉴 7개, 전경/지연 대상 18개(실제 창 10개 중복) — 새 UI 포함 고유 311개 창/메뉴/전경 검사에서 실패 없이 종료하는지 확인합니다.
4. 추가 공개 릴리스 updater 22개 및 관리자 시작 11개 및 GitHub 자격 증명/자동 업데이트 UAC·파일교체 실기기 검증 후, releases/latest.json의 testsPassed, nativeTestsPassed, menuTestsPassed, focusTestsPassed, uiTestsPassed, ZIP SHA-256을 확인합니다.
5. 실사용 Alt+Tab 왕복, 창 묶기, 취소 후 TopMost 복원, 게임용 관리자 모드를 수동 확인합니다.
6. 검증 완료 시 해당 커밋에 버전 태그를 만들고, 성공한 배포 ZIP을 GitHub Release에 첨부합니다.

~~~powershell
git tag -a v1.1.10 -m 'AltTabFlow v1.1.10: Windows regression verified'
git push origin v1.1.10
gh release create v1.1.10 '.\releases\AltTabFlow-latest-win-x64.zip' --title 'AltTabFlow 1.1.10' --notes-file CHANGELOG.md --verify-tag
~~~

실기기 검증 전에는 태그와 완성 릴리스 배포를 하지 않습니다. 개인 설정, 창 목록 및 진단 로그는 저장소와 릴리스에 포함하지 않습니다.

## 단일 공개 저장소 릴리스 (v1.1.12 이후)

1. 저장소 소유자가 기존 `hossea123456789-design/AltTabFlow`의 과거 커밋·Issues/Actions 로그를 검토하고 **Public**으로 전환합니다. 연결된 GitHub App에서는 Admin 권한이 없어 visibility를 변경할 수 없습니다.
2. 기존 소스 저장소 하나만 사용합니다. `AltTabFlow-Downloads` 생성이나 PAT 발급, 별도 Secret 등록은 **필요하지 않습니다**.
3. Private 상태에서는 `publish-public-binaries.yml`가 실제 릴리스를 게시하지 않도록 막습니다. Private → Public 전환 순간 GitHub `public` 이벤트로 릴리스 배포 작업이 자동 실행됩니다. 별도 Actions 수동 실행은 필요하지 않습니다(필요시 재시도용 workflow_dispatch 제공).
4. 워크플로가 `main`의 정확한 HEAD 커밋과 같은 성공 Windows CI를 선택하고 `AltTabFlow-win-x64.zip`, `SHA256SUMS.txt`만 릴리스 자산으로 올리는지 확인합니다. GitHub의 자동 Source code 링크는 저장소가 이미 Public이므로 별개입니다.
5. 앱에서는 GitHub 토큰 없이 동일 저장소 공개 Releases API의 최신 유효 버전, SHA-256 digest, ZIP 내 EXE 버전을 검사해 자동 교체·재시작합니다.
6. CI 검사는 입력206/UI85/업데이터22/관리자 시작11=324건. 실제 메이플 독점 전체화면, UAC, 파일 교체 종단간 및 공개 릴리스 조회는 별도로 확인하고 정식 검증 완료로 과장하지 않습니다.

## v1.1.13 메이플 전환기 실측

- PR CI에서는 WinForms 합성 창·정적 정책·DWM 캡처를 검증할 수 있으나 MapleStory 실제 Alt+Enter·DirectX 전체화면을 재현할 수 없습니다.
- 사용자 PC: 메이플을 Alt+Enter로 확장하고 Alt 유지+Tab으로 목록에서 MapleStory 항목을 지나간 뒤 Chrome/카카오톡으로 연속 2~5회 전환하며 전환기 노출을 확인. 실행할 창을 선택해 실제 foreground도 확인.
- 외부 Peek/Dim/Aura가 MapleStory를 건드리지 않는지, 일반 창에서는 정상 동작하는지, ESC와 마지막 키 해제 Commit 회귀를 확인.
- 제목·중복 라벨·버튼 위치는 실제 다크/라이트 테마와 UI Scale 85~140%에 걸쳐 시각 검증.
