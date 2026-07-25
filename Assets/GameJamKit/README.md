# GameJamKit

게임잼 48~72시간 안에 "매번 다시 짜게 되는 것들"만 모은 Unity 스니펫 킷.
**외부 패키지 의존성 없음** (FMOD / DOTween / Cinemachine 전부 불필요). Unity 6 + URP 2D 기준.

모든 코드는 `GameJamKit` 네임스페이스에 있다. 사용하는 스크립트 상단에 한 줄:

```csharp
using GameJamKit;
```

---

## 목차

| 분류 | 스크립트 | 한 줄 요약 |
|---|---|---|
| Core | `MonoSingleton<T>` | 모든 매니저의 베이스. 자동 생성 + DontDestroyOnLoad |
| Core | `GameManager` | Ready/Playing/Paused/GameOver 상태머신 + 점수 |
| Core | `EventBus` | 타입 기반 전역 이벤트. 매니저 간 결합도 제거 |
| Core | `TimerUtils` | `this.DelayCall(2f, () => ...)` 코루틴 헬퍼 |
| Core | `HitStop` | 타격 순간 시간 정지 |
| Pooling | `PoolManager` | Instantiate/Destroy 대체. 총알·이펙트 필수 |
| Pooling | `DespawnAfter` | 수명이 지나면 자동 반납 |
| Audio | `AudioManager` / `Sound` | `Sound.Play("hit")` 한 줄 |
| Audio | `SoundLibrary` | 사운드 ID ↔ 클립 매핑 에셋 |
| Combat | `Health` | HP / 무적 / 사망 이벤트. 적·플레이어 공용 |
| Combat | `DamageOnContact` | 닿으면 데미지. 총알·가시·적 몸통 |
| Feedback | `CameraShake` | 트라우마 기반 화면 흔들림 |
| UI | `ScreenFader` | 페이드 인/아웃. 배치 불필요 |
| UI | `SceneLoader` | 페이드 + 비동기 씬 전환 |
| UI | `UIManager` | 팝업 스택 + ESC 닫기 + 씬 전환 진입점 |
| UI | `UIPopup` | 팝업 베이스 클래스 |
| UI | `HealthBar` | Health → Image(Filled) 연결 |
| Data | `ColorPalette` / `Palette` / `PaletteTint` | hex 코드 한 곳 관리 |
| Data | `EntityStats` | 적/아이템 스탯 SO 템플릿 |
| Save | `Save` | PlayerPrefs 래퍼 + 하이스코어 |
| Spawn | `WaveData` / `WaveManager` | 웨이브 기반 스폰 |

---

## 0. 최초 셋업 (5분)

1. `Tools/GameJamKit/Create Default Assets`
   → `Assets/GameJamKit/Resources/` 에 `SoundLibrary.asset`, `ColorPalette.asset` 생성 (기본 팔레트 7색 포함)
2. `Tools/GameJamKit/Create Managers In Scene`
   → 씬에 `[Managers]` 오브젝트와 GameManager / AudioManager / PoolManager / UIManager / CameraShake 배치
3. `SoundLibrary` 에 클립 등록, `ColorPalette` 에 아트가 준 hex 붙여넣기

`ScreenFader`, `TimerRunner` 는 필요할 때 **자동 생성**되므로 배치하지 않아도 된다.
사실 위 매니저들도 전부 자동 생성되지만, 인스펙터에서 값을 조정하려면 씬에 두는 편이 낫다.

> **씬 전환 주의**: 로드할 씬은 반드시 `File > Build Profiles(Build Settings)` 의 씬 목록에 등록되어 있어야 한다.

---

## 1. MonoSingleton&lt;T&gt;

모든 매니저의 베이스. `Instance` 접근 시 씬에 없으면 자동 생성되고, 중복 인스턴스는 스스로 파괴된다.

