# 공연 포스터 수정 v2 — 전단 / 베이스 / 토끼 호스트

제작일: 2026-10-08  
도구: 기본 제공 imagegen. CLI/API fallback 미사용.  
기존 파일을 덮어쓰지 않았으며 Unity 씬/코드는 수정하지 않았다.

## Stage 1

- **Stage01_HandbillBase_v2.png**: 기존 밴드·가로등·골목 등 모든 삽화를 제거한 전단 바탕. 종이/테이프/절취선만 남김.
- **Stage01_HandwrittenTitle_v1.png**: “공연합니다. / 라쿤 스트리트” 2줄 검은 손글씨 투명 오버레이.
- **Stage01_HandbillPreview_v2.png**: 손글씨를 종이에 얹은 별도 생성 미리보기. 문자와 종이를 합친 완성 인상 참고용이며, 분리 파일의 픽셀 단위 합성이 아니라 imagegen을 통한 시각화다.
- 타이틀까지 Unity 텍스트로 작성할 경우 Base만 쓰면 된다. 고정 손글씨 타이틀을 사용할 때는 Title 이미지를 별도 Image로 올리고 설명은 Unity 텍스트로 유지한다.
- 검은 투명 글씨는 검은 배경의 이미지 뷰어에서 보이지 않을 수 있다. 밝은 종이 위에서 확인한다.

## Stage 3

- **Stage03_FestivalBassPoster_v2.png**
- 빨간 바탕과 기존 축제 구성 유지.
- 큰 악기를 4현 베이스로 변경. 헤드의 4개 튜닝 페그/포스트를 시각 확인했다.
- 프렛을 잡는 손을 구분되는 손가락 형태로 다시 생성했다. 생성형 일러스트이므로 악기 연주 자세의 정밀한 해부학/주법 검수까지 마친 것은 아니다.
- 하단 녹색 설명 여백 유지, 모든 문구는 비워둠. 불투명 PNG.

## Stage 4

- **Stage04_NightShowRabbitHost_v2.png**
- 기존 3인조와 방송 세트는 유지하고, 책상 뒤에 늙은 고전 만화풍 토끼 호스트 추가.
- 회색 털, 긴 귀, 익살스러운 표정, 버건디 재킷과 나비넥타이.
- 토끼는 밴드 멤버가 아니라 진행자이며, 총 등장인물은 4명.
- 상단 타이틀 공간과 하단 큐시트 문구 공간 유지.

## 확인 범위

- 모든 파일 저장 및 크기·알파 표본 확인.
- Stage 1 미리보기의 한글 문구, Stage 3 베이스 헤드, Stage 4 호스트 위치와 인물 수를 시각 확인.
- 알파가 있는 생성물의 내부/외곽은 부분 불투명 픽셀이 포함될 수 있다. 실제 Unity의 배경 위 프린지와 가독성은 아직 검증하지 않았다.
- 씬 연결, 텍스트 배치, 등장 애니메이션은 이번 작업 범위에 포함하지 않았다.

## 생성 프롬프트

### Stage 1 바탕

```text
Use case: precise-object-edit. Edit the supplied Stage 1 pixel-art flyer into a VERY SIMPLE homemade street-show handbill, with absolutely NO illustration.
Remove ALL musicians, animals, instruments, lamp, light beam, bricks, pipes, pavement, amp, guitar case, plants and scenery. Fill their former locations with the same continuous off-white paper. Keep only the paper itself, two little masking-tape pieces, subtle folding, lightly worn irregular pixel edges, and a simple bottom tear-off fringe with one tab missing. REDUCE the heavy stains and aged-parchment look: this is ordinary cheap slightly crumpled cream photocopy paper made by a scrappy band, not an ancient scroll.
The entire central paper area must be clean and uninterrupted, ready for large wonky handwritten Korean lettering to be added separately. No ornamental frame, separate copy boxes, separator lines, clipart or drawings. No text at all, not even placeholders. The intended future title is an amateur hand-scrawled '공연합니다. / 라쿤 스트리트', but DO NOT render it on this base image.
Preserve the current portrait proportions, whole flyer framing, pixel-art medium, torn silhouette and transparency outside the paper/tape. Make the surface opaque and calm. 1024x1536 PNG. No background scene or mockup.
```

### Stage 1 손글씨

```text
Use case: logo-brand. Generate a separate transparent PNG title overlay for a homemade PIXEL ART street-concert flyer, not a polished brand logo.
Render EXACTLY these TWO Korean lines, no additional text:
Line 1: "공연합니다."
Line 2: "라쿤 스트리트"
Check each syllable: 공 / 연 / 합 / 니 / 다 / . ; 라 / 쿤 / [space] / 스 / 트 / 리 / 트.
Style: clumsy but readable Korean handwriting with a thick black felt-tip marker. Endearingly uneven character sizes, irregular spacing, baseline a little crooked, slight changes of slant, as if a young amateur band wrote its notice by hand. Not brush calligraphy, not a printed geometric font, not perfectly centered graphic design, not child scribbles, not graffiti. First line a little bigger than the second. Make the lettering angular and stepped in visible pixel-art clusters consistent with a 2D pixel game; no smooth vector curves or antialiased script. Dark charcoal ink only, a few small pixel gaps in strokes at most; readable Hangul blocks, all syllables intact.
Only the two lines of ink on TRUE TRANSPARENT ALPHA, including gaps and counters. Absolutely no white or cream paper/background, no shadow, no border, no illustration, no icons, no watermark. Generous transparent padding so no glyph is clipped. Wide approximately 3:2 image.
```

