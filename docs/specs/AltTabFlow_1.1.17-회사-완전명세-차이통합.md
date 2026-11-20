# GitHub AltTabFlow v1.1.17 — 회사 전체 재구현 명세 비교·이식

## 원본·계보 및 검증 범위

- 사용자 업로드: `AltTabFlow_재구현_완전명세.md` (2026-11-20 KST), 문서 내 사용자 계약 1~18장, 부록의 전체 원본 소스 37개. 부록 manifest 각 파일의 길이·SHA-256이 실제 추출 후 정확히 일치함을 검증했다.
- 비교 대상: 기존 공개 GitHub main v1.1.16 (commit `fe2908495e1fcf6b526ca1f60557951fc52fe5e9`). 회사 파일에 적힌 로컬 v1.1.16은 GitHub v1.1.16과 서로 다른 계보.
- 실제 회사 1.1.16 실행본의 ZIP `93CE3BA981B3963FB670E8068CC8B247EF4573EE61616A34BBF9AEC442741C21`과 EXE `68A48BA177D079C647F1647F3809EBB476DD73F1159CEAFF2A7E8CACA235BD72`는 회사 기준본 식별 해시다. 새 GitHub ZIP의 검증 해시로 재활용하지 않는다.

## 기존 GitHub에 이미 구현되어 변경하지 않은 사용자 계약

- App 그룹별 목록·카드·아이콘 보기 및 주 모니터 표시; 다른 모니터 창 포함·높이/열 조정.
- MRU 최초 선택은 실제 독립 HWND·소유 popup 관계 기준. Excel A/B 또는 Chrome A/B 왕복, 그룹 대표와 선택 HWND 분리. 기본 표시 순서 MRU, `SortAlphabetically`·`PrioritizePinned` false 기본, 설정 저장/취소.
- 누른 실제 키를 모두 놓으면 일반 Alt+Tab 한 번 확정, 명시적 Ctrl+Alt+Tab/Space 고정, 늦은 Ctrl·Ctrl 해제 순서. 메뉴/IME VK_APPS(0x5D), VK_PROCESSKEY(0xE5), VK_PACKET(0xE7) 제외.
- 미리보기 TargetPeek, 아우라/딤드·7색 프리셋, 단 게임 MapleStory Alt+Enter에서 전경/Topmost 경합을 피하도록 해당 게임 외부 Peek/Dim/Aura 생략.
- 빠른 연타 대응 UI drain 48명령 또는 8ms의 유한 slice. 게임 관리자 재시작/예약 작업 자동 시작, Windows 10/11 .NET 8 portable 단일 EXE ZIP.
- 현재 운영 업데이트는 공개 GitHub Releases에서 암호/토큰 없이 내려받고 GitHub SHA-256 digest와 EXE 버전 검증. 설치 트랜잭션은 기존 파일들을 별도 백업해 EXE를 마지막으로 교체하고 새 PID·버전·훅 확인 실패 때 정상 종료가 가능한 경우 복구. 시험용 임시 설치 폴더에 대해 8개 롤백 검사 추가. 회사 Gitea feed로 교체하는 것은 이 사용자 GitHub 프로젝트의 목적과 충돌하므로 제외.

## 실제 반영한 차이

