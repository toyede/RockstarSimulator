# Stage 2 흑백 수정 / LUX//FAUNA 로고

- 제작일: 2026-10-08
- 생성 도구: 기본 제공 imagegen. CLI/API fallback 미사용.
- 이전 포스터는 덮어쓰지 않았다. Unity 씬/프리팹/코드에 연결하지 않은 아트 초안이다.

## 결과

- **Stage02_PunkPoster_v2_MonochromeBand.png**: 멤버 3명 및 악기를 흑백 복사 인쇄 느낌으로 변경. 기존 크림색 종이, 빨강/파랑 장식, 여백과 전체 구도 유지.
- **LUX_FAUNA_PixelLogo_v1.png**: 단독 투명 PNG. 금빛 레트로 전자음악 레터링, 청록/자주색 포인트. 표기는 LUX//FAUNA이며 두 줄로 구성.
- 멤버의 흑백 처리, 로고 철자 및 // 포함 여부는 생성 결과에서 시각 확인했다.
- 두 파일의 알파 채널과 투명 픽셀 존재를 파일 검사로 확인했다. 실제 Unity 배경에서의 가장자리, 축소 가독성, 임포트 필터 설정은 아직 검증하지 않았다.
- 로고는 포스터에 합쳐 저장하지 않았다. Unity에서 별도 Image로 크기/위치를 조절할 수 있다.

## 프롬프트

### Stage 2 편집

```text
Use case: precise-object-edit. Edit the supplied Stage 2 pixel-art punk concert poster, retaining the composition exactly.
ONLY requested change: apply a high-contrast BLACK-AND-WHITE XEROX / punk-zine printed-photo treatment to the THREE BAND MEMBERS and the instruments they hold/play. Convert all of their colored areas to neutral black, white and a small number of greys: raccoon's gold crown and orange guitar, raccoon fur and tongue, hedgehog's brown quills and skin, wooden drumsticks, skunk's fur/outfit and orange bass neck. All three musicians including their instruments must read as one monochrome cutout-band-photograph inside the colored punk poster. Emphasize chunky black shadows and limited white/grey pixel highlights, with a little ordered pixel dithering; keep facial expressions and anatomical detail readable, not crushed into featureless blobs.
INVARIANTS: preserve the exact three character identities, sizes, poses, faces, instruments, placement, tails and existing cutout contours. Keep the original warm CREAM blank paper areas, RED collage accents, BLUE tape, pins, basement backdrop, torn edges, text-safe empty regions, proportions and framing unchanged. Do NOT desaturate the entire poster. Keep ALL existing text areas empty; no added words/logo/labels.
Maintain clearly stepped PIXEL ART edges and existing pixel density; no painting, smooth airbrush or photographic grain. Preserve existing alpha transparency outside the artwork. Deliver same portrait 1024x1536 PNG, whole uncropped poster.
```

### LUX//FAUNA 로고

```text
Use case: logo-brand. Create ONE original transparent-background PIXEL ART wordmark for the fictional retro electronic music duo named exactly "LUX//FAUNA". This is a separate logo sprite to overlay on their midnight-teal and gold concert poster in a 2D pixel-art animal rock-band game.
Text (verbatim): "LUX//FAUNA". Spell L U X / / F A U N A. Both slashes are required. No other words, no tagline, no edition number.
Art direction: the warmth and swagger of a late-1970s record-sleeve logo reinterpreted as a famous electronic duo's emblem. Broad bold custom hand-lettered funk lettering with a confident forward lean, rounded-square counters, connected flowing strokes and restrained long terminals. Not a standard italic font. The paired // cuts are a strong signature, like twin slanted tape splices. Energetic but luxurious, recognizably retro rather than contemporary esports. Give it an ORIGINAL letter construction, not a traced or copied Daft Punk logo.
Composition: a single compact cohesive wordmark, "LUX//" on a shorter upper line and "FAUNA" below, both flowing together as one emblem; wide overall silhouette around 1.8:1. Large, readable, optically balanced. No surrounding badges, boxes, ellipses, decorative extra icons, helmets or characters. Entire logo and flourish ends within canvas with generous transparent margins.
Pixel medium: deliberate visible square pixel clusters and stair-step contours, like a high-quality approximately 360x200 pixel logo enlarged cleanly. Crisp edges, no antialias smoothing, no photographic material, no vector output. Use broad creamy ivory/champagne letter faces, a few hard-edged gold metallic reflection bands, near-black offset depth/outline, and very restrained thin cyan and magenta offset shadow accents referencing the duo's two visor colors. Keep letters mostly light for readability on the dark poster. No soft neon glow, no smooth gradients, no fuzzy sparkle field, no small noisy bevel detail. This is stylized retro chrome translated into pixel art, not generic futuristic spiky metal typography.
Output: ONE finished logo, no presentation sheet or mockup. Genuine transparent alpha outside every letter, shadow and flourish and inside counters. Do not draw a background/checkerboard, stage, paper or poster. Keep the text readable even when displayed about 400 pixels wide.
```