```csharp
public class ScoreManager : MonoSingleton<ScoreManager>
{
    protected override void OnAwake()   // Awake 대신 여기에 초기화 코드
    {
        // ...
    }
}

ScoreManager.Instance.DoSomething();
if (ScoreManager.HasInstance) { ... }   // 자동 생성을 원치 않을 때
```

| 멤버 | 설명 |
|---|---|
| `Instance` | 없으면 자동 생성 |
| `HasInstance` | 생성 여부만 확인 (생성하지 않음) |
| `Persistent` | `override` 로 false 주면 씬 전환 시 파괴됨 (기본 true) |
| `OnAwake()` | Awake 대신 오버라이드할 초기화 훅 |

⚠️ `Awake()` / `OnDestroy()` 를 직접 오버라이드하면 **반드시 `base.Awake()` 호출**. 아니면 싱글턴 등록이 안 된다.

---

## 2. GameManager (상태머신)

```csharp
GameManager.Instance.StartGame();       // Ready → Playing
GameManager.Instance.TogglePause();     // Playing ↔ Paused (Time.timeScale 자동 처리)
GameManager.Instance.GameOver();        // 하이스코어 자동 제출
GameManager.Instance.RestartScene();    // 페이드와 함께 현재 씬 재시작

GameManager.Instance.AddScore(100);
int score = GameManager.Instance.Score;
int best  = GameManager.Instance.HighScore;

if (GameManager.Instance.IsPlaying) { /* 입력 처리 */ }
```

상태 변경 구독 — 두 가지 방법 중 편한 쪽:

```csharp
// (1) C# 이벤트
void OnEnable()  => GameManager.Instance.OnStateChanged += HandleState;
void OnDisable() => GameManager.Instance.OnStateChanged -= HandleState;
void HandleState(GameState prev, GameState next) { ... }

// (2) EventBus (권장 — GameManager 를 참조하지 않아도 된다)
void OnEnable()  => EventBus.Subscribe<GameStateChanged>(OnStateChanged);
void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnStateChanged);
void OnStateChanged(GameStateChanged e) { if (e.Current == GameState.GameOver) ... }
```

허용되는 전이: `Ready→Playing`, `Playing→Paused/GameOver`, `Paused→Playing/GameOver`, `GameOver→Playing`, 그리고 언제든 `→Ready`.
잘못된 전이는 경고 로그만 남기고 무시된다.

---

## 3. EventBus

매니저·오브젝트 간 직접 참조를 없애 팀 작업 시 충돌을 줄인다.

```csharp
// 1) 이벤트 정의 (GameEvents.cs 에 추가 권장) — struct 라 GC 할당 없음
public struct PlayerDied { public Vector3 Position; }

// 2) 구독 / 해제  ★ OnDisable 에서 반드시 해제할 것
void OnEnable()  => EventBus.Subscribe<PlayerDied>(HandlePlayerDied);
void OnDisable() => EventBus.Unsubscribe<PlayerDied>(HandlePlayerDied);

// 3) 발행
EventBus.Raise(new PlayerDied { Position = transform.position });
```

킷이 기본 제공하는 이벤트 (`GameEvents.cs`):
`GameStateChanged`, `ScoreChanged`, `EntityDamaged`, `EntityDied`, `WaveStarted`, `WaveCleared`

이 프로젝트(CONTEXT STAGE)에서 추가한 이벤트 (`GameEvents.cs` 하단, 하단 "변경 이력" 참조):
`HypeChanged`, `HypeJudgementApplied` + `HypeJudgement` enum

- 같은 핸들러를 두 번 구독해도 중복 등록되지 않는다.
- 핸들러 하나가 예외를 던져도 나머지 핸들러는 정상 실행된다.
- `EventBus.ClearAll()` 로 전체 해제 (플레이 시작 시 자동 호출됨).

---

## 4. TimerUtils

`MonoBehaviour` 확장 메서드라 어떤 컴포넌트에서든 `this.` 로 부를 수 있다.

