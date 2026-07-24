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
| `SpecialAudienceTypes.cs` | `SpecialAudienceRequestType`, `SpecialAudienceEndReason`, `SpecialHitReward` |
| `SpecialAudienceManager.cs` | 타이머·셔플백·요구 관리·Special Hit 판정·이벤트 발행 + `SpecialAudience` 파사드 |
| `SpecialAudienceView.cs` | 표시 전담 (아이콘 전환·게이지·애니메이션). 로직을 전혀 모름 |
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

## 4. 카드 담당이 나중에 연결할 지점

**지금은 카드 코드에 아무것도 넣지 않았다.** 나중에 이 한 줄만 추가하면 된다.

```csharp
// CardSystem.SelectCard() 안, 판정 직후 등
if (SpecialAudience.TryHit(cardType, out var reward))
{
    // 점수·열기 담당이 reward.BonusBaseScore / reward.BonusHeat 를 적용
}
```

직접 참조가 있으면 `specialAudienceManager.TrySpecialHit(cardType, out var reward)` 도 동일하다.

- 맞으면 `true` + 보상 반환, 제한시간 정지 → 연출 → 0.7초 뒤 숨김 → 다음 간격 시작
- 틀리면 `false`, **요청은 그대로 유지되고 남은 시간도 계속 감소**
- 어느 쪽이든 점수·열기는 이 시스템이 직접 건드리지 않는다

카드에 맥락 타입이 아직 없으므로, 카드 담당은 `CardData` 에 `SpecialAudienceRequestType` 필드를 하나 추가하고
그 값을 넘기면 된다. (이 enum 은 카드 쪽에서 그대로 재사용하라고 만든 것)

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
