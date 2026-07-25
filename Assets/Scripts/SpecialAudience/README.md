# 특별 관객 시스템 (CONTEXT STAGE)

일정 시간마다 등장하는 특별 관객이 Chill / Singalong / Mosh 중 하나를 요구한다.
요구와 일치하는 타입이 들어오면 **Special Hit**.

> **카드·점수·열기 시스템을 전혀 건드리지 않는다.** 보상은 데이터로만 넘기고,
> 실제 점수·열기 적용은 각 담당이 이벤트를 받아서 한다.

## 1. 셋업

```
Tools/Special Audience/Setup Special Audience   →   Ctrl+S
```

`SpecialAudienceConfig.asset`, `[SpecialAudience]`(Manager), `SpecialAudienceCanvas`(그레이박스 View)가
생기고 서로 연결된다. 무대 액터에는 `CrowdSpawner`와 `SpecialAudienceDropTarget`을 명시적으로 연결한다.
아트 아이콘이 나오면 View 인스펙터에서 `chillIcon` / `singalongIcon` / `moshIcon` 만 갈아끼우면 된다.

## 2. 파일

| 파일 | 책임 |
|---|---|
| `SpecialAudienceConfig.cs` | 등장 간격·요구 시간·성공 연출 시간을 한 에셋에서 관리 |
| `SpecialAudienceTypes.cs` | `HeatStage`, `SpecialAudienceEndReason`, `SpecialHitReward` |
| `SpecialAudienceManager.cs` | 타이머·셔플백·요구 관리·Special Hit 판정·이벤트 발행 + `SpecialAudience` 파사드 |
| `SpecialAudienceView.cs` | UI 표시 전담 (아이콘 전환·게이지·애니메이션). 로직을 전혀 모름 |
| `SpecialAudienceCrowdActor.cs` | **무대 위 실물** — 일반 관객 사이에 섞여 돌아다니는 월드 스프라이트 |
| `SpecialAudienceSpeechBubble.cs` | 머리 위 말풍선 (1.5초마다 "Mosh!" 등) → `Scripts/Feedback/README.md` |
| `SpecialHitRewardBanner.cs` | 저격 성공 시 얻은 보상 배너 → `Scripts/Feedback/README.md` |
| `Editor/SpecialAudienceSetupMenu.cs` | 씬 셋업 + 그레이박스 UI |
| `GameJamKit/Core/GameEvents.cs` | 이벤트 struct 3종 추가 (킷 변경 이력에 기록함) |

## 3. 튜닝

등장 간격·요구 시간·연출 유지 시간은 `Assets/Settings/SpecialAudienceConfig.asset`에서 조정한다.
`autoStart`, 드롭 게이트, 디버그 입력 여부는 장면의 Manager가 소유한다.
일반 사용 값은 `AudienceReactionProfile`, 저격 사용 값은
`SpecialCardTargetEffect`가 소유한다.

## 4. 카드 시스템과의 연결 (완료됨)

요구 타입은 **카드 시스템의 `HeatStage`(Chill/Singalong/Mosh)를 그대로 쓴다.**
같은 의미의 enum 을 두 벌 두지 않기 위해 `SpecialAudienceRequestType` 은 폐기했다.

`CardSystem.SelectCard()`는 드롭 위치에서 만든 `SpecialCardRequest`를 다음처럼 사용한다.

```csharp
bool matchesTargetedRequest =
    card.Role == CardRole.Special &&
    request.IsActive &&
    card.TargetStage == request.RequestedStage;

if (matchesTargetedRequest &&
    SpecialAudience.ConsumeRequest(card.TargetStage, 0, 0f))
{
    SpecialCardTargetEffectExecutor.TryExecute(card.SpecialTargetEffect, ...);
    isSpecialHit = true;
}
```

저격이 성립하지 않으면 특수 카드도 `AudienceReactionProfile`의 일반 사용 효과를 적용한다.
성립하면 `SpecialCardTargetEffect`만 실행하고 `isSpecialHit`를 통해 콤보·UI·조명·성공 연출을
알린다. 기존 특수 히트 점수·열기 보상은 사용하지 않으므로 `ConsumeRequest`에는 0을 넘긴다.

