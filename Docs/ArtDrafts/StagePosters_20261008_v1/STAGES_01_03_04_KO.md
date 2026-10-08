# Stage 1 · 3 · 4 공연 예고 포스터

제작일: 2026-10-08  
방식: 기본 제공 imagegen. CLI/API fallback 사용 안 함.  
상태: 아트 초안. Unity 씬, 프리팹, 임포트 설정, 코드 및 에셋 참조는 변경하지 않음.

## 이번 제작물

| 파일 | 크기 | 콘셉트 | 텍스트 배치 여백 |
| --- | --- | --- | --- |
| Stage01_BuskingPoster_v1.png | 1024 × 1536 | 낡은 절취형 골목 버스킹 전단, 너구리와 고슴도치 2명 | 위쪽/왼쪽 넓은 종이 면: 공연명과 설명. 절취선 위 짧은 여백: 보조 정보 |
| Stage03_FestivalPoster_v1.png | 1024 × 1536 | 원색 실크스크린 풍, 동물 앞발과 기타, 야외 무대 | 상단 붉은 영역: 공연명. 아래 초록 영역: 소나기/깃발 기믹 설명 |
| Stage04_NightShowPoster_v1.png | 1024 × 1536 | 레트로 심야 토크쇼, 방송 세트에 앉은 3인조 | 상단 어두운 영역: 프로그램 타이틀. 아래 큐시트: 기믹 설명 |

- 1스테이지에는 스컹크를 넣지 않았다.
- 4스테이지에는 너구리·고슴도치·스컹크 3인조를 반영했다.
- 포스터 안에는 문구/로고/목표 점수/버튼을 굽지 않았다. Unity 텍스트와 별도 로고 이미지로 배치한다.
- Stage 3은 투명 이미지 시안의 색면이 만족스럽지 않아 불투명 직사각형 인쇄물로 새로 생성한 버전을 선택했다.
- Stage 1/4에는 알파 채널이 있으나 외곽·내부 불투명도와 프린지는 실제 UI 배경에서 추가 확인해야 한다. 완성된 런타임 에셋으로 검증한 것은 아니다.
- 시각 확인: 요청한 3가지 콘셉트 구분, 캐릭터 수, 스컹크 외형, 텍스트 여백, 문구 미포함.
- 파일 확인: 저장된 이미지 크기·형식·표본 알파 값 확인. 정밀 픽셀 격자 또는 제한색 팔레트 준수는 보장하지 않는 생성형 픽셀 일러스트 초안이다.
- 기존 Stage 2/5와 로고, 다른 팀원의 파일을 덮어쓰지 않았다.

## 전체 포스터 현재 선택본

1. Stage01_BuskingPoster_v1.png
2. Stage02_PunkPoster_v2_MonochromeBand.png
3. Stage03_FestivalPoster_v1.png
4. Stage04_NightShowPoster_v1.png
5. Stage05_LuxFaunaPoster_v1.png
6. LUX_FAUNA_PixelLogo_v1.png — Stage 5에 별도 배치하는 로고

## 최종 선택본 생성 프롬프트

### Stage 1

```text
Use case: ads-marketing. Asset for Raccoon Roll, a 2D PIXEL ART game. One portrait concert-announcement poster background, approximately 1024x1536 / 2:3. All lettering, logos, stage numbers, rules, times and buttons will be overlaid in Unity: ABSOLUTELY NO TEXT, no letters, numbers, logos, fake writing, ruled placeholder lines or watermark. Compose an expressive piece of poster graphic design with useful low-detail negative space, NOT a screenshot, wireframe, standardized UI template, labeled guide or contact sheet. Authentic visible square pixel clusters, stepped outlines, deliberate limited shading and sparse pixel dithering. No smooth vector curves, photographic grain, painterly rendering or blurry pixel filter. The entire front-facing poster perimeter must fit within the image. Transparent alpha only outside its silhouette; printed paper/art within remains opaque. No surrounding wall/table/room or checkerboard.
Stage 1: a charmingly shabby homemade ALLEY BUSKING handbill, printed cheaply by an unknown animal duo. Input images 1 and 2 are character identity references: the gold-crowned grey raccoon guitarist and spiky brown hedgehog drummer. Reference characters only, do not reproduce their white reference backgrounds.
Visual identity: warm old ivory paper, charcoal/black ink, a very small faded amber accent. Irregular guillotine-cut edges, a subtle fold, a couple of imperfect masking-tape tabs; bottom fringe of BLANK tear-off tabs, one missing. Distinct from Stage 2 punk: no red/blue collage, no safety pins, no elaborate pasted rectangles.
Free asymmetrical composition: a tall crooked streetlamp along the right edge casts a small amber pool onto exactly TWO musicians clustered in the lower-right/middle-right. The raccoon with small crown plays his electric guitar, and the prickly hedgehog plays a tiny improvised drum kit. Both in scruffy black sleeveless stage clothes, same designs as references. Modest cardboard amp, guitar case on the pavement, cable loop, a few simplified brick/utility-pole shapes. The duo is small enough to feel like a first neighborhood show, not a star concert. Render the characters as economical lively black-ink pixel drawings, not a large glossy illustration. No skunk, third bandmate, human or crowd.
Negative space integral to the drawing: an expansive unprinted ivory area in the upper-left for future title, continuing into a comfortably wide calm left/mid area for gameplay copy. Picture edges can intrude around the margins, but keep at least one nearly rectangular invisible safe area for 3-4 horizontal text lines, WITHOUT drawing a box. A small open paper strip above the tear-off fringe can later hold goal/time. Whimsically off-center, inexpensive DIY design, softly worn but sharp readable pixels. No artificial huge framed blank panels; just the blank paper of the flyer.
```

