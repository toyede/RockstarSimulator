# 관객 확인 교육·설명 여백 수정

2026-10-08 / Unity 6000.3.20f1 기존 Editor

## 구현

- Stage 1 `HoverHint`: 직전 카드에 호응한 관객 대신 새로운 CHILL 관객을 배치.
- 확인 대상만 튜토리얼 전용 안정 자세로 유지. 캐릭터 프레임·점프·반동은 멈추고 입장 페이드와 배치 보간은 유지.
- 새 관객 확인 단계 `CrowdChange`에도 동일한 안정 자세와 안내를 적용.
- 기존 `TutorialDragGuideUI`를 재사용하여 손 커서가 대상에게 이동 → 2초 머무름 → 반복. 카드 잔상은 표시하지 않음.
- 가상 안내는 raycast를 차단하지 않으며 실제 포인터나 게임 입력을 조작하지 않음.
- 지정된 관객을 2초 연속 확인해야 완료. 다른 관객이나 포인터 이탈은 대기 시간 초기화.
- 관객 확인 중 카드 사용은 소비 전에 차단. 다음 단계·스킵·비활성화 시 안내와 자세 제어 해제. 풀 재사용에서도 자세 플래그 초기화.
- 제목 28/최소 24, 본문 22/최소 20, 진행 안내 20, 큐시트 헤더 22. 기존 종이 크기·좌측 위치와 강조색은 유지하고 내부 좌우 여백을 10%로 확대.
- Hover 설명은 짧은 제목과 PC/모바일 행동 안내로 분리.

## 검증

총 **40 PASS / 0 FAIL**:

- 실제 Input System 가상 마우스로 일반 관객 확인 17개: 새 ID, 고정 위치·프레임, 안내 이동, raycast 비차단, 다른 관객/짧은 확인 차단, 포인터 이탈 시 초기화, 실제 2초 확인 완료, 다음 단계·비활성화 복구, CrowdChange 안내.
- 대표 긴 튜토리얼 6쌍의 제목/본문 12개: 최소 8px 수직 여유 기준 통과. 실제 최소 여유 21.8px.
- Stage 2 기존 특별 관객 드래그 안내 회귀 5개: 올바른 카드 바인딩·드롭 대상·안내 활성·raycast 비차단·모드 초기화.
- 실제 `CardInput.TryUseCard` 경로 6개: 3개 슬롯 사용 거부, 동일 손패, CardResolved 이벤트 없음, 점수 변화 없음.
- Unity 컴파일 성공. 수정 C# diff 공백 검사 통과.
- Game View 시각 확인: `Assets/Docs/Verification/CueSheets/tutorial-hover-spacious.png`.

검증 중 첫 CLI 호출의 컴파일 대기/Pipeline 타임아웃 및 Play 중 도메인 재로드로 초기 테스트가 중단되어, 컴파일 완료 후 새 Play 세션에서 위 결과를 확인했다. 초기 재로드에서 발생한 `StageLightEventBridge` 설정 오류와 Pipeline 로그를 이번 기능의 최종 성공 결과로 숨기거나 Console 전체 0건으로 주장하지 않는다. 최종 테스트 구간에서 새 게임 코드 오류는 발견되지 않았다.

테스트는 합성 스테이지 전환·가상 마우스를 사용하며 완료 기록이나 랭킹을 저장하지 않는다. 종료 시 테스트 플래그·가상 입력 장치를 해제하고 Play Mode를 종료한다.

## 변경 파일

- `Assets/Scripts/Tutorial/TutorialFlow.cs`: 대상 생성·진행 판정·복구.
- `Assets/Scripts/Tutorial/TutorialDragGuideUI.cs`: 일반 관객 확인 안내 모드.
- `Assets/Scripts/Tutorial/TutorialOverlayUI.cs`: 폰트와 내부 여백.
- `Assets/Scripts/Audience/AudienceMemberActor.cs`: 교육 전용 자세, 풀 재사용 초기화.
- `Assets/Scenes/Main.unity`: 기존 튜토리얼 컴포넌트 폰트/텍스트 영역만 Editor API로 저장.

모바일/WebGL 실기기 및 전체 투어 통과 테스트는 이번 검증에 포함하지 않았다.
