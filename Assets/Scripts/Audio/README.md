# 사운드 — 관객 앰비언스 (CONTEXT STAGE)

호응도(Hype)에 따라 관객 소리를 `crowd_low → crowd_middle → crowd_high` 로 자동 전환하는 시스템.
GameJamKit 의 `SoundLibrary` / `EventBus` / `MonoSingleton` / `Save` 를 그대로 쓰고,
**킷의 BGM 채널은 건드리지 않는다** (음악 트랙과 앰비언스가 동시에 필요하므로 자체 AudioSource 2개로 크로스페이드).

## 1. 셋업 (한 번만)

```
Tools/Audio/Setup Crowd Ambience
```

이 메뉴 하나로 아래가 전부 끝난다. 이미 있는 것은 덮어쓰지 않으므로 여러 번 눌러도 안전하다.

1. `[Managers]` (GameManager / AudioManager …) 생성 — 킷 메뉴 재사용
2. `Assets/GameJamKit/Resources/SoundLibrary.asset` 생성 + `Assets/Audio` 클립 자동 등록
3. `Assets/Settings/CrowdAmbienceConfig.asset` 생성 (low/middle/high 기본 매핑)
4. 씬에 `[CrowdAmbience]` 오브젝트 배치 + 디버그 입력 부착

## 2. 동작

| 시점 | 동작 |
|---|---|
| Ready → Playing | 현재 호응도에 맞는 티어로 페이드인 |
| 호응도 변화 (`HypeChanged`) | 티어 재평가 → 바뀌면 크로스페이드 |
| Playing → Paused | 앰비언스 일시정지 |
| GameOver / Ready | 페이드아웃 후 정지 |

티어 경계에는 히스테리시스(기본 ±0.04)가 있어 경계값에서 소리가 딸깍거리지 않는다.
전환 시 새 클립은 이전 클립과 같은 재생 위치에서 시작해 군중 소리가 끊기지 않는다.

## 3. 튜닝

수치는 코드가 아니라 `Assets/Settings/CrowdAmbienceConfig.asset` 에서 바꾼다.

| 항목 | 기본값 | 의미 |
|---|---|---|
| Low / Middle / High `minNormalized` | 0.00 / 0.40 / 0.70 | 호응도 비율(0~1) 경계 |
| 티어 `volume` | 0.8 / 0.9 / 1.0 | 티어별 기본 볼륨 |
| `crossfadeDuration` | 1.2초 | 티어 전환 시간 |
| `hysteresis` | 0.04 | 경계 진동 방지 여유폭 |
| `startFadeDuration` / `stopFadeDuration` | 1.5 / 1.0초 | 공연 시작·종료 페이드 |

**티어를 4단계 이상으로 늘려도 코드 수정이 필요 없다.** 리스트에 항목을 추가하고
`soundId` 를 SoundLibrary 에 등록하면 그대로 동작한다.

## 4. 다른 담당자용 API

```csharp
CrowdAmbience.Volume = 0.5f;         // 앰비언스 볼륨 (0~1, PlayerPrefs 자동 저장)
CrowdAmbience.ForceTier("High");     // 연출 중 강제 고조 (앙코르 등)
CrowdAmbience.ReleaseForcedTier();   // 연출 끝나면 호응도 추종으로 복귀
CrowdAmbience.SetMuted(true);        // 컷신용 임시 음소거 (저장 안 됨)
CrowdAmbience.CurrentTier;           // "Low" / "Middle" / "High"
```

티어가 바뀌는 순간에 조명·관객 애니메이션을 같이 바꾸고 싶다면 이벤트를 구독한다.

```csharp
void OnEnable()  => EventBus.Subscribe<CrowdAmbienceTierChanged>(OnTier);
void OnDisable() => EventBus.Unsubscribe<CrowdAmbienceTierChanged>(OnTier);
void OnTier(CrowdAmbienceTierChanged e) => Debug.Log($"{e.PreviousIndex} → {e.Index} ({e.TierName})");
```

## 4-1. 무대 BGM (`StageBgmPlayer`)