### Stage 3 — 불투명 인쇄물 선택본

```text
Create a NEW original Stage 3 concert-poster background for a PIXEL ART animal band game. Portrait 1024x1536, FULL-BLEED OPAQUE poster artwork. This version is a flat printed rectangular sheet, NOT a transparent cutout. Fill every pixel of the rectangular image with opaque ink colors. NO transparency, background extraction or isolated-object treatment.
The supplied Woodstock poster is a reference for the limited-color 1960s festival silkscreen DESIGN LANGUAGE only; do not copy its lettering, logo or exact illustration.
Use flat vermilion-red background (#e83b3b), cream, cyan-blue, grassy green, mustard yellow and dark plum. Very few solid color fills, all sharply separated. NO soft gradients, blur, vignette, airbrush, glows, shadows behind objects or black smudges. Not a glossy rendered image. Authentic large visible square pixels and stepped contours everywhere, like 256x384 game pixel art enlarged cleanly; no smooth vector edges. Sparse deliberate hard pixel print texture only at edges.
An oversized CREAM FURRY ANIMAL PAW grips a blue electric guitar neck sweeping diagonally from lower-left toward middle-right, in the central graphic area. An original small songbird and a festival pennant sit near upper-left. The paw/guitar illustration is strong, bold, simplified and playfully asymmetrical. A tiny outdoor stage, tent shapes, yellow sun, flags and cheering animal silhouettes cluster along the lower-right horizon. Below that horizon is a broad solid grassy-green area.
IMPORTANT graphic-design negative space: leave a large clean flat RED upper-right/upper-middle area for the future festival title and one generous flat GREEN area in the bottom quarter for several lines of game rules. The red and green copy spaces MUST remain solid opaque red and green, not black, fuzzy, transparent or framed. No boxes or empty paper scraps. No outlined UI panels. The reference-inspired large color fields naturally provide the copy spaces.
Unity will add all typography. ABSOLUTELY NO TEXT, no letters, digits, logos, lorem ipsum, placeholder rules or watermark. This is the final artwork background only, no mockup scene around the sheet.
```

### Stage 4

```text
Use case: ads-marketing. Asset for Raccoon Roll, a 2D PIXEL ART game. One portrait concert-announcement poster background, approximately 1024x1536 / 2:3. All lettering, logos, stage numbers, rules, times and buttons will be overlaid in Unity: ABSOLUTELY NO TEXT, no letters, numbers, logos, fake writing, ruled placeholder lines or watermark. Compose an expressive piece of poster graphic design with useful low-detail negative space, NOT a screenshot, wireframe, standardized UI template, labeled guide or contact sheet. Authentic visible square pixel clusters, stepped outlines, deliberate limited shading and sparse pixel dithering. No smooth vector curves, photographic grain, painterly rendering or blurry pixel filter. The entire front-facing poster perimeter must fit within the image. Transparent alpha only outside its silhouette; printed paper/art within remains opaque. No surrounding wall/table/room or checkerboard.
Stage 4: retro late-night TV talk-show music-guest announcement poster. Input image 1 is a mood/composition reference for a NIGHT TIME SHOW poster, not an edit target: borrow dramatic dark negative space and vintage broadcast atmosphere, NOT any words, humans, logos or faces. Input image 2 is ONLY the fictional trio's identity reference from the approved Stage 2 poster: crowned raccoon guitarist, spiky hedgehog drummer, black/white skunk bassist. Restore their normal muted grey/brown/black colors; do not copy the punk paper layout.
Visual identity: deep midnight navy/black, faded burgundy red, warm cream, restrained teal, wood-brown. Elegant retro television print aesthetic, visible PIXEL shapes, no photographic texture, no punk tears/safety pins, no glossy sci-fi gold.
Free asymmetrical composition: a broad dark empty area across upper-left/upper-middle reserved for a future oversized show-title overlay. Small cream crescent moon and pixel stars sit near upper-right, and a tiny illuminated red studio pilot lamp without lettering accents the edge. A TV studio set occupies the center/lower-middle: semicircular low stage, mustard/cream guest sofa, warm wood-slatted host desk with vintage microphone and plain mug, city-skyline window silhouettes. Camera/tripod silhouette partly at an edge suggests a real live recording, not a living room. No human host or newly invented fourth character.
Exactly THREE recognizably animal bandmates cluster awkwardly on/by the guest sofa as nervous music guests: the small-crowned raccoon holding his orange guitar too carefully with a forced grin, the hedgehog sitting upright gripping two drumsticks, the long-white-crested skunk with striped fluffy tail resting a bass upright and acting composed. The skunk is NOT a zebra. They are not performing a full rock set. Keep this compact character vignette clear, with enough space around faces.
Across the lower portion, one broad pale ivory paper CUE SHEET enters from a side at only a few degrees of tilt, held by one small clip; it is genuinely BLANK and mostly clean, large enough for several horizontal lines of future Korean gameplay text. This is a tangible production prop, NOT a UI dialog frame. A smaller calm dark margin elsewhere can hold future goal/time. Maintain large breathing spaces and strong type-ready hierarchy without actually drawing any type. Opaque dark printed poster interior, whole flat rectangular sheet visible, transparent only outside.
```