```csharp
this.DelayCall(2f, () => Debug.Log("2초 뒤"));
this.DelayCall(1f, Explode, unscaledTime: true);   // 일시정지 중에도 진행
this.DelayFrames(1, RefreshLayout);                // 다음 프레임
this.Repeat(0.5f, Fire, count: 10);                // 0.5초마다 10번
this.WaitUntilThen(() => target != null, Attack);

// 간단 트윈 (DOTween 대용)
this.TweenFloat(0f, 1f, 0.3f, v => sprite.color = new Color(1, 1, 1, v));

// 취소
Coroutine _routine;
_routine = this.DelayCall(5f, Boom);
this.Cancel(ref _routine);

// MonoBehaviour 가 없는 곳에서
TimerUtils.Delay(1f, () => Debug.Log("전역 러너에서 실행"));
```

대상이 비활성/파괴 상태면 전역 `TimerRunner` 로 자동 대체 실행되므로 `StartCoroutine` 예외가 나지 않는다.

---

## 5. PoolManager (오브젝트 풀)

`Instantiate` / `Destroy` 를 그대로 대체한다. 총알·적·이펙트에는 **반드시** 사용할 것.

```csharp
// 생성
var bullet = PoolManager.Spawn(bulletPrefab, muzzle.position, muzzle.rotation);

// 컴포넌트 타입으로 바로 받기
Bullet b = PoolManager.Spawn(bulletComponentPrefab, pos, rot);

// 반납 (Destroy 대신)
PoolManager.Despawn(gameObject);
PoolManager.Despawn(gameObject, 2f);      // 2초 뒤

// 미리 채우기 — 로딩 중에 호출하면 첫 발사 렉이 사라진다
PoolManager.Prewarm(bulletPrefab, 50);

// 정리
PoolManager.DespawnAll(enemyPrefab);   // 특정 프리팹 전부 회수
PoolManager.DespawnAll();              // 전부 회수 (씬 재시작 시)
```

- 풀 인스턴스가 아닌 오브젝트에 `Despawn` 을 호출하면 그냥 `Destroy` 되므로 안전하게 섞어 쓸 수 있다.
- 중복 `Despawn` 은 무시된다.

### 재사용 시 상태 초기화 — `IPoolable`

풀에서 꺼낸 오브젝트는 `Awake` 가 다시 호출되지 않는다. 초기화가 필요하면:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    Rigidbody2D _rb;
    void Awake() => _rb = GetComponent<Rigidbody2D>();

    public void OnSpawned()  { _rb.linearVelocity = transform.right * 10f; }
    public void OnDespawned() { _rb.linearVelocity = Vector2.zero; }
}
```

### 부가 컴포넌트

- **`DespawnAfter`** — 프리팹에 붙이면 `lifetime` 초 뒤 자동 반납. `useParticleDuration` 을 켜면 파티클 길이를 수명으로 사용.
- **`PoolPrewarmer`** — 씬에 하나 두고 프리팹+개수를 등록하면 시작 시 자동 예열.

---

## 6. AudioManager / Sound (FMOD 미사용)

팀원이 쓰는 API는 사실상 이것뿐이다:

```csharp
Sound.Play("hit");                       // SFX
Sound.Play("hit", 0.5f);                 // 볼륨 50%
Sound.PlayAt("explosion", transform.position);   // 위치 기반(거리 감쇠)
Sound.Bgm("stage1");                     // BGM 크로스페이드 재생
Sound.StopBgm(0.5f);

