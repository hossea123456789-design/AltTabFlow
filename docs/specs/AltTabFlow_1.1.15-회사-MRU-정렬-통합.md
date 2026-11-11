# AltTabFlow v1.1.15 · 회사 수정 내용 1.1.9 및 1.1.10 병합 명세

## 근거
업로드된 2026-11-11 수정 문서 2건의 변경사항을 적용. 파일명상의 1.1.9, 1.1.10은 회사 로컬 수정본이며 동일 번호로 존재하는 과거 GitHub 릴리스와 구분. 공개 저장소의 기준은 1.1.14.

## 1.1.9 기능
- Excel A→B와 B→A 간 짧은 Alt+Tab 왕복. Chrome 등 동일 프로세스의 여러 독립 최상위 창에도 동일.
- 현재 HWND와 실제 owner/popup 체인만 제외하고 앱 단위 그룹은 남김.
- 그룹 멤버 `Entry.Windows`와 선택 `Entry.Window` 분리, 클릭·시작·썸네일·강조·전환 전부 선택 HWND.
- Rebuild(true), Enter 그룹 확대, Backspace 복귀, HWND 폐기 시 복구.
- KeyboardHook/실제 MRU 수집 정책/원래 설정 파일은 변경 금지.
- 별도 프로세스 창 A/B 실제 전환 12개 focus 테스트.
- package.ps1에서 ZIP 일시적 잠금 시 최대 3회 재시도, 완료된 기존 릴리스는 덮어쓰지 않음.

## 1.1.10 기능
- 실제 창 활성화 MRU 기본, 그룹은 가장 최근 멤버 `Windows[0].Order` 기준 정렬. 표시/대표 hwnd 분리.
- `SortAlphabetically`, `PrioritizePinned` 독립 선택, 둘 다 기본 false. 네 조합 모두 보존.
- 정렬 페이지의 두 체크박스는 Save 전 변경사항 미적용; Cancel 무손실. 이전 settings.json 두 속성이 없으면 false, 기존 등록앱/숨김/색상 보존.
- Ctrl+P 또는 우클릭으로 상단 등록할 때 Priority 활성화.
- 한 제스처 동안 목록 순서 고정, 재진입 시 새 MRU 반영. 기존 게임/빠른 연타/업데이터/UAC 유지.

## 검증
- Windows CI 기대 기본286 + UI106 + updater22 + startup11 = 425.
- 별도 `--focus-self-test` 기존18 + 신규12 = 예상30. 실제 Microsoft Excel 물리 전환은 사용자 PC 수동 검증.
- 출처 문서의 회사 빌드 SHA-256은 회사 로컬 1.1.9/1.1.10 파일에만 적용되며 이 통합 1.1.15 해시로 재사용하지 않음.
