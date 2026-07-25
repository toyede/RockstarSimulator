# 주인공 · 밴드 애니메이션 (CONTEXT STAGE)

너구리(Raccoon)는 **Idle 루프 → 카드 사용 시 Stroke/GuitarSolo 1회 → 자동 Idle 복귀**,
밴드(Friends)는 무한 루프. Animator/AnimatorController 에셋 없이 공용 `SpriteSheetAnimator` 로 돌린다.

## 1. 실행 순서

```
1) Tools/Art/Fix Character Sprite Import     ← 임포트 설정 교정
2) Tools/Art/Setup Raccoon And Friends       ← 씬 배치 + 프레임 자동 연결
3) Scene 뷰에서 위치 조정 → Ctrl+S
```

1번은 `spriteMode = Single`(조각 슬라이스 제거) · pivot Center · PPU 지정에 더해
**filterMode = Point · 무압축 · alphaIsTransparency** 를 적용한다.
둘 다 픽셀아트라 Bilinear 로 두면 도트가 뭉개진다.

## 2. 에셋

| 클립 | 폴더 | 프레임 | loop |
|---|---|---|---|
| `Idle` | `Sprites/Raccoon/Idle` | 3 | ✅ |
| `Stroke` | `Sprites/Raccoon/Stroke` | 14 | ❌ |
| `GuitarSolo` | `Sprites/Raccoon/GuitarSolo` | 24 | ❌ |
| Friends `Idle` | `Sprites/Friends` | 14 | ✅ |

> **`Stroke`/`GuitarSolo` 의 loop 는 반드시 꺼야 한다.** loop 가 켜지면 완료 콜백이 오지 않아
> Idle 로 돌아오지 못한다. `RaccoonAnimator` 의 `OnValidate` 가 이 실수를 잡아 경고한다.

임포트 설정 (툴이 자동 적용):

| 항목 | Raccoon | Friends |
|---|---|---|
| 원본 크기 | 834×1112px | 1280×720px |
| PPU | 222 → 화면 대비 **5유닛** 높이 | 72 → 16:9 **화면 꽉 채움** |
| spriteMode | Single | Single |
| pivot | Center | Center |

## 3. 카드 → 클립 매핑 (`RaccoonAnimator` 인스펙터)

우선순위: **CardId 개별 지정 → Role 기본값 → (비었으면) Idle 유지**

| 카드 | Role | 클립 |
|---|---|---|
| `guitar_solo` | Normal | **GuitarSolo** ← CardId 개별 지정 |
| `hands_up` / `pass_mic` / `open_mosh_pit` | Special | **GuitarSolo** |
| `tempo_up` / `response_call` | Normal | **Stroke** |
| `draw_two` / `reroll_hand` | Utility | **없음 (Idle 유지)** |

`guitar_solo` 는 Role 이 Normal 이지만 솔로 연주가 필요해서 `cardClipOverrides` 로 덮어썼다.
새 카드가 늘어나면 인스펙터의 이 리스트에 한 줄 추가하면 된다 — 코드 수정 불필요.

## 4. 타이밍 (전부 인스펙터)

| 항목 | 기본값 | 설명 |
|---|---|---|
| `fps` (클립별) | 12 | Idle 0.25초 / Stroke 1.17초 / GuitarSolo 2.0초 |
| `startDelay` | 0초 | 카드 낸 뒤 연주 시작까지 지연 |
| `interruptPolicy` | `AlwaysRestart` | 연주 중 새 카드 처리 |

`interruptPolicy` 선택지:
- **AlwaysRestart** — 항상 새 카드로 갈아탐 (입력이 씹히지 않음)
- **SoloHasPriority** — GuitarSolo 는 Stroke 를 끊지만 반대는 불가
- **IgnoreWhilePlaying** — 재생 끝날 때까지 새 카드 무시

GuitarSolo 2초가 카드 템포에 비해 길면 `fps` 를 16~20 으로 올리면 된다
(24프레임 ÷ 20fps = 1.2초).

## 5. 연결 방식

카드 시스템을 **전혀 건드리지 않는다** — `CardResolved` 이벤트만 구독한다.
(`CardSelected` 가 아닌 이유: `Role` 과 `CardId` 를 둘 다 가진 쪽이 `CardResolved` 다.
두 이벤트는 같은 프레임에 발행되므로 타이밍 차이는 없다)

`Ready`/`GameOver` 상태로 바뀌면 연주를 끊고 Idle 로 되돌린다.

## 6. 테스트

`Raccoon` 인스펙터의 `Raccoon Animator` ⋮ 메뉴:
`Debug/Play Idle` · `Debug/Play Stroke` · `Debug/Play Guitar Solo`

실제 확인: Play → 일반 카드(`tempo_up`) 사용 → Stroke 재생 후 Idle 복귀 →
`guitar_solo` 또는 스페셜 카드 → GuitarSolo 재생 후 Idle 복귀.

## 7. 씬 배치 / 정렬 순서

실제 아트를 확인해 정한 값이다 (둘 다 투명 배경 픽셀아트).

| 오브젝트 | sortingOrder | 크기(유닛) | 기본 위치 |
|---|---|---|---|
| `night_city_ground` (기존) | 0 | — | (0, 0) |
| `stage_lights` (기존) | 1 | — | (0, 0) |
| 관객 (런타임 생성) | 5 ~ 38 | — | — |
| **Friends** (밴드 3인) | 45 | 17.8 × 10 (화면 전체) | (0, 0) |
| **Raccoon** (뒷모습) | 50 | 3.76 × 5.01 | (0, −1.5) |

너구리가 카메라에 가장 가깝고(뒷모습), 밴드는 그 뒤 무대 위, 관객은 더 뒤다.
Friends 캔버스가 화면 전체 크기라 밴드가 좌우로 퍼져 있고 중앙이 비어 있어 너구리와 겹치지 않는다.

## 8. 아트 확인 필요

두 스프라이트 세트 모두 **좌상단에 AI 생성 워터마크**가 박혀 있다
(Raccoon `Ai`, Friends 작은 기호). 스프라이트가 캔버스 전체라 **게임에 그대로 보인다.**
제출 전 아트 담당이 크롭해야 한다.

## 9. 알려진 별건

`Sprites/Crowd/Animated` 도 같은 조각-슬라이스 문제가 있다 (프레임당 14조각).
관객 애니메이션 담당자에게 공유 필요 — 이 툴의 `ImportFolders` 표에 폴더를 추가하면
같은 방식으로 교정할 수 있다.
