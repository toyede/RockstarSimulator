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

카드를 쓰면 기본적으로 `guitar_stroke` 가 재생된다. **카드 코드를 건드리지 않고 `CardSelected` 이벤트만 구독**하므로
카드 담당이 로직을 바꿔도 사운드 쪽은 영향을 받지 않는다.

| 인스펙터 | 기본값 | 설명 |
|---|---|---|
| `defaultSfxId` | `guitar_stroke` | 모든 카드 공통 소리 (override 가 없거나 클립이 아직 없는 카드의 폴백) |
| `cardOverrides` | 카드별 자동 배선 | 특정 `cardId` 만 다른 소리 (아래 참고) |
| `volumeScale` | 1 | 카드 효과음 볼륨 배율 |
| 판정별 4칸 | 비어 있음 | Perfect/Good/Miss/RiskMiss 에 소리를 얹고 싶을 때 (`hey_high`, `crowd_mistake` …) |

**카드별 소리는 `Assets/Scripts/Editor/AudioSetupMenu.cs` 의 `CardSfxMap` 표(카드 `id` → SoundLibrary
`sfxId`)가 단일 소스다.** `Tools/Audio/Setup Crowd Ambience` (또는 단독으로
`Tools/Audio/Assign Card Sfx Overrides`) 를 실행하면, `CardSfxMap` 에 있는 카드 중 **SoundLibrary에
실제 클립이 등록된 것만** `cardOverrides` 에 자동으로 채워진다. 클립이 아직 없는 카드는 조용히
건너뛰고 `defaultSfxId` 로 계속 재생된다 — 없는 sfxId 를 억지로 넣으면 카드를 낼 때마다 경고 로그가
쌓이기 때문에 일부러 이렇게 만들었다. 이미 인스펙터에서 수동으로 넣어둔 `cardId` 는 절대 덮어쓰지 않는다.

지금은 `guitar_solo` 카드 하나만 전용 클립(`Guitar_Solo.wav`)이 있어 실제로 다른 소리가 나고,
나머지 7장은 아래 "7. 등록된 사운드 ID" 표의 placeholder 경로에 파일이 채워지는 대로
같은 메뉴 재실행 두 번(`Register Audio Clips To Library` → `Setup Crowd Ambience` 또는
`Assign Card Sfx Overrides`)만으로 자동 연결된다. 코드 수정은 필요 없다.

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
| `guitar_solo` / `guitar_stroke` | `Audio/OneShot/` | 카드 연출 (`guitar_solo` 카드 전용 + 공통 기본음) |
| `hey_high` / `hey_low` | `Audio/OneShot/` | 관객 함성 |
| `card_tempo_up` | `Audio/OneShot/Cards/Card_tempo_up.wav` | `tempo_up` 카드 전용 |
| `card_response_call` | `Audio/OneShot/Cards/Card_ResponseCall.wav` | `response_call` 카드 전용 |
| `card_hands_up` | `Audio/OneShot/Cards/Card_HandsUp.wav` | **[클립 대기 중]** `hands_up` 카드 전용 |
| `card_pass_mic` | `Audio/OneShot/Cards/Card_PassMic.wav` | **[클립 대기 중]** `pass_mic` 카드 전용 |
| `card_open_mosh_pit` | `Audio/OneShot/Cards/Card_OpenMoshPit.wav` | `open_mosh_pit` 카드 전용 |
| `card_draw_two` | `Audio/OneShot/Cards/Card_DrawTwo.wav` | `draw_two` 카드 전용 |
| `card_reroll_hand` | `Audio/OneShot/Cards/Card_RerollHand.wav` | `reroll_hand` 카드 전용 |

원샷은 킷 API 로 재생한다: `Sound.Play("guitar_solo");`
새 클립을 넣으면 `Tools/Audio/Register Audio Clips To Library` 로 다시 등록한다
(추가할 ID 는 `Assets/Scripts/Editor/AudioSetupMenu.cs` 의 `DefaultSounds` 표에 한 줄 넣으면 된다).

**⚠️ 파일을 지우고 새로 만들면(대소문자만 바꾸는 것 포함) guid 가 바뀌어 참조가 끊긴다.**
`Register Audio Clips To Library` 는 이미 등록된 id 의 클립이 통째로 비어 있을 때만(끊어진 참조)
표의 경로로 자동 복구해준다. 하지만 **기존 파일을 다른 카드 용도로 재활용**(예: `Card_PassMic.wav` →
`Card_OpenMoshPit.wav`로 이름만 바꿔서 다른 카드 소리로 재사용)한 경우는 guid 는 그대로 유지되고
클립도 여전히 "유효"하기 때문에 자동으로 감지되지 않는다 — 이 경우 옛 id(`card_pass_mic`)의
`SoundLibrary` 항목과 `CardSfxPlayer.cardOverrides` 항목을 **직접 지워야** 한다. 안 지우면 두
카드가 같은 소리를 내는 채로 남는다.

