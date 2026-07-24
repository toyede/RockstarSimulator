# 특별 관객 시스템 (CONTEXT STAGE)

일정 시간마다 등장하는 특별 관객이 Chill / Singalong / Mosh 중 하나를 요구한다.
요구와 일치하는 타입이 들어오면 **Special Hit**.

> **카드·점수·열기 시스템을 전혀 건드리지 않는다.** 보상은 데이터로만 넘기고,
> 실제 점수·열기 적용은 각 담당이 이벤트를 받아서 한다.

## 1. 셋업

```
Tools/Special Audience/Setup Special Audience   →   Ctrl+S
```

`[SpecialAudience]`(Manager) + `SpecialAudienceCanvas`(그레이박스 View)가 생기고 서로 연결된다.
아트 아이콘이 나오면 View 인스펙터에서 `chillIcon` / `singalongIcon` / `moshIcon` 만 갈아끼우면 된다.

## 2. 파일

| 파일 | 책임 |
|---|---|
| `SpecialAudienceTypes.cs` | `HeatStage`, `SpecialAudienceEndReason`, `SpecialHitReward` |
| `SpecialAudienceManager.cs` | 타이머·셔플백·요구 관리·Special Hit 판정·이벤트 발행 + `SpecialAudience` 파사드 |
| `SpecialAudienceView.cs` | UI 표시 전담 (아이콘 전환·게이지·애니메이션). 로직을 전혀 모름 |
| `SpecialAudienceCrowdActor.cs` | **무대 위 실물** — 일반 관객 사이에 섞여 돌아다니는 월드 스프라이트 |
| `Editor/SpecialAudienceSetupMenu.cs` | 씬 셋업 + 그레이박스 UI |
| `GameJamKit/Core/GameEvents.cs` | 이벤트 struct 3종 추가 (킷 변경 이력에 기록함) |

## 3. 튜닝 (인스펙터)

| 항목 | 기본값 |
|---|---|
| 첫 등장 대기 `firstSpawnDelay` | 10초 |
| 등장 간격 `spawnInterval` | 15초 (사라진 시점부터) |
| 요구 유지 `requestDuration` | 7초 |
| 자동 시작 `autoStart` | true |
| Special Hit 점수 `specialHitBaseScore` | 400 |
| Special Hit 열기 `specialHitBonusHeat` | 25 |
| 연출 유지 `specialHitHoldDuration` | 0.7초 |
| 디버그 키 `debugSpawnKey` | P |

## 4. 카드 시스템과의 연결 (완료됨)

요구 타입은 **카드 시스템의 `HeatStage`(Chill/Singalong/Mosh)를 그대로 쓴다.**
같은 의미의 enum 을 두 벌 두지 않기 위해 `SpecialAudienceRequestType` 은 폐기했다.

`CardSystem.SelectCard()` 에서 실제로 이어지는 부분은 두 곳뿐이다.

```csharp
// 1) 판정기에 지금 요구 중인 맥락을 넘긴다 (없으면 SpecialCardRequest.None 과 동일)
var result = CardEffectResolver.Resolve(card, currentHype, config, SpecialAudience.CurrentRequest);

// 2) 특수 히트였다면 요청을 소비시킨다 (보상은 카드 수치로 이미 적용됨)
if (result.IsSpecialHit)
    SpecialAudience.ConsumeRequest(card.TargetStage, result.BaseScore, result.HeatDelta);
```

**보상 이중 적용 주의.** 점수·열기는 카드가 자기 수치(`CardDefinition.SpecialHitBaseScore/HeatDelta`)로
`CardSelected` 경로를 통해 이미 적용한다. 그래서 카드 경로는 매니저 보상을 쓰지 않는
`ConsumeRequest` 를 호출하고, 이때 `SpecialHitLanded.AlreadyApplied` 가 `true` 로 나간다.

| API | 쓰는 곳 | 보상 |
|---|---|---|
| `SpecialAudience.CurrentRequest` | 카드 판정기에 맥락 전달 | – |
| `SpecialAudience.ConsumeRequest(stage, score, heat)` | 카드 경로 (보상 이미 적용됨) | 이벤트에 실제 적용값 전달, `AlreadyApplied = true` |
| `SpecialAudience.TryHit(stage, out reward)` | 카드 밖에서 단독 판정할 때 | 매니저 인스펙터 값(400/25) 반환, 호출부가 적용 |

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
| 성공 | `celebrateMotion` 으로 크게 뛰다가 `celebrateDuration`(0.7초) 후 사라짐 |
| 만료/종료 | 즉시 사라짐 |

**매니저를 직접 참조하지 않고 EventBus 만 구독**하므로 UI 뷰(`SpecialAudienceView`)와
동시에 켜둘 수 있다 — 게이지는 화면 위, 캐릭터는 무대 아래.

주요 인스펙터 값: `moveSpeed`(1.6) / `dwellDuration`(1.8초) / `lateralOffset`(0.45) /
`scaleMultiplier`(1.15 — 주변보다 살짝 커서 눈에 띈다) / `sortingOrderBonus`(1).

`CrowdSpawner` 가 없거나 관객이 아직 배치되지 않았으면 `fallbackPosition` 에 조용히 선다.

## 5. 점수·열기 담당이 받는 이벤트

```csharp
void OnEnable()  => EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
void OnDisable() => EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);

void OnSpecialHit(SpecialHitLanded e)
{
    score.Add(e.Reward.BonusBaseScore);   // 점수 담당
    hype.Add(e.Reward.BonusHeat);         // 열기 담당
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
| `Debug/Resolve Current Request As Special Hit` | 현재 요구 타입을 그대로 `TrySpecialHit()` 에 넘겨 성공시킴 |

두 번째 메뉴는 실제 판정 경로를 그대로 타므로 이벤트·연출·다음 간격까지 전부 검증된다.

## 7. 설계 메모

- **타이머는 코루틴이 아니라 `Update` + `Time.time` 마감시각 비교** (`HypeSystem` 과 같은 방식).
  Disable·씬 재시작 때 코루틴이 남아 중복 실행되는 사고가 구조적으로 불가능하다.
- Special Hit 이벤트는 `_hitRaisedForCurrentRequest` 플래그로 **한 요청당 정확히 1회**.
  만료 프레임과 호출이 겹쳐도 `Update` 가 먼저 Phase를 바꾸므로 `TrySpecialHit` 은 `false` 를 돌려준다.
- 셔플 백은 리스트 하나를 재사용한다 (등장마다 새 할당 없음).
- Animator 트리거는 `StringToHash` 로 캐싱, 문자열 비교 없음.
- 로그는 `UNITY_EDITOR || DEVELOPMENT_BUILD` 에서만 컴파일된다.