// 옵션 슬라이더에 그대로 연결 (PlayerPrefs 자동 저장)
Sound.MasterVolume = slider.value;
Sound.BgmVolume    = slider.value;
Sound.SfxVolume    = slider.value;
```

### SoundLibrary 설정

`Create/GameJamKit/Sound Library` → **`Assets/GameJamKit/Resources/SoundLibrary.asset`** 이름으로 저장하면 자동 로드된다.
(다른 위치에 두려면 씬의 AudioManager 인스펙터에 직접 넣거나 `AudioManager.Instance.SetLibrary(lib)` 호출)

| 필드 | 설명 |
|---|---|
| `id` | 코드에서 부를 이름 (`"hit"`, `"bgm_main"`) |
| `clips` | 여러 개 넣으면 **랜덤 재생** → 반복감이 사라진다 |
| `volume` | 개별 볼륨 |
| `pitchMin/Max` | 다르게 주면 매번 피치 랜덤 → 훨씬 덜 지겹다 |
| `loop` | BGM/앰비언트용 |
| `minInterval` | 같은 사운드가 이 간격 안에 겹쳐 나오지 않게 함 (기본 0.02초) |

동시 재생은 SFX 보이스 12개(`sfxVoices`)로 돌려 쓰며, 모자라면 가장 오래된 소리를 뺏는다.
BGM은 AudioSource 2개로 크로스페이드하며, 페이드는 `unscaledTime` 기반이라 일시정지 중에도 동작한다.

---

## 7. Health / DamageOnContact

### Health

적·플레이어 공용. 오브젝트에 붙이고 인스펙터에서 설정한다.

```csharp
health.TakeDamage(25f, attacker);        // 간단 버전
health.TakeDamage(new DamageInfo {       // 상세 버전
    Amount = 25f, Source = gameObject,
    Direction = dir, Critical = true
});

health.Heal(10f);
health.Kill();                            // 무적 무시 즉사
health.Revive(0.5f);                      // 체력 50%로 부활
health.GrantInvincibility(1.5f);          // 대시/리스폰 무적
float ratio = health.Normalized;          // 0~1 (UI용)
```

구독:

```csharp
void OnEnable()  { health.OnDamaged += Flash; health.OnDeath += Explode; }
void OnDisable() { health.OnDamaged -= Flash; health.OnDeath -= Explode; }
```

인스펙터에서 바로 연결할 수 있는 UnityEvent 도 있다: `onHealthChanged01(float)`, `onDamagedEvent`, `onDeathEvent`.

| 인스펙터 항목 | 설명 |
|---|---|
| `maxHealth` | 최대 체력 |
| `invincibleDuration` | 피격 후 무적 시간 (0이면 없음) |
| `disableOnDeath` | 사망 시 자동으로 풀 반납/파괴 |
| `deathDelay` | 사망 연출을 위한 정리 지연 |
| `deathEffect` | 사망 시 스폰할 이펙트 프리팹 (자동 풀링) |
| `hitSoundId` / `deathSoundId` | 사운드 ID (SoundLibrary 기준) |
| `hitShake` | 피격 시 카메라 흔들림 세기 (0이면 없음) |

사망 시 `EntityDied`, 피격 시 `EntityDamaged` 이벤트가 EventBus 로 자동 발행되므로,
점수·킬 카운트·UI 는 **Health 를 참조하지 않고도** 반응할 수 있다.

```csharp
void OnEnable()  => EventBus.Subscribe<EntityDied>(OnEntityDied);
void OnDisable() => EventBus.Unsubscribe<EntityDied>(OnEntityDied);
void OnEntityDied(EntityDied e)
{
    if (e.Entity.CompareTag("Enemy")) GameManager.Instance.AddScore(10);
}
```

### DamageOnContact

`Collider2D`(대개 `isTrigger`)가 있는 오브젝트에 붙인다. 총알이면 `Rigidbody2D`(Kinematic)도 함께.

| 항목 | 설명 |
|---|---|
| `damage` / `targetLayers` | 데미지량, 맞힐 레이어 |
| `despawnAfterHit` | 맞으면 자기 자신 반납 (총알 = true, 가시 = false) |
| `repeatInterval` | 접촉 유지 중 재타격 간격 (0이면 진입 시에만) |
| `hitEffect` / `hitSoundId` / `shake` / `hitStop` | 타격감 연출 한 번에 |

`IDamageable` 은 부모까지 탐색(`GetComponentInParent`)하므로, 자식 콜라이더 구조에서도 동작한다.

---

## 8. CameraShake / HitStop

```csharp
CameraShake.Shake(0.2f);            // 약함
CameraShake.Shake(0.4f);            // 보통
CameraShake.Shake(0.7f);            // 강함 (0~1)
CameraShake.ShakeFor(0.4f, 0.6f);   // 0.6초 동안 지속 (보스 등장 등)
CameraShake.StopShake();