**사운드 담당자용 요청 사항:** 위 "[클립 대기 중]" 7개를 각 카드 분위기에 맞는 원샷 효과음으로
채워서 정확히 표에 적힌 경로(`Assets/Audio/OneShot/Cards/Card_XXX.wav`)에 넣어주면 된다.
파일만 넣고 `Tools/Audio/Register Audio Clips To Library` → `Tools/Audio/Setup Crowd Ambience`
순서로 두 번 실행하면 카드 효과음까지 자동으로 연결된다 (코드 수정 불필요).

---

## 관객 반응 효과음 + 겹쳐 까는 앰비언스

```
Tools/Audio/Setup Audience Reaction And Ambience   →   Ctrl+S
```

기존 `[CrowdAmbience]` 오브젝트에 두 컴포넌트를 붙인다.
카드·점수 시스템을 수정하지 않고 `CardResolved` / `ComboChanged` 만 구독한다.

### `AudienceReactionSfx` — 타격감

카드 한 장의 획득 점수(`CardResolved.GainedScore`) 구간에 따라 관객이 반응한다.
구간 경계는 **`CardReactionTextUI` 의 라벨 구간과 같다** — 화면 문구와 소리가 어긋나지 않게.

| 점수 | 화면 문구 | 소리 |
|---|---|---|
| ≥ 20 | LOVE IT! | `BigStomp` 발 구르기 |
| ≥ 9 | INTERESTED | `Whistle` 휘파람 |
| −5 ~ 8 | (없음) | 무음 — 중립 구간에 소리를 넣으면 피로해진다 |
| ≥ −16 | NOT FOR ME | `cough_sarcastic` 헛기침 (1.2초로 자름) |
| 그 이하 | BORED | `cricket` 귀뚜라미 |
| Special Hit | SPECIAL! | `BigStomp` (피치 살짝 위) |

**야유는 반응을 대체하지 않고 위에 겹친다.** `booingMaxScore`(−25) 이하일 때
`crowd_low` 가 함께 울린다. `booingCooldown`(8초)으로 연속 실패에도 도배되지 않는다.

#### 클립을 잘라 쓴다

`cricket.wav` 는 귀뚜라미가 **4번 우는 2초짜리**라 그대로 쓰면 카드 한 장에 네 번 운다.
`startTime` 0.05 · `duration` 0.3 으로 첫 울음 하나만 쓴다.
`crowd_low.wav`(21초)도 앞 2.5초만 잘라 쓴다.

AudioManager 의 원샷 재생은 구간 지정을 지원하지 않아 자체 AudioSource 를 쓴다.
정지는 코루틴이 아니라 Update 에서 목표 시각과 비교한다 — 중간에 꺼져도 남는 상태가 없다.
피치를 올리면 구간이 더 빨리 지나가므로 정지 시각도 그만큼 당긴다.

### `StageAmbienceLayers` — 겹쳐 까는 앰비언스

`CrowdAmbienceSystem`(호응도 티어를 **하나씩 크로스페이드**)과 목적이 다르다.
이쪽은 여러 소리가 **동시에 겹쳐 쌓이는** 구조로, 겹마다 AudioSource 를 하나씩 갖는다.

| 겹 | 볼륨 | 조건 |
|---|---|---|
| `amp_noise_ambience` | 0.35 | 항상 |
| `festival_noise_ambience` | 0.18 | 항상 (낮게) |
| `crowd_middle` | 0.5 | 콤보 3 이상 |
| `crowd_high` | 0.5 | 콤보 7 이상 |

`minCombo` 가 0이면 항상, 0보다 크면 그 콤보에서 페이드로 들어오고 떨어지면 다시 빠진다.
`startOffset` 으로 겹마다 다른 지점에서 시작해 같은 파형이 뭉쳐 울리지 않게 한다.

볼륨은 기존 앰비언스 채널(`CrowdAmbience.Volume`)을 따르므로 **옵션 슬라이더가 그대로 적용**된다.
(`CrowdAmbienceSystem` 컴포넌트는 씬에서 꺼져 있지만 `Awake` 는 돌아 볼륨 저장/조회는 살아 있다)

### 확인

Play 중 인스펙터 `⋮` 메뉴:

| 컴포넌트 | 메뉴 |
|---|---|
| `AudienceReactionSfx` | `Debug/Play Great · Good · Bad · Awful · Booing` |
| `StageAmbienceLayers` | `Debug/Force Combo 10` · `Force Combo 0` · `Start` · `Stop` |
