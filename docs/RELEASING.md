# 릴리스 절차

main 브랜치의 소스와 CI 컴파일 성공만으로 Windows 창 제어가 검증됐다고 보지 않습니다.

1. 변경 사항을 Pull Request로 검토 후 main에 병합합니다.
2. 잠금 해제된 Windows PC에서 Windows-Build.cmd 또는 package.ps1 -RuntimeVersion 8.0.32를 실행합니다.
3. 기본 72개, 실제 창 미리 표시 10개, 메뉴 7개 검사에서 실패 없이 종료하는지 확인합니다.
4. releases/latest.json의 testsPassed, nativeTestsPassed, menuTestsPassed, ZIP SHA-256을 확인합니다.
5. 실사용 Alt+Tab 왕복, 창 묶기, 취소 후 TopMost 복원, 게임용 관리자 모드를 수동 확인합니다.
6. 검증 완료 시 해당 커밋에 버전 태그를 만들고, 성공한 배포 ZIP을 GitHub Release에 첨부합니다.

~~~powershell
git tag -a v1.1.2 -m 'AltTabFlow v1.1.2: Windows regression verified'
git push origin v1.1.2
gh release create v1.1.2 '.\releases\AltTabFlow-latest-win-x64.zip' --title 'AltTabFlow 1.1.2' --notes-file CHANGELOG.md --verify-tag
~~~

실기기 검증 전에는 태그와 완성 릴리스 배포를 하지 않습니다. 개인 설정, 창 목록 및 진단 로그는 저장소와 릴리스에 포함하지 않습니다.
