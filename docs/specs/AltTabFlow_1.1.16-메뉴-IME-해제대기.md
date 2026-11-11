# AltTabFlow v1.1.16 · 회사 로컬 1.1.11 종료 대기 고착 수정 병합

## 업로드 문서·버전 구분
- `AltTabFlow_1.1.11_갱신내용.md` (2026-11-11 KST, 비교 기준 회사 로컬 1.1.10).
- 회사 파일의 1.1.11 번호는 기존 GitHub 공개 v1.1.11 및 v1.1.15와 서로 다른 버전 체계.
- GitHub main v1.1.15 코드에는 메뉴 VK_APPS(0x5D), IME VK_PROCESSKEY(0xE5), VK_PACKET(0xE7)의 현재 키 눌림이 `ShortcutRouter.knownDown`/`heldKeys`에 포함되어 Alt+Tab `Commit`을 가로막을 수 있는 코드 경로 존재.

## 회사 소스가 제공한 근거 및 한계
- 재현 상태(회사 1.1.10): `lastTabCtrl=false`, `physicalCtrl=false`, `pinnedSession=false`, `session=true`, `heldKeys=[0x5D]`, `knownDownCount=1`, `physicalAlt/Shift=false`. 최근 Next 이후 Commit 없음. 상태 기록 당시 UI는 hidden이었지만 라우터 입력 세션 잔류.
- 원래 누가 메뉴 키 상태를 남겼는지는 보고서로 특정하지 못함(앱/장치/리매퍼 미상). 이 내용을 확정 원인 프로그램이라고 추정하지 말 것.
- 회사 로컬 1.1.11에서 사용자 확인 “이제 둘 다 바로 닫힘”; GitHub 새 1.1.16 물리 키·Excel·게임 검증으로 대체하면 안 됨.
- 회사 로컬 파일 SHA-256: ZIP `980AECFE3B50A0126D5E0EAA344A6934988C011C95AFE22934AE5A860F415A94`; EXE `4C9009FE7FD2F301693D35FC59A9372A38D173C36CDF82E7931D9092D152E46C`. 이 버전 소스에 사용하지 않음.

## 구현 불변 조건
1. `ReleaseParticipant(k)`는 0x5D,0xE5,0xE7에 대해서만 false.
2. `ShortcutRouter.Route`의 이벤트 소비 정책 변경 없음; 세 키는 포워딩, 강제 key-up 없음.
3. 키 down의 `knownDown` 등록, 세션의 `heldKeys` 등록, `ReconcileReleased`의 stale 처리에서 세 키 제외. 나머지 키의 물리 해제 판정 그대로.
4. `auxiliaryKeyDowns` 누계, `switch-open` pinned/persistent 기록, 실제 Alt/Ctrl/Shift가 모두 false인데 입력 세션이 500ms 지속하면 그 세션당 `release-wait` 한 번 기록. 이것은 타임아웃 자동 종료가 아니라 진단.
5. 세션 종료시 WM_TIMER 및 임시 release-wait 상태 초기화. 전체 입력 내용은 저장하지 않음.
6. 기존 1.1.13 메이플 Alt+Enter, 1.1.14 bounded FIFO, 1.1.15 같은 Excel HWND 왕복·MRU/정렬·앱 등록 우선 옵션·관리자 시작·공개 업데이트 보존.

## 테스트 및 릴리스 조건
- 기본 기존286+신규 라우터22+폼6=314. UI106, updater22, startup11: 자동 **453개**.
- 별도 전경 focus 통합 테스트, 실제 회사 PC 일반 Alt+Tab 및 고정 Ctrl+Alt+Tab, Excel 두 문서, MapleStory 전체화면/빠른 키 연타는 CI 결과와 구분.
- SOURCE-SHA256 manifest가 모든 수정된 추적 소스 파일의 UTF-8 바이트를 검증. PR head CI 통과 → main 동일 sha CI 통과 → 공개 prerelease ZIP+SHA256SUMS 확인 후 완료.
