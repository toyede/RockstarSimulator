# 공연 예고 포스터 — Stage 2 / Stage 5 픽셀 아트 초안

제작일: 2026-10-08  
생성 방식: 기본 제공 imagegen 도구. CLI/API fallback 미사용.  
범위: 문구 없는 포스터 PNG 2종. Unity 씬, 프리팹, 스크립트, 기존 에셋 참조는 변경하지 않음.

## 파일

| 파일 | 크기 | 방향 |
| --- | --- | --- |
| Stage02_PunkPoster_v1.png | 1024 × 1536 | 찢긴 크림색 종이, 빨강/파랑 포인트, 지하 클럽의 3인조 펑크 콜라주 |
| Stage05_LuxFaunaPoster_v1.png | 1024 × 1536 | 기존 동물 헬멧 디자인을 반영한 청록/금빛 LUX//FAUNA 초대형 광고 |

## 텍스트 배치 메모

공통 양식에 맞추지 않고 각 포스터의 여백을 사용한다. 아래 위치는 가이드이며 고정 RectTransform 사양이 아니다.

### Stage 2

- 위쪽 큰 크림색 종이: 공연명 / 밴드 로고.
- 아래쪽 넓은 크림색 종이: 특별 관객 기믹 설명.
- 아래 오른쪽 작은 종이: 목표 점수 / 시간 등 짧은 정보.
- 기울어진 장식과 달리 한글 본문은 거의 수평으로 배치한다.
- 등장인물: 왕관 쓴 너구리 기타리스트, 고슴도치 드러머, 스컹크 베이시스트. 얼룩말이 아님.

### Stage 5

- 위쪽 왼편의 어두운 여백: 커다란 LUX//FAUNA 로고.
- 아래쪽 중앙의 어두운 여백: 배틀 기믹과 승리 조건.
- 하단 한쪽 작은 자리: “…vs Raccoon Roll”과 별도로 불러온 기존 타이틀 로고.
- 상대 이름과 우리 이름의 홍보 비중은 크게 차이 나게 하되, 게임 규칙 본문은 읽기 쉽게 유지한다.
- 기존 캐릭터의 각진 청록 바이저 헬멧 / 둥근 자주색 바이저 헬멧을 참고했다.

## 확인한 것 / 남은 것

- 두 파일 모두 1024 × 1536, 32-bit ARGB PNG임을 확인.
- 생성 결과를 눈으로 검토: 3인조 / 보스 2인조 구분, 스컹크 포함, 픽셀 스타일, 텍스트용 여백, 문구 미포함 확인.
- 알파 채널이 존재한다. 8픽셀 간격 표본에서 외곽의 alpha 0과 이미지 내부의 주로 252~253 값을 확인했다. 완전 불투명 내부 + 이진 알파 경계로 정규화된 제작용 이미지라고 보장하지 않는다.
- 원본은 생성형 픽셀 일러스트 초안이며, 정확한 저해상도 격자/인덱스 팔레트 규격의 수작업 도트 에셋은 아니다.
- Unity 임포트, Point 필터·압축 설정, 실제 UI 크기에서의 한글 배치, 어두운 배경 위 가장자리/알파, 등장 애니메이션은 아직 검증하지 않았다.
- 기존 아트는 덮어쓰지 않았으며 최종 연결은 사용자 검토 후 별도 진행한다.
- Stage 2 외곽 배경 정리 편집을 1회 수행했다. 생성 도구 미리보기는 알파 바깥의 RGB가 보일 수 있으므로 투명 여부는 실제 파일 알파도 함께 확인했다.

## 최종 생성 프롬프트

### Stage 2 — 기본 생성

```text
Use case: ads-marketing. Asset: one portrait PIXEL ART concert announcement poster background for the game Raccoon Roll, approximately 2:3 aspect ratio, PNG. Generate a new original fictional-band punk poster using the supplied images only as references, not as a screenshot collage.
Input reference roles: images 1 and 2 are the game's raccoon guitarist and hedgehog drummer character identity references; images 3 and 4 are Television and Sex Pistols printed-poster composition references. Use the design language of underground xerox gig posters, never the real artists, their lettering, photographs, logos or printed words.
MANDATORY ART STYLE: authentic visible square-pixel illustration, looks authored on a roughly 256x384 pixel grid and cleanly nearest-neighbor enlarged. Bold stepped silhouettes, deliberate pixel clusters, 2-3 tone shading, sparse ordered pixel dithering. NO smooth illustration with a pixel filter, NO painterly shading, NO antialiased curves, NO tiny photographic grain.
A stylish aggressively asymmetrical cream paper punk collage, charcoal black, desaturated blue, sharp vermilion-red accents. Middle area: high-contrast black-and-cream cutout 'band photograph' DRAWN IN PIXEL ART of exactly THREE anthropomorphic animal musicians in a cramped basement club entrance: the recognizable small gold-crowned grey RACCOON, cheerful/trying-to-look-cool, orange electric guitar; a stocky spiky brown HEDGEHOG drummer in sleeveless black punk clothes holding drumsticks; and a cool older black-and-white SKUNK bassist with long white crest and broad striped bushy tail, dark leather clothes, holding a bass, dry unimpressed expression. The skunk is NOT a zebra. The three form a lively diagonal, overlapping cutout silhouettes, instrument details economical. Faint basement steps, brickwork and a box amp ground the venue behind them. Avoid extra musicians and human figures.
Graphic design: a large organically empty cream headline area in the upper part, surrounded asymmetrically by a red torn edge and a small blue tape fragment; imagery occupies the middle; in the lower portion one large irregular torn off-white paper scrap at nearly horizontal angle provides clean space for 3-4 lines of future Unity text, plus a smaller adjacent low-detail paper margin for future goal/time. These should feel like actual blank unprinted parts of a punk poster, NOT rectangular UI widgets, NOT a standardized template or form. Small safety pins/tape corners and rough offset color silhouettes limited to the perimeter; retain a strong art composition despite having no lettering. Keep the text-safe regions spacious, light and almost texture-free.
Text: ABSOLUTELY NO TEXT, no letters, numbers, logos, pretend writing, ruled placeholder lines, barcodes, signatures, watermark or button labels. Unity will overlay ALL text and logos later. No external game screenshot, characters standing behind the poster, explanation labels or mockup background.
Output: one complete flat front-facing poster artwork, ragged stepped pixel paper edges and tape tabs fully inside frame with modest padding. Genuinely TRANSPARENT ALPHA outside the poster/tabs only; opaque cream paper and opaque artwork within. No wall, tabletop, fake checkerboard, photographic drop shadow, or cropped paper.
```

