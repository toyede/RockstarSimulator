# Stage 3 주름 제거 v4

기본 제공 imagegen으로 사용자가 표시한 앞발 안쪽의 짧은 갈색 주름과 빨간 마크업을 제거했다.
결과 파일: Stage03_FestivalRabbitPaw_v4.png
시각 확인: 표시 영역의 주름/마크업 제거. 기존 v3 보존. Unity 연결 변경 없음.

## 최종 프롬프트

```text
Use case: precise-object-edit. Make ONE tiny local edit to the attached annotated pixel-art festival poster. The user's red outline highlights a small vertical tan WRINKLE/CREASE INSIDE the upper curved cream rabbit finger/paw, around x=36-40% and y=51-54% of the image. REMOVE that interior tan crease entirely and fill it with the adjacent clean light-cream paw color so this patch is unwrinkled. Erase ALL of the red hand-drawn annotation around that little patch too.
Keep the paw's outer silhouette, its chubby proportions, existing finger separations and essential edge shadows unchanged. Do NOT redesign the hand, do NOT erase its outline or merge the fingers, do NOT restore any dangling fur under the bass. Change nothing outside this small marked area.
Preserve the complete existing artwork: pixel grid, four-string blue bass and four tuners, paw, red background, green meadow, bird, flags, stage, tent, crowd, original colors, empty typography areas, layout, framing and 1024x1536 portrait resolution. Full opaque background, no added text, labels, logos or other marks.
```

