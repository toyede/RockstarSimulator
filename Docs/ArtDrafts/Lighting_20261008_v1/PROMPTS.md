# 조명 아트 — 실제 사용한 생성 프롬프트

2026-10-08 · 내장 image_gen 사용 · 선택본 전부 transparent_background=true.

새 기구/스모그 생성에는 참조 이미지 파일을 전달하지 않았다. 기존 스테이지 아트를 먼저 육안 조사한 뒤 팔레트와 형태를 프롬프트에 반영했다. 스모그와 기구는 별도 PNG로 생성했으며, 스모그는 중성 tint 베이스, ON AIR는 글자 없는 프레임으로 요청했다.

## VFX_PixelSmog_Neutral_v1.png

선택된 생성 원본 파일명: exec-1a340903-a294-4111-8d63-bbbce8b0c971.png

```text
Use case: stylized-concept. Asset type: one isolated grayscale pixel-art smoke particle sprite for Unity URP 2D concert lighting in Raccoon Roll. Primary request: a single low, horizontally spreading stage-smog wisp, for tinting and fading with a particle system. Subject: irregular connected rounded puffs and wispy edges with a little upward curl, lighter central billows fading into thin edges, asymmetrical organic shape, roughly twice as wide as tall. Style: genuine coarse hand-placed pixel art, a coherent approximately 64 by 64 logical pixel grid, visible blocky stepped edges and clustered pixels, restrained dithering, no smooth painterly brushwork, not a decorative outlined cartoon cloud. Palette: only neutral white and light gray, with genuine partly transparent low-opacity edge pixels; no black outlines, no colorful pixels, no baked colored illumination, no dirt or stars. Composition: one complete smoke wisp centered, occupy around 60 percent of canvas width, generous fully transparent padding on every side, nothing touching canvas boundaries. Constraints: actual transparent background with alpha, never paint a checkerboard or white/black backdrop; do not draw any equipment, no floor, no stage, no text, no glow halo, no shadow. This is a reusable standalone VFX texture, not a mockup or sprite sheet.
```

## ST02_CeilingPAR_Unlit_v1.png

선택된 생성 원본 파일명: exec-f1c66abe-08af-4c42-92b4-1f75d42cfa31.png

```text
Use case: stylized-concept. Asset type: one isolated pixel-art industrial ceiling PAR spotlight fixture sprite for Raccoon Roll, a 2D pixel animal rock-band game. Primary request: a cheap slightly worn ceiling-mounted concert PAR can for a cramped basement live hall. Subject: one squat cylindrical metal stage light in a U-shaped mounting yoke, short ceiling clamp and a small cable, facing forward and tilted down so the round glass lens is visible as an ellipse. Slight three-quarter view matching a band seen from behind facing the audience, dark rugged metal casing, a few meaningful vents and tiny screws, compact silhouette, no tripod and no floor stand. Style: crisp authentic coarse pixel art on a consistent approximately 64 by 64 logical pixel grid, stepped edges, clustered shading, restrained dither, no anti-aliased vector contour and no smooth gradient. Palette: #2e222f, #3e3546, #625565, #7f708a, #9babb2, muted warm-gray metal accents #ab947a; lens is inactive dark gray, not glowing red. Composition: single whole fixture centered with transparent padding at least 15 percent each side, yoke/clamp/cable fully visible. Constraints: true transparent alpha background; no checkerboard, no scenery, no light beam, no projected light pool, no glow, no cast shadow, no labels, no text, no duplicate variants or sprite sheet. The lens emission and light beam will be separate runtime effects, so keep this base sprite unlit.
```

## ST04_StudioSoftbox_Unlit_v1.png

선택된 생성 원본 파일명: exec-8d1357eb-1c3e-4f29-b769-6c21834ba7a7.png

