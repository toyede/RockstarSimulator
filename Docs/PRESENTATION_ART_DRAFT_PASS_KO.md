# 공연 화면·보스·아트 초안 적용 기록

## 적용 및 검증

- [Main](../Assets/Scenes/Main.unity): 기존 밴드 Y=-0.5와 LUX_Truss 위치 보존. 배경 → 장식 관객 → 무대 오버레이 → 실제 관객/밴드 순으로 분리. 튜토리얼 중앙 한 명이 왕관에 가리지 않는 장면을 Editor에서 확인.
- 관객 퇴장: 관심 소진 시 Calm 애니메이션으로 화면 가장자리까지 걸어 나가는 기존 방식 유지. 이동 속도 0.85→2.1. Stage 4 표본에서 약 2.63초 이동 경로 확인.
- 보스: 기존 1101_art 공연 프레임 6장 사용. Play Mode에서 라이벌 카메라 전환·클릭으로 복귀 및 프레임 교대 확인. 확대된 로고가 있는 포스터 렌더 확인. 미션 성공/실패 UI는 기존 동작이 있어서 추가 아트 없이 유지; 표시 이벤트를 각각 주입해 문구 확인.
- 임시 BGM: 튜토리얼 1·2 So Bueno, Stage 4 Summer Nights. 기존 파일 재사용. Play Mode에서 튜토리얼 및 Stage 4 실제 AudioSource 확인. 전체 음량·효과음 믹스 청취 및 일반 공연 전환 재검증은 별도.
- 너구리: 기본형·기존 PNG 보존. 기쁨/화남/슬픔 초안은 [CharactersStanding](../Assets/Sprites/CharactersStanding/)에 추가하여 DialoguePortraitCatalog로 연결. 기쁨 대화 렌더, 전 표정 ID 매핑과 Point 임포트 확인.
- [배드엔딩 초안](../Assets/Sprites/UI/Ending_Bad_LoadOut_Draft.png): 스컹크 합류 후의 결말 대화에만 연결. TourHub Play Mode에서 합류 전·후 분기와 합류 후 일러스트, 중복 스탠딩 미표시 확인. 결말 요약 화면과 판정은 변경하지 않음.

## 추가 아트 및 남은 검증

- 엔딩: 전 공연 S 랭크, 크라우드서핑, 떼창, 첫 싱글 4장. 합류 전 배드엔딩 전용 그림을 별도로 만들면 2인 구도로 작업.
- 동료 무대: 고슴도치·스컹크 기본/당황/피버 각 3상태. 장비와 발·의자 기준점을 프레임 사이 고정. 스탠딩은 정지 상태와 호환되는 눈깜빡임/호흡 프레임.
- 결말 요약 화면은 통계와 타이틀로 돌아가는 동작을 유지하면서 레이아웃과 타이포를 디자인할 것.
- 카드와 스페셜 관객 기획/밸런스는 이번 변경에서 보존. 신규 플레이어로 몰입 단계별 선택 근거와 첫 공연 진행을 확인한 뒤 결정.
- 출시 QA: 전 스테이지 가림·종료 전환, 브라우저/실기기 입력, 빌드 로딩·텍스처 메모리, 음악·효과음 믹스. 이번 작업은 빌드, 커밋, 푸시, 점수 업로드를 하지 않음.

## 아트 생성 프롬프트

내장 imagegen 사용. 초안만 등록했고 최종 아트 교체 시 카탈로그 연결을 갱신하면 된다.

### 너구리 기쁨

```text
Use case: precise-object-edit
Asset type: Raccoon Roll pixel-art dialogue sprite, joyful reaction draft.
Image 1 is the adopted default raccoon. Edit ONLY the eyes, eyebrows and mouth inside the face. Give him a clear but modest pleased smile for the happy/relieved dialogue: short softly lifted mouth corners, just a small dark gap in the smile, no visible tongue; both eyes gently curve and narrow with joy, naturally aligned gaze, no large round staring eyes, no squint or exaggerated grin. Keep his face angle, ear and mask shape, hair, crooked rusty crown, hands-behind-back pose, clothing, tail, all full-body proportions, feet placement, character scale, hard pixel edges, and palette exactly as the reference. Preserve the full 2:3 framing and a genuine transparent alpha background. Do not add glow, objects, or text.
```

### 너구리 화남

```text
Use case: precise-object-edit
Asset type: Raccoon Roll dialogue portrait, Angry expression draft.
Image 1 is the exact adopted raccoon edit target. Redraw only his eyes, eyebrows and mouth. A quiet, believable frustrated expression. Slightly draw the brows down toward the bridge, gently narrow the upper lids, and use a short closed tense mouth with one corner dipped. Keep the pupils naturally aligned as in the reference. A shy character trying to be firm, not a villain: no snarl, teeth, rage marks or exaggerated squint.
Lock the same head angle, ears, hair, charcoal mask, cheek silhouette, crooked rusty crown, pose, full-body composition, pixel edges, clothes, ringed tail and palette. Keep the hands behind his back and feet in the same place. True alpha transparency around the full sprite. No new elements, text or backdrop.
```

### 너구리 슬픔

```text
Use case: precise-object-edit
Asset type: Raccoon Roll dialogue portrait, Sad expression draft.
Image 1 is the exact adopted raccoon edit target. Redraw only his eyes, eyebrows and mouth. A subdued sad, disappointed expression. Gently lift the inner brows, let the eyelids soften and glance a little lower without crossing, and use a small closed downturned mouth. Restrained, vulnerable sadness rather than melodramatic crying. No tears, gloss streaks or cartoon symbols.
Lock the same head angle, ears, hair, charcoal mask, cheek silhouette, crooked rusty crown, pose, full-body composition, pixel edges, clothes, ringed tail and palette. Keep the hands behind his back and feet in the same place. True alpha transparency around the full sprite. No new elements, text or backdrop.
```

### 배드엔딩

```text
Use case: illustration-story
Asset type: full-screen landscape 16:9 pixel-art bad-ending dialogue backdrop for Raccoon Roll.
Input 1 identifies the small shy raccoon in a tarnished crooked crown, striped scoop-neck shirt and patched loose jeans. Input 2 identifies the rugged tan hedgehog drummer with swept long quills and olive utility vest. Input 3 identifies the taller skunk bassist with a white swept mane, black leather jacket over a white tank, and a large striped tail. Use their exact visual identities and outfits, not their standing poses.
Scene: the gig has been stopped. A quiet side exit of a basement music venue at night, with an open utilitarian metal door, a few road cases and a faint warm glow inside. The raccoon and hedgehog hold opposite handles of the same closed black drum road case, helping carry it out together. The skunk stands a step back, holding the door for them. Three characters only. Make the case weight believable, with simple three-digit paw shapes where fingers show. Tired and disappointed but still helping each other, no tears or jokes, no audience.
Composition: readable mid-wide side view; keep all three heads and the carrying gesture in the central 55% of the width and above the lower quarter, because a dialogue box overlays that area. Leave the left and right edges quieter for in-game portraits. Hand-placed sharp pixel clusters, stepped shading and subtle restrained amber/cool blue color contrast, consistent with the references. No lettering, no logos, no UI, opaque full scene.
```