### Stage 1 조합 미리보기

```text
Use case: compositing. Image 1 is the blank paper flyer base; image 2 is a transparent black handwritten title overlay. Create ONE readable assembled preview of this street-show flyer.
Keep Image 1 exactly: same simple worn pixel paper, tape, tear-off bottom tabs, empty lower portion and transparent surroundings. Place the black handwritten lettering from Image 2 in the upper third / upper-middle of the paper, occupying roughly 75 percent of paper width, with natural uneven baseline. Do not place the transparent image's black preview backdrop: composite only its opaque ink strokes using alpha.
The title must read exactly TWO lines:
"공연합니다."
"라쿤 스트리트"
If the supplied overlay's glyphs are not legible, cleanly redraw these same exact Hangul syllables as wonky thick black marker handwriting, keeping the pixel stepped style. No misspelling: 공 연 합 니 다 . / 라 쿤 [space] 스 트 리 트.
The feeling is an amateur band hastily scrawled this notice with a marker. Not a typeset font, not elegant calligraphy, not a polished logo. No other text, illustrations, musical doodles, extra panels, lines, icons or scenery. Leave at least the lower half of the paper blank for later gameplay information. Front-facing full portrait 1024x1536, no crop. Preserve alpha beyond paper edges; opaque paper behind black letters. This is only a presentation preview, the title and base are also delivered separately.
```

### Stage 3

```text
Use case: precise-object-edit. Edit the supplied red festival PIXEL ART poster. Change ONLY the foreground instrument and its fretting hand.
Replace the current six-string guitar neck/headstock with an unmistakable FOUR-STRING ELECTRIC BASS neck and headstock, retaining the cyan-blue body of the neck/headstock, cream frets/strings, yellow tuning keys and current diagonal placement. Exactly FOUR parallel strings, exactly FOUR tuning machines/pegs and FOUR string posts, NOT six, not five. Use a slightly long bass headstock and wider spaced bass strings. No instrument brand or letters.
Rebuild the single large cream furry fretting hand with clear credible anatomy. It is ONE animal musician's left hand naturally wrapping a bass neck: exactly FOUR visibly distinct curved fingers (index, middle, ring, little) resting over the front of the fingerboard at staggered nearby frets. The thumb is behind the neck and mostly occluded, not an extra hook beside the four fingers. Fingers connect to ONE palm/wrist entering from the left. Clear separations and knuckle steps, shorter little finger, no fused mitten, duplicated knuckles, dangling disconnected finger, six-finger hand, thumb on the wrong side or fingernails floating in space. The fingers press strings instead of surrounding them like a fist crushing a pole. Simplify into confident cream and tan PIXEL shapes for readability.
INVARIANTS: preserve the dominant RED background, bird, flag, yellow sun, small festival stage/tent/crowd, green lower copy space, all empty future-text areas, style, palette and composition. No typography or new UI. Preserve crisp pixel edges and full opaque rectangular background with no transparency, no blurred black patches or vignette. Same portrait 1024x1536.
```

### Stage 4

```text
Use case: precise-object-edit. Add ONE elderly cartoon RABBIT TALK-SHOW HOST to the supplied pixel-art nighttime TV poster, seated naturally BEHIND the existing wood host desk on the RIGHT, facing the three animal band guests.
Character: a tall-eared grey rabbit with cream muzzle and cheek tufts, expressive half-lidded eyes, raised bushy silver eyebrows, slight wrinkles at eye corners, large friendly front teeth, one long ear subtly bent, a knowing comic grin. The energy of a classic mid-century American theatrical cartoon rabbit, reminiscent of a Bugs Bunny-like wisecracking host, but an original ELDERLY host design rather than a literal unchanged licensed character. Dress him in a burgundy dinner jacket, cream shirt and small dark bow tie. Seasoned veteran broadcaster, playful not sinister. Sitting upper-body view, one hand resting on desk, the other gesturing open-palmed toward his band guests; simple clear cartoon finger anatomy. His head/ears must fit in the right-side window area without invading the broad empty upper-left title region.
Keep the original crowned raccoon, hedgehog and skunk trio, their instruments/expressions/positions, sofa, microphone, mug, desk location, curtains, skyline, moon, studio light, camera, paper cue-sheet, palette and PIXEL ART rendering unchanged. Do not add a fourth band member. The host is the fourth character in total, distinct and separated by the desk. Retain all blank text-safe areas including the large ivory cue sheet. No text, logos, placeholder writing or extra props. No painterly detail, smooth outlines or realistic fur; use the same pixel sizes, limited shading and crisp clusters as the band. Preserve existing transparency and full portrait framing, 1024x1536 PNG.
```