| 회사 명세 | GitHub v1.1.16 상태 | 통합 v1.1.17 |
|---|---|---|
| 11.11 / 1.1.12 종료·중요 예외 진단 | 로그를 무한 ThreadPool에 파일 비동기 append, 세션 미완료 추적 없음 | 512 bounded queue + 단일 worker, critical.jsonl 독립 Flush(true), 1MB×4 회전, 이전 session marker, 키보드·포그라운드·해제 타이머 예외 경계, 60초 heartbeat |
| 11.13 / 1.1.14 재시작 설정 보존 | 설정 원본 파일만 읽고 Save가 File.Move 덮어쓰기, 생성자에서 무조건 prefs.Save() | settings.json → .lastgood → .bak 순서 및 손상 원본 보존, candidate Commit 후 메모리 반영, 시작시 무조건 재저장 제거 |
| 11.14 / 1.1.15 X·큰 그룹 버튼 | 행·카드·아이콘의 그룹 버튼 소형, 개별 창 정상 닫기 X 없음 | X 36×38, 그룹 80×38, 클릭 영역 분리, HWND+PID 확인 후 WM_CLOSE만 비동기 전달 |
| 11.15 / 1.1.16 그룹 마우스 릴리스 | 키 해제시 진행 중 클릭 결과가 반영되기 전에 Commit | key-first `commitAfterMouseRelease` 보류; mouse-first 마지막 키 Commit; pinned/취소/영역 밖 일관 처리 |
| 11.12 / 1.1.13 회사 Gitea 업데이트 | GitHub 공개 업데이트 별도 기능 이미 있음 | GitHub 업데이트 유지; 회사 서버·인증토큰·내부 구조 미복제 |

### 보안·설정 보존 유의사항

- `settings.json`, `settings.json.lastgood`, `settings.json.bak`, `settings.json.corrupt-*`, `session.json`, `session.previous.json`, `events.jsonl`, `critical.jsonl`은 사용자별 로컬 경로에만 존재. 공개 저장소에 개인 설정/원시 진단 기록을 올리지 않음.
- `WM_CLOSE`는 외부 프로그램의 저장 확인·취소 흐름에 대한 요청이지 강제 종료가 아니다. PID가 다르거나 사라진 HWND, 자기 프로세스에 전송하지 않는다.
- `Release`/온전한 설정 저장 완료 여부는 실제 디스크/Windows 인스턴스를 검사해야 하며, 코드 검토만으로 성공을 선언하지 않는다.
- 중요한 로그의 동기 Flush(true)도 파일시스템 오류·정전 중 100% 보존을 보장하지 않는다. 종료 세션 미완료 감지는 원인을 자동 분류하지 않음.
- 관리자 로그인 예약 작업 기능은 유지. 사내 명세의 일반 HKCU Run만 쓰는 정책으로 다운그레이드하지 않음.

## 회귀 검사 계획

- `--self-test`: 314개 (기존 완료).
- `--ui-self-test`: 133개 = 기존106 + 27 (세 보기 각각 버튼 폭/영역 분리/키 우선/마우스 우선/고정·취소/가짜 HWND X).
- `--diagnostics-self-test`: 회사 독립 시험 프로세스 원본 52개 (실제 clean/kill/fatal, bounded queue, JSONL 회전, 원본/lastgood/bak/Corrupt/commit/잠금, 프로세스 간 설정 및 수정 성공).
- `--updater-self-test` 22 + `--startup-self-test` 11. CI 기본 고유 목표 314+133+52+22+11=**540**.
- `--peek-self-test` 10, `--menu-self-test` 7, `--focus-self-test` 30은 실제 Windows 전경 환경을 요구하고 focus에 peek10이 겹쳐 CI의 540 건과는 별개. ZIP 패키징 시 실제 창 검증도 필수.
- 사용자 실제 환경의 P4 소유 팝업, Chrome 다중 창 마우스·키 릴리스, 실제 Excel A/B, 관리자 MapleStory Alt+Enter와 DXGI 전체화면은 자동 검증으로 대체 불가.

## 통합 이후 확인해야 할 경계

- Company section 18의 혼합 DPI 모니터 변경, DWM 뒤늦은 topmost, 재사용된 외부 HWND, 실제 게임/IME/보호창, 미리보기로 인해 작은 창이 가려짐 및 비정상 종료 후 남은 임시 topmost는 추가 실물 검증 범위다. 문서가 자동 해결을 보장하지 않는다.
- `RunJournal`은 시작 시 이전 clean 여부를 분리해 기록하지만 예전 마커가 없던 버전의 종료 원인은 소급 추론하지 않는다.
- CI 성공 뒤 PR 병합 → main SHA 동일 Windows CI 재검사 → v1.1.17 프리릴리스 ZIP + SHA256SUMS 공개 게시 여부를 확인하기 전 사용자에게 배포 완료로 안내하지 않는다.
