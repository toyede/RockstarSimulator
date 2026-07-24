# 관객 비주얼 피드백 (CONTEXT STAGE)

호응도(Hype)에 따라 관객 스프라이트와 움직임을 `Low → Middle → High` 로 바꾼다.
**High 상태에서는 관객이 방방 뛴다.**

## 1. 셋업

```
Tools/Crowd/Setup Crowd Scene
```

1. `Assets/Settings/CrowdMoodConfig.asset` 생성
2. `Assets/Sprites/Crowd/crowd_low·middle·high.png` 의 슬라이스된 스프라이트를 상태별 세트로 자동 연결
3. 씬에 `[Crowd]`(CrowdMoodDirector + CrowdSpawner) 배치

실행 후 **Ctrl+S 로 씬을 저장**해야 다른 팀원 컴퓨터에서도 보인다.
아트가 시트를 다시 슬라이스하면 `Tools/Crowd/Reassign Crowd Sprites` 만 다시 누르면 된다.

## 2. 구조

```
HypeSystem ──HypeChanged──▶ CrowdMoodDirector ──ICrowdMoodReactor──▶ CrowdMemberView (관객 N명)
                                   │                                  └ 스프라이트 교체 + 절차적 움직임
                                   └──CrowdMoodChanged(EventBus)──▶ 조명·카메라·UI (원하는 쪽이 구독)
```

- 상태 판정은 사운드(`CrowdAmbienceConfig`)와 **같은 `HypeTierUtil`** 을 쓴다
  → 경계값만 맞춰두면 소리와 그림이 정확히 같은 순간에 바뀐다
- 관객은 프리팹 없이 `CrowdSpawner` 가 런타임에 생성한다 (씬 병합 충돌 없음)
- 반응자는 등록 즉시 현재 상태를 받으므로, 늦게 생성돼도 상태가 어긋나지 않는다

## 3. 움직임 (애니메이터 없음)

상태별 스프라이트가 1종류뿐이어도 구분되도록 움직임을 전부 코드로 만든다.

| 요소 | 설명 | Low | Middle | High |
|---|---|---|---|---|
| bob | 제자리 위아래 까딱임 | 느리고 작게 | 뚜렷하게 | 빠르게 |
| sway | 좌우로 기울기 | 2° | 6° | 8° |
| **jump** | **포물선 점프 (방방)** | 없음 | 없음 | **0.45유닛 · 초당 1.8회** |
| squash | 착지 시 눌림 | – | – | 15% |

관객마다 위상(phase)·속도·좌우반전이 달라 군무처럼 보이지 않고,
상태 전환은 `moodBlendDuration`(기본 0.6초) 동안 수치가 섞여 툭 끊기지 않는다.

## 4. 튜닝

전부 `Assets/Settings/CrowdMoodConfig.asset` 에서 한다. 코드 수정 불필요.

- 상태 경계: `minNormalized` (기본 0 / 0.4 / 0.7 — **사운드 콘픽과 같은 값으로 유지할 것**)
- 상태별 색조 `tint`, 크기 `scaleMultiplier`
- 움직임: `bobHeight/Speed`, `swayAngle/Speed`, `jumpHeight`, `jumpsPerSecond`, `airTimeRatio`, `squash`
- `jumpHeight` 를 0보다 크게 주면 **어느 상태든** 점프한다 (Middle 도 살짝 뛰게 하고 싶으면 0.15 정도)

배치는 `[Crowd]` 오브젝트의 `CrowdSpawner` 인스펙터에서:
줄 수 / 한 줄 인원 / 간격 / 흐트러짐 / 앞줄 크기 / 줄마다 줄어드는 비율 / seed.

## 5. 확장 포인트

**상태를 4단계 이상으로 늘리기** — 콘픽 리스트에 항목 추가 → `CrowdSetupMenu.MoodSheets` 표에 시트 한 줄 추가. 코드 로직은 그대로.

**다른 연출을 관객과 동시에 반응시키기** — `ICrowdMoodReactor` 구현 후 등록:

```csharp
public class StageLight : MonoBehaviour, ICrowdMoodReactor
{
    void OnEnable()  => CrowdMood.Register(this);
    void OnDisable() => CrowdMood.Unregister(this);

    public void OnCrowdMoodChanged(CrowdMoodTier tier, int index, bool instant)
        => GetComponent<Light2D>().color = tier.tint;   // 관객과 같은 프레임에 바뀐다
}
```

느슨하게 붙이고 싶으면 EventBus 도 된다.

```csharp
EventBus.Subscribe<CrowdMoodChanged>(e => Debug.Log($"{e.PreviousIndex} → {e.Index} ({e.MoodName})"));
```

**스프라이트 프레임 애니메이션으로 바꾸기** — 아트가 진짜 프레임 시트를 주면
해당 상태의 `spriteCycleFps` 를 0보다 크게만 하면 세트를 프레임처럼 순환한다 (코드 수정 불필요).

## 6. 연출용 API

```csharp
CrowdMood.Current;              // "Low" / "Middle" / "High"
CrowdMood.ForceMood("High");    // 앙코르 연출 중 강제 고조
CrowdMood.ReleaseForcedMood();  // 호응도 추종으로 복귀
```

## 7. 테스트

`Space` 로 공연 시작 → `1`(Perfect) 을 두세 번 눌러 호응도를 70% 이상으로 올리면 High 로 바뀌며 관객이 뛴다.
`3`·`4` 로 떨어뜨리면 다시 가라앉는다. (`HypeDebugInput`)