```text
Use case: stylized-concept. Asset type: transparent isolated pixel-art base sprite of one POWERED OFF suspended rectangular television-studio lighting fixture for a Unity 2D animal rock concert game. Make an INACTIVE physical prop, not a lighting effect. Single wide rectangular gray softbox enclosure, recessed opaque MATTE MEDIUM GRAY diffuser panel that is DARKER than paper white, short ceiling clamp and metal yoke, compact small side barn doors, frontal three-quarter slight downward angle. The panel is not illuminated, not white, not emissive. Coarse genuine pixel art like a hand-drawn 64x48 sprite enlarged with nearest-neighbor: large discrete rectangular pixels, crisp stepped boundary, two or three flat shade clusters per surface, bold dark purple-charcoal edge pixels, restrained tiny screws and vents. Limited metal palette #2e222f #3e3546 #625565 #7f708a #9babb2; the inactive matte diffuser mainly #7f708a with #9babb2 highlights. Whole object centered with at least 15 percent truly empty transparent margins. Actual transparent RGBA cutout: every pixel outside the physical metal-and-panel silhouette must be alpha zero. Absolutely no atmospheric haze, no luminous halo, no aura, no fog, no soft shadow, no light bloom, no backplate, no environment, no background gradients, no checkerboard, no text, no other equipment, no sprite sheet. Do not light the background because there is no background. Clean opaque-object pixel sprite for separate runtime beam/brightness control.
```

## ST04_OnAirFrame_Blank_v1.png

선택된 생성 원본 파일명: exec-499e47b0-42a2-4ff3-8279-4a19384b3fcb.png

```text
Use case: stylized-concept. Asset type: one isolated empty pixel-art ON AIR broadcast-sign housing sprite for Raccoon Roll. Primary request: a blank horizontal broadcast studio indicator sign frame, a reusable background for dynamically rendered ON AIR text. Subject: compact wide rectangular dark charcoal-purple metal lightbox with a recessed EMPTY very dark plum central display window, layered bevel border, four small corner screws, very small inactive status indicator on one side, tidy squared-off retro hardware. Width-to-height around 3 to 1; central display large enough for six letters in a pixel font to be added by the game later. Absolutely NO letters or symbols anywhere: do not print ON AIR, numbers, pseudo-text, decorative dashes, or labels. Style: crisp coarse hand-placed pixel art, logical resolution around 96 by 32 pixels, stepped edges, solid limited palette, restrained clustered highlights, matching rugged stage props rather than cute interface buttons. Palette: #2e222f #3e3546 #625565 #7f708a #9babb2, display nearly black-purple, inactive status indicator muted dark red #6e2727. No bright light or red aura baked in. Composition: one whole sign centered, front facing with negligible perspective, transparent padding on every side, no mounting poles. Constraints: actual transparent alpha background outside the housing, central empty display is dark opaque; no checkerboard, no glow, no scenery, no shadow, no text, no variants or sprite sheet.
```

## 소프트박스 시도 기록

첫 생성은 밝은 diffuser 주변의 보라색 haze가 남았다. 배경/alpha만 정리하는 편집 1회를 시도했지만 잔상이 남아, 전원이 꺼진 medium-gray 패널 버전을 새로 생성해 선택했다. 선택본에도 미세한 낮은 Alpha 외곽이 있으므로 최종 하드 컷아웃·픽셀 축소가 완료된 것으로 취급하지 않는다. 중간 시도 파일은 프로젝트 납품 폴더에 포함하지 않았다.

사용한 편집 프롬프트:

```text
Use case: background-extraction. Edit target: the attached pixel-art suspended rectangular studio softbox. Change ONLY the background and alpha cutout. Remove every purple haze, glow, drop shadow, soft fog and low-opacity speck OUTSIDE the physical metal fixture silhouette, including around the ceiling clamp, yoke and barn doors. Preserve the entire existing equipment exactly: same pixel geometry, proportions, perspective, mounting clamp, screws, dark-purple metal shades and pale diffuser texture. Keep the original image framing and all physical parts intact. Do NOT redraw or brighten the diffuser; do NOT add effects, text, beams or scenery. Make the fixture itself solid and its pixel-stepped outer edges sharp; every background pixel outside the fixture must have alpha exactly zero, no translucent halo. Genuinely transparent RGBA output, no checkerboard, no colored background. This is a runtime sprite base that must not have lighting baked around it.
```

