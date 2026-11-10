# 1.1.13 사용자 스크린샷/재현 요구 (2026-11-11)

사용자 보고 원문 취지:
- Alt+Enter로 MapleStory 화면을 확대하고 AltTabFlow에서 MapleStory를 한 번 거친 뒤 전환창이 잠시 사라짐
- Chrome/카카오톡 등 다른 앱을 약 두 번 오가며 Alt+Tab을 더 누르면 전환창이 다시 보임
- 우측 MapleStory 창 미리보기에서 창 제목과 '선택한 창 미리보기' 라벨이 겹침

직접 확인된 정적 코드 관찰: `TargetPeek.Select`는 선택된 대상 HWND를 chooser 바로 뒤 Z-order에 위치시키는 Win32 요청을 수행함. `SwitcherForm`의 기존 Deactivate는 origin이 아닌 foreground를 보았을 때 즉시 화면을 닫음. 우측 미리보기 창 제목 `ty+35`와 고정 footer `H-160`는 화면 크기에 따라 겹침.

패치: MapleStory 행은 external Peek/Dim/Aura를 사용하지 않고 DWM thumbnail만 남김. 빠른 Alt 세션 중 일시적 focus churn은 사용자 입력이 살아 있는 동안 보호. 우측 미리보기 footer 텍스트를 없애고 Title을 버튼 상단으로 제한.

테스트: 기본206+UI96+Updater22+Startup11=335 자동 검사 목표. 메이플 Alt+Enter 실제 전체화면·게임 안티치트/Compositor 관련 검증은 사용자 PC 수동 수행. 첨부 스크린샷 파일은 개인 자료이므로 저장소에 업로드하지 않음.