HitStop.Do(0.06f);                  // 타격 순간 60ms 정지 → 타격감 급상승
```

- 카메라를 지정하지 않으면 `Camera.main` 을 사용한다. (`CameraShake.Instance.SetTarget(t)` 로 변경 가능)
- 매 프레임 이전 오프셋을 되돌린 뒤 새로 적용하므로 **카메라 팔로우 스크립트와 충돌하지 않는다.**
- `unscaledTime` 기반이라 히트스톱 중에도 흔들린다.

---

## 9. ScreenFader / SceneLoader

```csharp
ScreenFader.Instance.FadeOut(0.4f, () => Debug.Log("암전 완료"));
ScreenFader.Instance.FadeIn(0.4f);
ScreenFader.Instance.Transition(0.3f, () => player.position = spawnPoint); // 암전 중 처리 후 복귀
ScreenFader.Instance.SetColor(Color.white);   // 화이트 아웃
```

캔버스를 런타임에 스스로 만들기 때문에 **씬에 아무것도 배치하지 않아도 된다.** 정렬 순서 30000이라 모든 UI 위에 뜬다.

```csharp
SceneLoader.Load("Stage2");            // 페이드 + 비동기 로드
SceneLoader.Reload();                  // 현재 씬 재시작
SceneLoader.LoadNext();                // Build Settings 상 다음 씬 (마지막이면 첫 씬으로)
SceneLoader.Quit();                    // 에디터에서는 플레이 종료

SceneLoader.OnProgress += p => loadingBar.fillAmount = p;   // 0~1
```

씬 전환 시 `Time.timeScale` 이 1로 복구되므로, 일시정지 상태로 넘어가 멈춰버리는 사고가 없다.

---

## 10. UIManager / UIPopup

### 팝업 만들기

1. Canvas 아래에 팝업 오브젝트를 만든다 (`CanvasGroup` 자동 추가됨)
2. `UIPopup` 을 상속한 스크립트를 붙인다 (그대로 `UIPopup` 을 붙여도 된다)
3. **팝업 루트 오브젝트는 활성 상태로 둘 것** — 그래야 자동 등록된다. `startHidden` 이 켜져 있으면 시작 시 알아서 숨는다.

```csharp
public class PausePopup : UIPopup
{
    protected override void OnOpen()  { /* 열릴 때 데이터 갱신 */ }
    protected override void OnClose() { }

