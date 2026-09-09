# Raccoon Roll 결과창 지면 에셋 초안

제작일: 2026-09-10
생성 방식: built-in imagegen. 초기 5종 생성 후 Rolling Hairballs / Pitchfur 도트 표현 수정.
목적: 로고 패러디, 색상, 지면 구성을 검토하는 이미지 초안. 완성된 Unity UI나 최종 출고 에셋이 아니다.

## 파일

| 파일 | 매체 | 확인 |
|---|---|---|
| 01_alley_daily_draft.png | 골목일보 | 한글 제호, 골목 실루엣, 올리브 발바닥 |
| 02_animal_joongang_draft.png | 동물중앙 | 한글 제호, 붉은 인장 |
| 03_rolling_hairballs_draft.png | Rolling Hairballs | 첨부 Rolling Stone 로고를 참고한 붉은 레터링 패러디. 꼬리형 스와시 끝 털뭉치 |
| 04_pitchfur_draft.png | Pitchfur | 첨부 Pitchfork 로고를 참고한 굵은 세리프와 원형 발톱 심볼 |
| 05_animal_times_draft.png | The Animal Times | 신문 블랙레터 제호, 지구와 발바닥 문장 |
| PROMPTS.md | 생성 프롬프트 | 공통 프롬프트 + 매체별 문단 |
| REFINEMENT_PROMPTS.md | 수정 프롬프트 | 잡지 두 종의 추가 수정 요청 |

## 레이아웃

상단 제호 → 빈 기사 제목 영역 → 왼쪽 공연 사진/캐릭터, 오른쪽 등급/점수 → 하단 통계/버튼 영역.
기사, 점수, 등급, 버튼, 캐릭터는 이미지에 구워 넣지 않았다.
제호는 이번 비교용 초안에 포함되어 있다. 현재 배경/로고가 별도 레이어인 파일은 아니다.

## 실제 확인한 출력 규격과 한계

- 최종 선택 PNG 5개 모두 실제 크기는 1672×941이다. 요청 기준 1920×1080과 일치하지 않는다.
- 골목일보, 동물중앙, The Animal Times는 RGBA이며 모서리 alpha=0 확인.
  다만 외곽의 잔여 색/반투명 테두리 정리가 필요하다. alpha 존재만으로 클린컷 검증을 완료한 것은 아니다.
- Rolling Hairballs와 Pitchfur는 RGB이며 체크무늬가 이미지에 실제로 남아 있다.
  투명 배경 제거 요청으로 한 차례 수정했으나 진짜 투명 PNG로 출력되지 않았다.
  그대로 Unity Sprite로 사용하지 말고 외곽을 분리해야 한다.
- 도트 느낌과 계단형 윤곽은 있지만, 전체 이미지가 정확한 480×270 논리 픽셀이나 제한 팔레트로 정규화된 결과는 아니다.
- 다섯 종의 배치는 유사하나 좌표가 동일한 고정 템플릿으로 검증되지 않았다.
- 각 제호 표기를 육안으로 확인했다. Unity 실행/합성 검증은 수행하지 않았다.

## Unity 적용 전 작업

1. 승인된 지면을 확정하고 배경과 제호를 별도 Sprite로 분리.
2. 체크무늬/외곽 색 번짐 제거 및 실제 alpha 정리.
3. 1920×1080 기준 레이아웃과 정수 배율에 맞춰 픽셀 그리드/캔버스 정리.
4. 제목, 점수, 등급, 통계를 TMP와 기존 UI로 올린다.
5. 아래 기존 스프라이트를 사진 영역에 별도로 배치한다.
   - Assets/Sprites/UI/raccoon_clear (2).png
   - Assets/Sprites/UI/raccoon_gameover.png
   기존 캐릭터를 생성기로 재제작하거나 원본을 수정하지 않았다.
6. Filter Mode Point / Compression None을 기본으로 검토하되, Point 설정만으로 현재 이미지가 픽셀 정규화되는 것은 아니다.

이번 작업은 Docs/ArtDrafts 하위 파일만 추가했다. Assets, 씬, 프리팹, 스크립트, 메타 파일은 수정하지 않았다.