| API | 쓰는 곳 | 역할 |
|---|---|---|
| `SpecialAudience.ResolveDropRequest(position, radius)` | 카드 드롭 순간 | 유효한 저격 요청 생성 |
| `SpecialAudience.ConsumeRequest(stage, 0, 0)` | 저격 성공 카드 경로 | 요청 소비 및 성공 이벤트·연출 발생 |
| `SpecialAudience.TryHit(stage, out reward)` | 구형 호출부 호환 전용 | 요청과 연출만 소비하며 보상은 0. 신규 코드는 `ConsumeRequest` 사용 |

- 맞으면 제한시간 정지 → 연출 → 0.7초 뒤 숨김 → 다음 간격 시작
- 틀리면 **요청은 그대로 유지되고 남은 시간도 계속 감소**
- 어느 쪽이든 매니저가 점수·열기를 직접 건드리지 않는 건 그대로다

Special 역할 카드는 3단계 모두 있다: `Card_02_Response`(Chill) / `Card_04_PassMic`(Singalong) / `Card_06_MoshPit`(Mosh).

## 4-1. 무대 위 특별 관객 (`SpecialAudienceCrowdActor`)

등장하면 `CrowdSpawner` 가 배치한 일반 관객 중 한 명의 옆자리를 골라 **그 줄에 섞여 선다.**
`dwellDuration` 마다 다른 관객 자리로 걸어서 옮겨 다니고, 줄이 바뀌면 그 줄의
**크기·정렬 순서를 물려받아** 앞뒤 관계가 깨지지 않는다.

작동 방식:

| 단계 | 내용 |
|---|---|
| 등장 | `SpecialAudienceSpawned` 구독 → 군중 오브젝트의 자식으로 들어가 자리 선정 |
| 이동 | 관객 한 명을 골라 `HomeLocalPosition ± lateralOffset` 으로 걸어감. 진행 방향으로 스프라이트 반전 |
| 제자리 | 일반 관객과 **같은 `CrowdMotionProfile`** (반동·흔들림·점프·스쿼시) |
| 성공 | `celebrateMotion`으로 크게 뛰다가 이벤트의 `HoldDuration` 후 사라짐 |
| 만료/종료 | 즉시 사라짐 |

**매니저를 직접 참조하지 않고 EventBus 만 구독**하므로 UI 뷰(`SpecialAudienceView`)와
동시에 켜둘 수 있다 — 게이지는 화면 위, 캐릭터는 무대 아래.

주요 인스펙터 값: `moveSpeed`(1.6) / `dwellDuration`(1.8초) / `lateralOffset`(0.45) /
`scaleMultiplier`(1.15 — 주변보다 살짝 커서 눈에 띈다) / `sortingOrderBonus`(1).

## 4-2. 저격 성공 화면 효과 (요구 타입별)

```
Tools/Special Audience/Setup Special Hit Effects
```

| 요구 | 연출 |
|---|---|
| Chill | **색수차** — `SpecialHit_Chill_Chromatic.asset` (ChromaticSplit, Circle, 0.45초) |
| Singalong | **카메라 셰이크** — `CameraShake.ShakeFor(0.35, 0.35)` |
| Mosh | **링 디스토션** — 내장 폴백 (기존과 동일) |

셋 다 **같은 타이밍**에 터진다. `SpecialAudienceCrowdActor.OnSpecialHit` 한 지점에서
`specialHitEffects` 표를 찾아 재생하기 때문이다. 예전에는 이 자리에 Mosh 만 하드코딩돼 있었다.

표 한 줄(`SpecialHitScreenEffect`)이 갖는 것:

| 필드 | 뜻 |
|---|---|
| `profile` | 재생할 화면 효과. 비우면 화면 효과 없음 |
| `effectSize` / `strengthMultiplier` / `durationOverride` | 프로필 재생 인자 |
| `useBuiltinDistortionFallback` | 프로필이 비었을 때 내장 링 디스토션으로 대신할지 (Mosh 전용) |
| `cameraShake` / `shakeStrength` / `shakeDuration` | 카메라 흔들림. 화면 효과와 **독립**이라 둘 다 켤 수 있다 |