    public void OnClickResume()  => Close();          // 버튼 OnClick 에 연결
    public void OnClickRestart() => GameManager.Instance.RestartScene();
}
```

```csharp
UIManager.Instance.Open<PausePopup>();
UIManager.Instance.Close<PausePopup>();
UIManager.Instance.CloseTop();          // ESC 로도 자동 실행
UIManager.Instance.CloseAll();
if (UIManager.Instance.AnyPopupOpen) { /* 게임 입력 무시 */ }
```

| UIPopup 인스펙터 | 설명 |
|---|---|
| `startHidden` | 시작 시 닫힌 상태로 (기본 켜짐) |
| `pauseGameWhileOpen` | 열려 있는 동안 GameManager 일시정지 |
| `closableByEscape` | ESC 로 닫히는 팝업인지 |
| `animDuration` / `scaleAnimation` | 페이드 + 살짝 커지는 연출 (unscaledTime 기반) |
| `openSoundId` / `closeSoundId` | 열기/닫기 사운드 |

`UIManager` 에는 버튼 `OnClick` 에 바로 연결할 수 있는 래퍼도 있다:
`StartGame()`, `TogglePause()`, `RestartGame()`, `LoadScene(string)`, `ReloadScene()`, `LoadNextScene()`, `QuitGame()`

### HealthBar

`Image` 의 Image Type 을 **Filled** 로 설정한 뒤, `HealthBar` 의 `fillImage` 에 넣고 `target` 에 `Health` 를 연결한다.
`delayedImage` 를 추가로 넣으면 뒤늦게 따라오는 빨간 바(격투게임 스타일)가 된다.
적 머리 위 월드 스페이스 캔버스에서도 동일하게 동작한다 (`target` 을 비워두면 부모에서 자동 탐색).

---

## 11. ColorPalette / Palette / PaletteTint

아트가 준 hex 를 한 곳에 모아두고, 팀원은 색을 직접 찍지 않는다.

**세팅**: `Create/GameJamKit/Color Palette` → `Assets/GameJamKit/Resources/ColorPalette.asset`
각 항목에 `key` 와 `hex`(`#RRGGBB` 또는 `#RRGGBBAA`)를 넣으면 옆 Color 가 자동 갱신된다.

```csharp
sprite.color = Palette.Get("player");
image.color  = Palette.Get("ui_bg");

Color c = Palette.FromHex("#FF3366");     // 임시 변환
string hex = Palette.ToHex(c);
```

**코드 없이 쓰기** — `PaletteTint` 컴포넌트를 `SpriteRenderer` 나 UI `Graphic` 오브젝트에 붙이고 `colorKey` 만 입력.
에디터에서도 즉시 반영되고, 팔레트의 hex 를 바꾸면 씬 전체가 따라온다.

---

## 12. EntityStats

적·아이템 스탯을 코드에서 분리하는 ScriptableObject 템플릿. `Create/GameJamKit/Entity Stats`

```csharp
[SerializeField] EntityStats stats;

void Start()
{
    stats.ApplyTo(GetComponent<Health>());
    _speed = stats.moveSpeed;
}
```

필드(`maxHealth`, `damage`, `moveSpeed`, `scoreValue`, `dropChance` …)는 프로젝트에 맞게 자유롭게 추가/삭제할 것.
같은 프리팹에 다른 스탯 에셋만 갈아끼우면 몹 종류가 늘어난다.

---

## 13. Save (PlayerPrefs 래퍼)

```csharp
Save.SetInt("stage", 3);
int stage = Save.GetInt("stage", 1);       // 기본값 1

Save.SetBool("tutorialDone", true);
Save.SetFloat("volume", 0.8f);

// 하이스코어
if (Save.TrySetHighScore(score)) ShowNewRecord();
int best = Save.GetHighScore();

// 클래스 통째로 (JsonUtility)
[System.Serializable] public class Progress { public int stage; public int coins; }
Save.SetObject("progress", progress);
var loaded = Save.GetObject<Progress>("progress");

Save.Delete("stage");
Save.DeleteAll();          // 또는 Tools/GameJamKit/Clear Saved Data
```

모든 키에 `gjk_` 접두사가 자동으로 붙는다. `JsonUtility` 는 `Dictionary` 를 지원하지 않으니 주의.

---

## 14. WaveManager / WaveData

**세팅**: `Create/GameJamKit/Wave Data` 로 에셋 생성 → 빈 오브젝트에 `WaveManager` 를 붙이고 `waveData` 연결.
`spawnPoints` 에 Transform 들을 넣으면 그 중 랜덤, 비워두면 오브젝트 주변 `spawnRadius` 원형에 스폰한다. (씬 뷰에 기즈모 표시)

WaveData 구조:

```
WaveData
└ Wave[0]  name / waitUntilCleared / delayAfterClear
   └ SpawnEntry[]  prefab / count / interval / startDelay
```