### Stage 2 — 외곽 투명화 편집

```text
Edit the supplied generated pixel-art punk concert poster. Change ONLY the background outside the ragged paper silhouette and its attached tape tabs/safety pins: remove the blurry colored red/grey/blue backdrop entirely and replace it with genuine transparent alpha=0. Preserve EVERY interior detail, the three animal musicians, their faces/instruments, the cream blank text regions, colors, pixel edges, composition, dimensions, and the entire paper silhouette exactly. Do not redesign, redraw or add anything. Opaque cream paper stays opaque. Keep all safety pins, blue tape and red torn under-paper. Clean pixel-stepped boundary with no white/color halo, no soft drop shadow, no checkerboard image. No new text or logo. Output the same full uncropped portrait PNG with truly transparent surroundings.
```

### Stage 5 — 기본 생성

```text
Use case: ads-marketing. Asset: one portrait PIXEL ART mega-star concert battle announcement poster background for the game Raccoon Roll, approximately 2:3 ratio, PNG. Generate a new composition based on the supplied references.
Input roles: image 1 is the ACTUAL LUX//FAUNA fictional duo character design and must control identity; image 2 is only a reference for large dramatic electronic-music poster cropping, metal highlights and asymmetric luxurious composition. Do NOT depict the human reference musicians or reproduce their helmets/logo.
MANDATORY STYLE: authentic clearly visible square pixel art, roughly 256x384 pixel authored feeling enlarged with nearest-neighbor. Strong pixel clusters, deliberate stair-step contours, hard-edged stepped metal reflections, restrained ordered dithering, no smooth gradients, NO photorealism, NO painterly art, no vector curves, no blurry pseudo-pixel filter, no film grain.
Two massive upper-body portraits of the game's electronic duo loom over the page: the taller member in the reference wears an angular silver/black FOX/WOLF-EARED helmet with an electric CYAN visor, black fitted stagewear and crossed arms; the other wears a rounded black BEAR-EARED helmet with PURPLE/MAGENTA visor, black fitted stagewear. Preserve both distinctive animal helmets, their colors and recognizable silhouettes. Low angle, overlapping diagonal shoulders, immense confident star presence; bodies can crop at the sides but helmets remain readable. Their metal surfaces catch luxurious pale GOLD and amber edge reflections against deep midnight TEAL/BLACK. Keep cyan/purple identity accents. Use restrained chunky 4-point pixel glints rather than soft bloom.
FREE EDITORIAL COMPOSITION, not a generic game panel: duo occupies the dominant central-to-right region, one helmet nearer and larger; a sweeping angular unlit dark-teal area through the upper-left/top provides generous space for a HUGE future LUX//FAUNA logo, no drawn border. Beneath their shoulders the poster descends into large calm near-black lower negative space for several lines of future readable white gameplay text; a narrow stadium horizon with tiny warm lights can graze one side without polluting this copy zone. At a lower corner leave one MUCH SMALLER incidental empty space for the future '...vs Raccoon Roll' treatment, communicating the underdog's tiny billing. The real logo/text will be added in Unity, so do not print it. No literal empty white boxes, panels, labels, placeholder lines, UI frames or button graphics. Use diagonal light and portrait cropping to make the artwork dynamic, not stacked template regions.
Materials: expensive pristine concert advertising, deep opaque ink, hard specular pixel gold; absolutely no distressed punk paper, torn scraps, safety pins, or tape. Visually opposite to the Stage 2 DIY collage while retaining the same game's pixel medium.
Text: absolutely NO text, letters, numbers, symbols used as type, logos, brand names, signatures, watermark, lorem ipsum or fake writing. No real band logo. No raccoon cameo portrait (a tiny Unity logo will be added separately).
Output one complete flat front-facing rectangular poster artwork, whole perimeter visible within small transparent padding, no perspective mockup. True TRANSPARENT ALPHA only outside the poster silhouette; poster interior remains fully opaque midnight teal/black. No wall, desk, checkerboard, outer scene or other UI.
```

## 생성 원본

- Stage 2 기본: C:/Users/FOR/.codex/generated_images/019f95ab-251d-7ed3-96f9-7faf833131ea/exec-c32fc999-1c95-4719-96d7-1dfef2d52f3c.png
- Stage 2 선택본: C:/Users/FOR/.codex/generated_images/019f95ab-251d-7ed3-96f9-7faf833131ea/exec-9204dac5-b43f-4d1e-9927-0fb75f058ae7.png
- Stage 5 선택본: C:/Users/FOR/.codex/generated_images/019f95ab-251d-7ed3-96f9-7faf833131ea/exec-35601a74-c763-4e0d-80c7-d263a9bf9ec3.png