`[CrowdAmbience]` 오브젝트에 함께 붙는다. 씬이 시작되면 `big_rock` 을 1.5초 페이드인으로 재생한다.

| 인스펙터 | 기본값 | 설명 |
|---|---|---|
| `bgmId` | `big_rock` | SoundLibrary ID |
| `startOn` | `SceneStart` | `PerformanceStart`(공연 시작 시) / `Manual` 로 변경 가능 |
| `fadeInDuration` | 1.5초 | 페이드인 |
| `stopOnGameOver` | 꺼짐 | 켜면 게임오버 시 페이드아웃 |

**볼륨은 두 층으로 나뉜다.** 곡 자체 밸런스는 SoundLibrary 의 `big_rock` 항목 볼륨(= 0.5),
플레이어가 옵션에서 만지는 값은 Bgm 채널 볼륨(`AudioVolumeSlider`)이다. 서로 곱해진다.

```csharp
Bgm.Play("big_rock");   // 곡 교체 (자동 크로스페이드)
Bgm.Stop();
Bgm.Volume = 0.8f;      // 채널 볼륨 (자동 저장)
```

## 4-2. 카드 효과음 (`CardSfxPlayer`)

카드를 쓰면 `guitar_stroke` 가 재생된다. **카드 코드를 건드리지 않고 `CardSelected` 이벤트만 구독**하므로
카드 담당이 로직을 바꿔도 사운드 쪽은 영향을 받지 않는다.

| 인스펙터 | 기본값 | 설명 |
|---|---|---|
| `defaultSfxId` | `guitar_stroke` | 모든 카드 공통 소리 |
| `cardOverrides` | 비어 있음 | 특정 `cardId` 만 다른 소리 (예: 기타 솔로 카드 → `guitar_solo`) |
| `volumeScale` | 1 | 카드 효과음 볼륨 배율 |
| 판정별 4칸 | 비어 있음 | Perfect/Good/Miss/RiskMiss 에 소리를 얹고 싶을 때 (`hey_high`, `crowd_mistake` …) |

빈 칸은 조용히 넘어가므로 지금은 스트로크 한 방만 난다.

## 5. 볼륨 UI

옵션 패널의 `Slider` 에 `AudioVolumeSlider` 를 붙이고 `channel` 만 고르면 끝이다.
(Master / Bgm / Sfx / CrowdAmbience — 값은 자동 저장·복원된다)

## 6. 디버그 키 (`CrowdAmbienceDebugInput`)

| 키 | 동작 |
|---|---|
| `[` `]` | 앰비언스 볼륨 ∓10% |
| `-` `=` | 마스터 볼륨 ∓10% |
| `M` | 앰비언스 음소거 토글 |
| `T` | 티어 강제 전환 (Low → Middle → High → 자동) |

호응도 자체는 `HypeDebugInput` 의 `Space`(시작) / `1~4`(판정) 로 움직인다.
정식 옵션 UI 가 붙으면 이 컴포넌트는 삭제하지 말고 비활성화만 해둔다.

## 7. 등록된 사운드 ID

| ID | 파일 | 용도 |
|---|---|---|
| `big_rock` | `Audio/BGM/Big Rock.mp3` | 무대 BGM (루프, 볼륨 0.5) |
| `crowd_low` / `crowd_middle` / `crowd_high` | `Audio/BGM/` | 관객 앰비언스 (루프) |
| `crowd_mistake` | `Audio/OneShot/Crowd_mistake.wav` | 실패 판정 |
| `guitar_solo` / `guitar_stroke` | `Audio/OneShot/` | 카드 연출 |
| `hey_high` / `hey_low` | `Audio/OneShot/` | 관객 함성 |

원샷은 킷 API 로 재생한다: `Sound.Play("guitar_solo");`
새 클립을 넣으면 `Tools/Audio/Register Audio Clips To Library` 로 다시 등록한다
(추가할 ID 는 `Assets/Scripts/Editor/AudioSetupMenu.cs` 의 `DefaultSounds` 표에 한 줄 넣으면 된다).