- `waitUntilCleared` — 전멸해야 다음 웨이브로 (false면 스폰 종료 즉시 진행)
- `startDelay` 가 다른 SpawnEntry 를 여러 개 두면 "잡몹 5초 뒤 정예 등장" 같은 구성이 된다
- `loopLastWave` — 마지막 웨이브 후 처음부터 무한 반복

```csharp
waveManager.StartWaves();     // autoStartOnPlaying 이 켜져 있으면 Playing 진입 시 자동 호출
waveManager.StopWaves();
waveManager.ResetWaves();     // 남은 적 회수 + 카운터 초기화

waveManager.OnWaveStarted    += i => ShowBanner($"WAVE {i + 1}");
waveManager.OnWaveCleared    += i => GameManager.Instance.AddScore(100);
waveManager.OnAllWavesCleared += () => ShowVictory();

int alive = waveManager.AliveCount;
```

스폰은 전부 `PoolManager` 를 통하며, 생존 판정은 0.25초마다 `Health.IsDead` 와 활성 상태로 확인한다
(풀링된 적은 파괴되지 않으므로 `== null` 검사만으로는 부족하다).

---

## 15. .unitypackage 로 내보내기

```
Tools/GameJamKit/Export .unitypackage
```

저장 위치를 고르면 `Assets/GameJamKit` 전체가 하위 폴더 포함해서 패키징된다.
다른 프로젝트에서는 `Assets > Import Package > Custom Package` 로 가져온 뒤,
`Tools/GameJamKit/Create Default Assets` 와 `Create Managers In Scene` 만 실행하면 바로 쓸 수 있다.

> 내보내기 전 콘솔에 컴파일 에러가 없는지 확인할 것. 에러가 있는 상태로도 패키지는 만들어진다.

---

## 자주 하는 실수

| 증상 | 원인 / 해결 |
|---|---|
| `Sound.Play("hit")` 가 무음 + 경고 로그 | SoundLibrary 가 `Resources` 폴더에 `SoundLibrary` 이름으로 없거나, `id` 오타 |
| 풀에서 나온 총알이 안 움직임 | `Awake` 에서 초기화함 → `IPoolable.OnSpawned()` 로 옮길 것 |
| 팝업이 `Open<T>()` 로 안 열림 | 씬에서 팝업 루트가 비활성 상태 → 활성으로 두고 `startHidden` 사용 |
| 일시정지 중 UI 애니메이션이 멈춤 | `Time.deltaTime` 대신 `Time.unscaledDeltaTime` 사용 (킷 내부는 이미 그렇게 되어 있음) |
| 씬 전환 후 이벤트가 두 번 호출됨 | `OnDisable` 에서 `EventBus.Unsubscribe` 를 빠뜨림 |
| 싱글턴이 null | `Awake()` 오버라이드 시 `base.Awake()` 누락 |
| 카메라 흔들림이 안 보임 | 씬에 `MainCamera` 태그가 붙은 카메라가 없음 |

---

## 변경 이력 (프로젝트에서 킷을 수정한 기록)


| 날짜 | 작업자 | 파일 | 내용 |
|---|---|---|---|
| 2026-07-24 | Claude | `Core/GameEvents.cs` | CONTEXT STAGE 호응도 이벤트 추가 — `HypeJudgement` enum, `HypeChanged`, `HypeJudgementApplied`, `EncoreTriggered`, `HypeDepleted`. (파일 상단 안내대로 프로젝트 고유 이벤트를 이 파일에 모음. 발행 주체는 `Assets/Scripts/Hype/HypeSystem.cs`) |
| 2026-07-24 | Claude | `Core/GameEvents.cs` | 관객 앰비언스 이벤트 `CrowdAmbienceTierChanged` 추가. (발행 주체는 `Assets/Scripts/Audio/CrowdAmbienceSystem.cs`. 앰비언스는 킷 BGM 채널을 쓰지 않고 자체 AudioSource 2개로 크로스페이드하므로 `AudioManager` 는 수정하지 않았다) |

