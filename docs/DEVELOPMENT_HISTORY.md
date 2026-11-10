# AltTabFlow 개발·운영 이력 (2026-11-10)

이 기록은 **대화 요약이 아니라 GitHub에 남기는 장기 추적 로그**입니다. 실제 검증과 첨부 문서에 기록된 검증을 혼동하지 않습니다. 소스의 진실 공급원은 항상 해당 시점의 Git commit이며, 요구사항의 진실 공급원은 버전별 원문 명세입니다.

## 버전별 타임라인

| 버전 | 요청/구현의 핵심 | 테스트 수 (중복 제외, 명세 기록) | GitHub에서 별도 검증한 범위 |
|---|---|---:|---|
| 1.1.1 | 원본 완전 명세와 복원 스크립트, WinForms 25파일, MRU·Peek·테마·게임 호환 | 기본 72 + Peek 10 | Linux 작업 환경에서 소스·SHA 검증. Windows EXE 생성/실동작 재검증은 하지 않음 |
| 1.1.2 | ContextMenuStrip Closed/Dispose 생명주기 오류 수정, 초기화/런타임 오류 분리, 메뉴 7개 검사 | 89 (72+10+7) | 소스/패치 복원 후 비공개 GitHub 저장소 최초 등록. 초기 Windows CI 컴파일+실제 UI screenshot 성공 |
| 1.1.3 | 전경 전환 회귀 발생, 1.1.4에서 해결하도록 갱신 명세가 지시 | 별도 최종판 아님 | 이 저장소의 정식 안정 버전으로 취급하지 않음 |
| 1.1.4 | P4 소유 팝업·실제 클릭 창 기준 MRU, 전경 활성화 확인, QuickGesture/FX 최적화, Switching.cs 추가 | 112 (기본87+Peek10+메뉴7+Focus18−중복10) | [PR #2](https://github.com/hossea123456789-design/AltTabFlow/pull/2), [Windows 성공 CI](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42764686888), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/b2e5aa57d031d7201d40788fa6bcf7db290509a1) |
| 1.1.5 | 일반 Alt+Tab 방향키 탐색 시 자동 고정 제거, 마지막 key-up 커밋·Esc 취소 | 142 (117+10+7+18−10) | [PR #3](https://github.com/hossea123456789-design/AltTabFlow/pull/3), [Windows CI 기본117](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42766936582), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/c26cc1237c1920d374487a91f154c4c8284f2694) |
| 1.1.6 | 설정 좌측 메뉴/우측 카드/하단 고정 저장, 7종 테마, owner-draw UI, mouse-up 커밋, UI 62개 | 204 (117+10+7+18+62−10) | [PR #4](https://github.com/hossea123456789-design/AltTabFlow/pull/4), [Windows CI 기본117+UI62](https://github.com/hossea123456789-design/AltTabFlow/actions/runs/42768914976), [main merge](https://github.com/hossea123456789-design/AltTabFlow/commit/93fb3164a1945016926526730b791ffb1f6f4140) |
| **1.1.7** | 숨길 앱 Enter 줄바꿈, Ctrl+Alt+Tab 고정·반복·늦은 Ctrl 승격, 상단 등록 앱 아이콘·폴백·비동기 로딩 | **257 (161+10+7+18+71−10)** | 이번 세션에 소스 diff 9개와 문서 반영. **독립 GitHub CI·사용자 PC 실행 결과는 최신 PR에서 확인** |

## GitHub 전환과 관리 결정

- 비공개 저장소 `hossea123456789-design/AltTabFlow`를 초기화했습니다.
- 초기 원본과 1.1.2 변경을 Git으로 등록하고, GitHub Actions Windows 빌드·테스트 후보 EXE 업로드, 메인/설정 화면 캡처 자동화 및 Issues/PR 템플릿을 추가했습니다.
- 이후 요청은 GitHub 별도 브랜치 → PR → CI → `main` 병합 흐름으로 관리. CI 컴파일과 실사용 회귀 테스트는 구별했습니다.
- [Issue #1](https://github.com/hossea123456789-design/AltTabFlow/issues/1)은 **버전마다 갱신하는 실사용 검증의 단일 체크리스트**입니다. 검증 미완료 상태에서 정식 GitHub Release를 만들지 않습니다.
- 버전별 원문 명세를 `docs/` 아래에 보관. 1.1.1 완전 원문은 `docs/1.1.1-완전명세-아카이브.md`, 후속 패치는 `docs/1.1.2-변경명세.md`, `docs/1.1.4-변경명세.md`, `docs/1.1.5-변경명세.md`, `docs/1.1.6-UI개선-변경명세.md`, `docs/specs/AltTabFlow_1.1.7_갱신내용.md`입니다.

## 추적해야 할 중요한 회귀/설계 결정

1. **우클릭 메뉴**: 반복 close/open 중 `ContextMenuStrip`을 `Closed`에서 Dispose 하면 충돌. 메뉴를 컨트롤 소유로 재사용.
2. **원래 창의 Z-order**: 전환 대상 Peek는 진짜 HWND를 다루며 Escape/취소 시 원래 TopMost 등 상태를 복원해야 함.
3. **P4/메이플스토리**: HWND·소유 관계·권한과 실제 `GetForegroundWindow` 확인이 중요. 요청만 성공했다고 전환 성공 처리하지 않음.
4. **키보드 단축키**: 일반 마지막 키 해제 커밋과 고정 Ctrl+Alt+Tab 제스처는 독립 상태. 누르는 순서·좌우 수정키·Esc 취소를 매번 회귀 테스트.
5. **설정 저장**: 미리보기로 즉시 `settings.json`을 변경하거나 자동 실행 Registry 상태를 바꾸면 안 됨. 숨길 앱 입력 Enter는 **줄바꿈**, 수동 앱 등록 입력 Enter는 **등록**.
6. **앱 아이콘**: 비동기 아이콘 추출 중 UI를 막지 않으며, 종료된 폼에 BeginInvoke/Invalidate 하지 않음. 체크 선택의 객체 참조 동일성 유지.
7. **Windows UI**: 1.1.6 이후 도입된 owner-draw/GDI 테마와 사용자 키보드 접근성 유지. 96 DPI 검증과 혼합 DPI 실측은 구분.
8. **EXE 이력**: 명세에 언급된 배포 ZIP/EXE 해시는 타 환경의 빌드 지문. GitHub CI로 재빌드한 파일과 당연히 일치하지 않을 수 있음.

## 남은 실제 검증

- 첨부 1.1.7 문서에는 일반 권한에서 관리자 메이플스토리가 foreground를 차지하여 전경 회귀 검사가 실패한 기록이 있습니다. 관리자 권한 **전체 재검사**가 성공했다는 기록은 있지만, 사용자 PC에서 동일 재현을 완료한 것은 아닙니다.
- 실제 **P4 메인/소유 팝업**, 관리자 게임에서 Alt+Tab/Ctrl+Alt+Tab 물리 키 반복, 다중 모니터·DPI, Peek 취소 복원을 Windows 실기기에서 확인해야 합니다.
- 검증 결과를 기록할 때 “인계 명세의 통과 이력 / GitHub CI 재실행 / 사용자 실기기 재실행”을 각각 분리해야 합니다.
- 성능 p50/p95/max, 비정상 종료 시 TopMost 복원, .NET 8 지원 종료 후 업그레이드 계획 등은 [ROADMAP](ROADMAP.md)의 후속 후보입니다.

## 새 세션 작업자에게

빠른 시작은 [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md). 구현 변경은 [ARCHITECTURE_AND_INVARIANTS.md](ARCHITECTURE_AND_INVARIANTS.md), 테스트는 [TEST_MATRIX.md](TEST_MATRIX.md). 명세와 현재 소스의 충돌이 생기면 최신 사용자 요청 → 최신 version patch → Git commit/실제 테스트 로그 순으로 분리하여 근거를 남깁니다. ChatGPT 대화 메시지를 유일한 변경 근거로 쓰지 마세요.
