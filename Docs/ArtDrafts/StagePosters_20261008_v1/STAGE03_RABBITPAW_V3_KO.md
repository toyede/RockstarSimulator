# Stage 3 토끼 앞발 수정 v3

- 파일: Stage03_FestivalRabbitPaw_v3.png
- 도구: 기본 제공 imagegen. CLI/API fallback 미사용.
- 크기: 1024 × 1536 PNG.
- 긴 사람 손가락처럼 보이던 형태를 짧고 통통한 토끼 앞발 형태로 수정.
- 사용자가 표시한 베이스 아래쪽의 길게 늘어진 털과 빨간 마크업 제거.
- 빨강/초록 배경, 새, 깃발, 페스티벌 풍경, 4현 베이스, 텍스트 여백 유지.
- 생성 결과에서 앞발 형태 및 표시 영역의 털 제거를 시각 확인. 크기와 불투명 PNG 형식을 파일 확인.
- 기존 v2는 보존. Unity 연결 및 런타임 검증은 이번 작업에 포함하지 않음.

## 최종 프롬프트

```text
Use case: precise-object-edit. Edit the user's attached annotated PIXEL ART red festival poster. The red hand-drawn circle is an EDIT ANNOTATION, not part of the intended artwork. Make ONLY these two local corrections:
1. Replace the long human-looking fingers of the large cream hand holding the blue four-string bass with a CHUBBY CARTOON RABBIT FRONT PAW: short thick softly rounded digits, broad cushioned palm, compact friendly mitten-like proportions but with clean separations between the visible fingers. No human fingernails, long bony finger segments, sharp knuckles or tendon detail. Four compact visible curved finger shapes naturally resting on/gripping the fingerboard, thumb hidden behind it. Use the same cream and restrained tan pixel shading. Rounded silhouettes must still be rendered with deliberate PIXEL steps, not smooth vector/painted curves. Keep the hand at its current location and about the same overall size; retain one short wrist connection toward the left edge, not a separate floating glove.
2. COMPLETELY REMOVE the redundant long pale furry strip BELOW the bass neck inside the user's red circle in the LOWER LEFT (roughly x=2-28%, y=67-80%). It looks like an extra dangling furry forearm/tail under the instrument: DELETE THAT ENTIRE STRIP. Reconstruct the clean RED background where the strip was above the green meadow, and reconstruct the GREEN meadow edge where appropriate. Only the bass neck/dark edge and the actual compact gripping paw remain; NO spare fur, cuff, second arm, dangling wrist or tail sticking out beneath the bass. Remove EVERY red markup-circle stroke too, including the segment over green and the line near the fretboard.
Strictly preserve everything else: dominant bright red field, green empty lower copy space, bird, pennant, sun, tiny festival stage, tent, crowd, exact diagonal bass placement, cyan/cream/yellow palette, four strings, four tuning keys and four posts, dimensions and pixel-art rendering. Keep existing text spaces blank. No new lettering, panels, animals, logos or props. Full opaque portrait image, approximately 1024x1536, no transparency, no crop, no borders. Do not redesign the poster.
```

