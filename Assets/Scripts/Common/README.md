# 스프라이트 애니메이션 (CONTEXT STAGE 공용)

Animator/AnimatorController 에셋 없이 **인스펙터 리스트만으로** 프레임 애니메이션을 돌린다.
관객·특별 관객·(나중에) 주인공이 전부 같은 계층을 쓴다.

## 1. 구성

| 타입 | 역할 |
|---|---|
| `SpriteAnimationClip` | 프레임 배열 + fps + loop + pingPong. **프레임 1장이면 정지 이미지** |
| `SpriteAnimationPlayer` | 재생 상태만 들고 있는 순수 로직. `Bind(renderer/image)` → `Play(clip)` → `Tick(dt)`. 프레임당 할당 0 |
| `SpriteSheetAnimator` | 이름으로 클립을 고르는 범용 컴포넌트. `Play("Idle")`, `PlayOnce("Solo", onComplete)` |

`SpriteRenderer`(월드)와 `Image`(UI) 둘 다 출력 대상으로 지원한다.

## 2. 지금 어디에 붙어 있나

**특별 관객** (`SpecialAudienceView`)
- `chillClip` / `singalongClip` / `moshClip` — 지금은 `frames`에 1장씩만 들어있다
- `specialHitClip` / `expireClip` — 연출용, 비워두면 요구 클립을 계속 재생
- 아트가 프레임을 더 주면 **인스펙터에서 `frames` 크기만 늘리면 끝.** 코드 수정 없음

**관객** (`CrowdMoodTier`)
- `sprites[]` — 개체 변형(누가 어떻게 생겼나). 지금 쓰는 방식
- `animationVariants[]` — **비어 있으면 위 정지 이미지, 채우면 애니메이션.** 관객마다 variant 하나를 배정받는다
- 관객은 위상·속도가 개체별로 다르므로 `Tick(dt * _speedMul)` 로 리듬이 서로 어긋난다

> **variant(변형)와 frame(프레임)은 다른 축이다.** 관객 10명이 서로 다르게 생긴 건 variant,
> 한 명이 팔을 흔드는 건 frame. `animationVariants[i].frames[j]` 로 두 축을 모두 표현한다.

## 3. 주인공(락스타)은 어떻게 할까

**결론: 이 프로젝트에서는 `SpriteSheetAnimator` 를 권장하고, Animator 는 쓰지 않는 쪽이 낫다.**

| | SpriteSheetAnimator | Animator (Mecanim) |
|---|---|---|
| 에셋 | 없음 (컴포넌트 인스펙터에 데이터) | `.controller` + `.anim` 파일 다수 |
| 병합 충돌 | 프리팹/씬 diff로 해결 가능 | **`.controller` 는 사실상 병합 불가** |
| 상태 전환 | `Play("Solo")` 코드 한 줄 | 파라미터 + 전이 조건 그래프 |
| 이벤트 | `PlayOnce(clip, onComplete)` 콜백 | Animation Event (함수명 문자열 참조) |
| 블렌드/레이어 | 없음 | 강력 (블렌드 트리, 레이어, IK) |
| 러닝 코스트 | `Update` 한 줄 | 오브젝트당 Animator 오버헤드 |

판단 근거:

1. **이 팀은 이미 씬 병합 충돌로 한 번 크게 깨졌다.** `.controller` 는 그보다 더 고약하다.
   주인공 애니메이션은 결국 아트·연출 담당이 동시에 만질 파일이라 충돌 확률이 높다.
2. 주인공이 필요한 건 `Idle / Stroke / Solo / Fail` 같은 **상태 몇 개의 단순 전환**이고,
   블렌드 트리나 레이어가 필요한 이동·조준이 없다 (기획서: "락스타를 직접 조작하는 액션 게임이 아니다").
3. 카드 연출과 타이밍을 맞춰야 하는데, `PlayOnce("Solo", () => Play("Idle"))` 가
   Animation Event 로 콜백 받는 것보다 훨씬 추적하기 쉽다.

**Animator 를 쓸 만한 경우**: 프레임 애니메이션이 아니라 본/스켈레탈(2D Animation 패키지)로 가거나,
전환마다 크로스페이드가 꼭 필요하거나, 아트가 Animator 워크플로에 익숙해서 직접 만들어 넘겨줄 때.

절충안도 열려 있다 — `SpecialAudienceView` 는 **Animator 가 붙어 있으면 트리거도 같이 쏘고,
없으면 클립만 재생한다.** 주인공도 같은 식으로 두 방식을 병행할 수 있다.

## 4. 주인공에 붙일 때 (나중에)

```csharp
// 1) 락스타 오브젝트에 SpriteSheetAnimator 추가, clips 에 Idle/Stroke/Solo 등록
// 2) 카드 이벤트를 구독해 상태만 바꾼다 (카드 코드는 건드리지 않는다)
void OnEnable()  => EventBus.Subscribe<CardSelected>(OnCardSelected);
void OnDisable() => EventBus.Unsubscribe<CardSelected>(OnCardSelected);

void OnCardSelected(CardSelected e)
{
    string clip = e.Judgement == HypeJudgement.Perfect ? "Solo" : "Stroke";
    animator.PlayOnce(clip, () => animator.Play("Idle"));
}
```

관객 상태에 맞춰 주인공 연출을 바꾸고 싶으면 `ICrowdMoodReactor` 를 구현하면 된다.
