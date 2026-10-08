# 큐시트·결과창 가독성 수정 검증

2026-10-08 / 기존 Unity 6000.3.20f1 Editor에서 확인.

## 변경

- 튜토리얼 종이 높이 440px, 특별 관객 레슨 432px. 폭·좌측 배치는 유지.
- 제목·본문·진행 안내 영역 분리, 줄 간격 확장, 관객 성향·카드·피버 강조색 복원.
- 보스 미션 높이 기본 384px, 부가 정보가 있을 때 480px.
- TMP 자동 축소를 2px 단위 맞춤으로 변경. 공유 폰트 에셋은 수정하지 않음.
- 결과창은 등장 연출 후 수평으로 정착. 가짜 볼드·외곽선을 제거하고 신문별 헤드라인 여백 조정.
- 통계 라벨을 구분선 아래로 10px 내림. 긴 보스 판정은 부제로 분리하여 기록 누락 없이 표시.
- 공연 규칙·결과 판정·점수 저장·증강 로직 변경 없음.

## 확인 결과

- Unity 재컴파일 성공, compilationFailed=false.
- 긴 튜토리얼 6쌍(12개 영역)과 신문 5종 × 성공/실패 × 14개 영역: **152 PASS / 0 FAIL**.
- `VerifyReadability.cs`는 실제 씬 UI에 합성 기록을 표시해 영역 넘침과 텍스트 bounds를 검사. 점수를 저장하지 않음.
- 실제 Game View: 특별 관객 튜토리얼, 단일 패널에 보스 패턴·스탠딩석 결과를 함께 표시, Pitchfur 결과창, 긴 보스 결과창을 캡처·시각 확인.
- 최종 Console groundTruth 오류 0. 버퍼의 이전 Pipeline 타임아웃·인자 검증 오류는 게임 런타임 오류와 구분.
- 테스트용 타이머 정지·호응도 감소 억제·튜토리얼 억제 플래그를 해제 후 Play Mode 종료.

캡처: `Assets/Docs/Verification/CueSheets/tutorial-readable.png`, `mission-readable.png`, `result-readable-pitchfur.png`, `result-readable-boss.png`.

## 미검증

- WebGL/모바일 실기기, 모든 화면 비율·번역 문자열.
- 실제 투어 전체를 끝까지 진행한 결과 저장/전환 회귀 테스트. 이번 결과 검사는 합성 기록으로 UI에 한정함.