요구 타입이 늘어나도 표에 줄만 추가하면 되고 코드는 그대로다.
셋업은 **프로필이 이미 연결된 줄을 덮어쓰지 않으므로** 연출 담당이 바꿔 둔 값이 보존된다.

`CrowdSpawner` 또는 배치된 관객이 없으면 fallback 위치에 잘못 표시하지 않고 오류를 남긴 뒤 숨는다.
`SpecialAudienceDropTarget`도 런타임에 자동 생성하지 않으므로 셋업 검증에서 누락을 잡을 수 있다.

## 5. 점수·열기 담당이 받는 이벤트

```csharp
void OnEnable()  => EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
void OnDisable() => EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);

void OnSpecialHit(SpecialHitLanded e)
{
    if (e.AlreadyApplied) return;
    score.Add(e.Reward.BonusBaseScore);
    hype.Add(e.Reward.BonusHeat);
}
```

매니저 직접 참조가 편하면 C# 이벤트도 있다 (같은 타이밍, 한 요청당 1회):

```csharp
manager.OnSpecialAudienceSpawned += (type, duration) => ...;
manager.OnRequestTimeChanged     += (remaining, total) => ...;
manager.OnSpecialAudienceExpired += type => ...;
manager.OnSpecialAudienceEnded   += (type, reason) => ...;   // 종료 사유 통합
manager.OnSpecialHit             += (type, reward) => ...;
```

## 6. 테스트

**P 키** — Play Mode에서 누르면 즉시 등장. 이미 활성 요청이 있으면 새 랜덤 요청으로 교체된다.
콘솔에 `[SpecialAudience] Debug Spawn: Singalong` 이 찍힌다.

**ContextMenu** — 카드 시스템 없이 Special Hit까지 확인할 때. `[SpecialAudience]` 인스펙터의
컴포넌트 우측 `⋮` 메뉴에서:

| 메뉴 | 동작 |
|---|---|
| `Debug/Spawn Random Special Audience` | 랜덤 타입으로 즉시 등장 (시스템이 꺼져 있으면 켜고 등장) |
| `Debug/Resolve Current Request As Special Hit` | 현재 요구를 보상 없이 소비해 성공 연출 검증 |

두 번째 메뉴는 실제 판정 경로를 그대로 타므로 이벤트·연출·다음 간격까지 전부 검증된다.

## 7. 설계 메모

- **타이머는 코루틴이 아니라 `Update` + `Time.time` 마감시각 비교** (`HypeSystem` 과 같은 방식).
  Disable·씬 재시작 때 코루틴이 남아 중복 실행되는 사고가 구조적으로 불가능하다.
- Special Hit 이벤트는 `_hitRaisedForCurrentRequest` 플래그로 **한 요청당 정확히 1회**.
  만료 프레임과 호출이 겹쳐도 `Update`가 먼저 Phase를 바꾸므로 중복 소비는 `false`를 돌려준다.
- 셔플 백은 리스트 하나를 재사용한다 (등장마다 새 할당 없음).
- Animator 트리거는 `StringToHash` 로 캐싱, 문자열 비교 없음.
- 로그는 `UNITY_EDITOR || DEVELOPMENT_BUILD` 에서만 컴파일된다.

## 8. 성향 픽셀 VFX

`SpecialAudiencePersonalityVFX`는 특별 관객 액터가 등장할 때 한 번 준비하고,
등장 중 같은 파티클 시스템과 스프라이트 렌더러를 계속 재사용한다.

- Chill(나무늘보): 머리 양옆에서 떨어지는 청록 픽셀 땀방울
- Singalong(앵무새): 위로 떠오르는 보라·노랑 픽셀 음표
- Mosh(검은 소): 머리 주변에서 조여드는 굵은 붉은 픽셀 분노 주름과 파편

평상시에는 낮은 빈도로 특징을 보여주고, `SpecialHitLanded`에서는 카드 중앙
임팩트보다 0.05초 늦게 강한 버전을 재생한다. 이 색은 특별 관객의 정체성
표현이며, 일반 관객의 성공/실패 반응색과는 별도 경로다.
