# AltTabFlow v1.1.14 · 빠른 Alt+Tab 반복 입력 회귀 수정

## 사용자 보고
메이플스토리 Alt+Enter 전체화면 및 일반 창에서 Alt+Tab을 아주 빠르게 반복하면 선택기가 즉시 닫히지 않고 남아 있다가 뒤늦게 닫힌 뒤 이후 Alt+Tab이 먹통으로 보임. v1.1.13의 메이플 행 선택 후 전환창 소실 및 우측 미리보기 글자 겹침 수정과 독립적인 추가 회귀.

## 소스 정적 분석
- 키보드 훅에서 대기 명령을 `ConcurrentQueue`에 넣고 WinForms `BeginInvoke`로 처리.
- 기존 `SwitcherContext.DrainInput`은 제한 없는 `while` 반복 및 매 루프 큐 전체 `ToArray()` 복사. 입력이 빠르면 Paint, 키 해제, 10ms 전환 Commit 검사 타이머가 메시지 루프에서 밀릴 수 있음.
- `drainPosted`를 드레인 진입 시 해제해 처리 중 새 BeginInvoke가 중복 게시될 가능성.
- 이미 chooser가 foreground인데도 매 Tab마다 별도 activation.Request를 실행.

## 설계 및 불변 조건
1. 메시지 한 턴에서 48개 명령 또는 8ms까지만 FIFO 처리하고 잔여 입력은 다음 메시지로 재예약.
2. `ConcurrentQueue.ToArray` 전체 큐 복사 금지, 최대 48개까지 제한 복사. Next/Previous/Commit/Cancel 순서 그대로.
3. 드레인 작업 중 게시 플래그 유지; finally에서 해제 및 잔여 입력 재예약. CommitFinished 동기 재진입/예외에도 고착 방지.
4. chooser가 실제 foreground일 때 중복 WindowActivation 작업 금지. 기존 900ms Commit deadline, 게임 Peek/Z-order, 물리 Alt/Tab 키 해제 정책 유지.
5. 신규 UI 정책 검사 3개, 기본206+UI99+업데이터22+관리자11=338개 CI 목표.

## 실기기 검증
- 메이플스토리 Alt+Enter 전체화면 창·독점 전체화면을 구분해 Alt+Tab 10회·30회 고속 반복, 중도 ESC·마지막 키 해제·이후 새 Alt+Tab 작동을 확인.
- Chrome/탐색기 왕복, Ctrl+Alt+Tab 고정, 화살표/휠, Peek 복원, 오른쪽 DWM 프리뷰.
- Windows CI는 실제 DirectX 독점 전체화면·게임 보호/관리자 권한·물리 키 연타를 대체하지 않음.