| 2026-07-24 | Claude | `Core/GameEvents.cs` | 관객 비주얼 이벤트 `CrowdMoodChanged` 추가. (발행 주체는 `Assets/Scripts/Crowd/CrowdMoodDirector.cs`) |
| 2026-07-24 | Claude | `Core/GameEvents.cs` | 특별 관객 이벤트 `SpecialAudienceSpawned`, `SpecialAudienceEnded`, `SpecialHitLanded` 추가. (발행 주체는 `Assets/Scripts/SpecialAudience/SpecialAudienceManager.cs`. 타입은 `ContextStage` 네임스페이스라 정규화해서 참조한다) |
| 2026-07-24 | Codex | `Core/GameEvents.cs` | 병합 중 누락된 `CrowdAmbienceTierChanged`의 닫는 중괄호를 복구해 카드 이벤트 타입들을 `GameJamKit` 네임스페이스 직속으로 되돌림. |
| 2026-07-24 | Claude | `Core/GameEvents.cs` | 열기(점수 배율) 전환: `CardSelected` 구조체에 `BaseScore`(int)·`Multiplier`(float) 필드 추가. 카드 낼 때의 열기 배율을 실어 점수 담당(`Assets/Scripts/Score/PerformanceScoreSystem.cs`)이 구독해 `GameManager.AddScore` 로 누적한다. |
| 2026-07-25 | Codex | `Core/EventBus.cs`, `Core/TimerUtils.cs`, `Pooling/*` | 이벤트 발행 중 구독 변경을 안전하게 지연하고 발행 할당을 제거했다. 타이머의 실제 실행 호스트를 추적하며, 풀 인스턴스 재사용 시 이전 지연 Despawn이 새 대상을 반납하지 않도록 lease 버전을 추가했다. |
| 2026-07-25 | Codex | `Core/GameManager.cs`, `UI/UIPopup.cs`, `Core/HitStop.cs`, `Feedback/CameraShake.cs` | 중첩 팝업의 일시정지 소유권을 분리하고, HitStop·카메라 흔들림이 자신이 적용한 상태만 복원하도록 변경했다. |
| 2026-07-25 | Codex | `Save/Save.cs`, `Combat/Health.cs`, `Combat/DamageOnContact.cs`, `UI/HealthBar.cs`, `Spawn/WaveManager.cs` | 연속 저장을 dirty/flush 방식으로 바꾸고, 체력 UI를 이벤트 기반으로 전환했다. 대상별 접촉 피해 쿨다운과 웨이브 중단 후 자식 코루틴 취소를 보강했다. |
| 2026-07-25 | Codex | `Core/GameEvents.cs` | 실제 발행되지 않던 `EncoreTriggered`, `HypeDepleted`를 제거하고 `SpecialHitLanded`에 연출 유지시간을 추가해 시스템 간 중복 수치를 없앴다. |
| 2026-07-25 | Claude | `Core/GameEvents.cs` | 공연 시간(타이머) 이벤트 `PerformanceTimeChanged` 추가. (발행 주체는 `Assets/Scripts/Timer/PerformanceTimerSystem.cs`. 제한시간 초과 시 목표 점수 미달성이면 `GameManager.GameOver()`를 호출한다) |
| 2026-07-25 | Claude | `Core/GameEvents.cs` | 관객 유입/이탈 이벤트 `AudienceMemberSpawned`, `AudienceMemberExited`, `AudienceCountChanged` 추가. (발행 주체는 `Assets/Scripts/AudienceLifecycle/AudienceInflowSystem.cs`. 개인별 몰입도 시스템은 별도 담당자가 CrowdComposition 쪽에서 개편 중이며, 이 이벤트들은 그 작업이 끝나면 MemberId 를 키로 이어붙이도록 선행 구현한 것) |

