using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 호응도에 따라 관객 앰비언스(crowd_low / crowd_middle / crowd_high)를 자동으로 바꿔주는 시스템.
    ///
    /// - 공연 시작(Ready → Playing)에 재생 시작, 게임오버/Ready 로 돌아가면 페이드아웃
    /// - HypeChanged 이벤트를 구독해 티어를 재평가하고, 바뀌면 크로스페이드
    /// - 일시정지(Paused)에는 함께 멈춘다
    /// - 볼륨은 앰비언스 채널 + AudioManager 마스터 볼륨을 곱해 매 프레임 반영 (슬라이더 즉시 반응)
    ///
    /// 클립은 GameJamKit 의 SoundLibrary(ID → 클립)에서 가져오므로 다른 사운드와 같은 방식으로 관리된다.
    /// 자체 AudioSource 2개로 크로스페이드하기 때문에 AudioManager 의 BGM 채널(음악)과 충돌하지 않는다.
    ///
    /// [다른 담당자용 API]
    ///   CrowdAmbience.Volume = 0.5f;          // 옵션 슬라이더 (자동 저장)
    ///   CrowdAmbience.ForceTier("High");      // 연출용 강제 지정 (Release() 로 해제)
    ///   CrowdAmbience.SetMuted(true);         // 컷신 등에서 임시 음소거
    /// </summary>
    [DisallowMultipleComponent]
    public class CrowdAmbienceSystem : MonoSingleton<CrowdAmbienceSystem>
    {
        public const string VolumeSaveKey = "vol_ambience";

        [SerializeField, Tooltip("필수 티어·볼륨 밸런스 에셋")]
        CrowdAmbienceConfig config;

        [SerializeField, Tooltip("공연 시작 이벤트를 기다리지 않고 씬 시작과 동시에 재생")]
        bool playOnStart = false;

        AudioSource _a, _b;
        bool _usingA = true;          // 현재 '앞' 소스가 A 인가
        float _weightA, _weightB;     // 0~1 크로스페이드 가중치
        float _volA, _volB;           // 각 소스가 물고 있는 티어의 기본 볼륨

        int _currentTier = -1;        // 지금 재생 중인 티어 인덱스 (-1 = 없음)
        int _forcedTier = -1;         // 연출용 강제 티어 (-1 = 호응도 자동)
        float _fadeDuration = 1f;
        bool _active;                 // 재생(페이드인) 상태인가
        bool _muted;

        float _ambienceVolume = 1f;

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem 과 동일)</summary>
        protected override bool Persistent => false;

        public CrowdAmbienceConfig Config => config;
        public int CurrentTierIndex => _currentTier;
        public string CurrentTierName => config?.GetTier(_currentTier)?.tierName ?? "-";
        public bool IsMuted => _muted;

        /// <summary>앰비언스 채널 볼륨(0~1). 설정하면 PlayerPrefs 에 자동 저장된다.</summary>
        public float AmbienceVolume
        {
            get => _ambienceVolume;
            set
            {
                _ambienceVolume = Mathf.Clamp01(value);
                Save.SetFloat(VolumeSaveKey, _ambienceVolume);
            }
        }

        /// <summary>마스터 볼륨까지 곱한 최종 채널 배율. (연출 담당이 참고용으로 읽을 수 있다)</summary>
        public float EffectiveVolume =>
            _muted ? 0f : _ambienceVolume * (AudioManager.HasInstance ? AudioManager.Instance.MasterVolume : 1f);

        // ---------------- 수명 주기 ----------------

        protected override void OnAwake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[CrowdAmbience] CrowdAmbienceConfig is required. " +
                    "Run Tools/Audio/Setup Crowd Ambience.",
                    this);
                enabled = false;
                return;
            }

            _ambienceVolume = Save.GetFloat(VolumeSaveKey, config.defaultAmbienceVolume);
            _fadeDuration = config.crossfadeDuration;

            _a = CreateSource("Ambience_A");
            _b = CreateSource("Ambience_B");
        }

        void Start()
        {
            // 이미 공연이 진행 중인 씬(디버그 재생 등)에서도 자연스럽게 붙는다
            if (playOnStart || (GameManager.HasInstance && GameManager.Instance.IsPlaying))
                StartAmbience();
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<HypeChanged>(OnHypeChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<HypeChanged>(OnHypeChanged);
        }

        AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;        // 앰비언스는 항상 루프
            src.spatialBlend = 0f;  // 2D
            src.volume = 0f;
            return src;
        }

        // ---------------- 이벤트 반응 ----------------

        void OnGameStateChanged(GameStateChanged e)
        {
            switch (e.Current)
            {
                case GameState.Playing:
                    if (e.Previous == GameState.Paused) ResumeAmbience();
                    else StartAmbience();
                    break;

                case GameState.Paused:
                    PauseAmbience();
                    break;

                default: // Ready · GameOver
                    StopAmbience();
                    break;
            }
        }

        void OnHypeChanged(HypeChanged e)
        {
            if (!_active || _forcedTier >= 0) return;
            ApplyTier(config.ResolveTierIndex(e.Normalized, _currentTier));
        }

        // ---------------- 공개 API ----------------

        /// <summary>현재 호응도에 맞는 티어로 앰비언스를 시작한다. (공연 시작 시 자동 호출)</summary>
        public void StartAmbience()
        {
            _active = true;
            _fadeDuration = config.startFadeDuration;

            int target = _forcedTier >= 0 ? _forcedTier : config.ResolveTierIndex(Hype.Normalized);
            ApplyTier(target, force: true);
        }

        /// <summary>앰비언스를 페이드아웃하고 멈춘다.</summary>
        public void StopAmbience()
        {
            if (!_active) return;
            _active = false;
            _fadeDuration = config.stopFadeDuration;
            _currentTier = -1;
        }

        public void PauseAmbience()  { _a.Pause();   _b.Pause(); }
        public void ResumeAmbience() { _a.UnPause(); _b.UnPause(); }

        /// <summary>[연출 담당] 호응도와 무관하게 특정 티어로 고정. 해제는 ReleaseForcedTier().</summary>
        public void ForceTier(int index)
        {
            _forcedTier = config.ClampIndex(index);
            if (_active) ApplyTier(_forcedTier);
        }

        /// <summary>이름으로 강제 티어 지정. 예: ForceTier("High")</summary>
        public void ForceTier(string tierName)
        {
            int index = config.IndexOfTier(tierName);
            if (index < 0)
            {
                Debug.LogWarning($"[CrowdAmbience] '{tierName}' 티어를 콘픽에서 찾을 수 없습니다.");
                return;
            }
            ForceTier(index);
        }

        /// <summary>강제 티어를 풀고 다시 호응도를 따라가게 한다.</summary>
        public void ReleaseForcedTier()
        {
            _forcedTier = -1;
            if (_active) ApplyTier(config.ResolveTierIndex(Hype.Normalized, _currentTier));
        }

        /// <summary>임시 음소거. (연출·컷신용. 저장되지 않는다)</summary>
        public void SetMuted(bool muted) => _muted = muted;

        // ---------------- 내부: 티어 전환 ----------------

        void ApplyTier(int index, bool force = false)
        {
            index = config.ClampIndex(index);
            if (index < 0) return;
            if (!force && index == _currentTier) return;

            var tier = config.GetTier(index);
            var clip = ResolveClip(tier);
            if (clip == null)
            {
                Debug.LogWarning($"[CrowdAmbience] 티어 '{tier.tierName}' 의 클립을 찾을 수 없습니다. " +
                                 $"SoundLibrary 에 '{tier.soundId}' 가 등록되어 있는지 확인하세요.");
                return;
            }

            int previousTier = _currentTier;
            _currentTier = index;

            // 첫 재생은 StartAmbience 가 정한 페이드인 시간을 유지하고, 티어 전환일 때만 크로스페이드 시간을 쓴다
            if (previousTier >= 0)
                _fadeDuration = tier.fadeDurationOverride > 0f ? tier.fadeDurationOverride : config.crossfadeDuration;

            var next = _usingA ? _b : _a;   // 뒤에 있던 소스에 새 클립을 얹고 앞으로 끌어온다
            var prev = _usingA ? _a : _b;

            next.clip = clip;
            // 이전 소스와 같은 재생 위치에서 이어붙이면 군중 소리가 툭 끊기지 않는다
            next.time = prev.clip != null && prev.isPlaying ? Mathf.Repeat(prev.time, clip.length) : 0f;
            next.volume = 0f;
            next.Play();

            if (_usingA) _volB = tier.volume; else _volA = tier.volume;
            _usingA = !_usingA;

            EventBus.Raise(new CrowdAmbienceTierChanged
            {
                PreviousIndex = previousTier,
                Index = index,
                TierName = tier.tierName
            });
        }

        AudioClip ResolveClip(CrowdAmbienceTier tier)
        {
            if (tier == null) return null;
            if (tier.clipOverride != null) return tier.clipOverride;

            // AudioManager 가 씬에 없으면 Instance 접근 시 자동 생성되며 Resources/SoundLibrary 를 로드한다
            var manager = AudioManager.Instance;
            var library = manager != null ? manager.Library : null;
            return library != null ? library.Find(tier.soundId)?.PickClip() : null;
        }

        // ---------------- 내부: 볼륨/페이드 ----------------

        void Update()
        {
            // timeScale 0(일시정지)에서도 페이드가 진행되도록 unscaled 사용
            float step = _fadeDuration > 0f ? Time.unscaledDeltaTime / _fadeDuration : 1f;

            float targetA = _active && _usingA ? 1f : 0f;
            float targetB = _active && !_usingA ? 1f : 0f;

            _weightA = Mathf.MoveTowards(_weightA, targetA, step);
            _weightB = Mathf.MoveTowards(_weightB, targetB, step);

            float channel = EffectiveVolume; // 마스터/앰비언스 볼륨 변경이 매 프레임 반영된다
            ApplySource(_a, _weightA * _volA * channel);
            ApplySource(_b, _weightB * _volB * channel);
        }

        void ApplySource(AudioSource src, float volume)
        {
            src.volume = volume;

            // 페이드가 완전히 끝난 소스만 정지해 보이스를 아낀다.
            // (음소거는 볼륨만 0 이므로 재생 위치를 유지해야 한다 → weight 로 판단)
            float weight = src == _a ? _weightA : _weightB;
            if (weight <= 0f && src.isPlaying) src.Stop();
        }
    }

    /// <summary>
    /// 어디서든 한 줄로 관객 앰비언스를 제어하는 전역 접근자. (킷의 Sound / 프로젝트의 Hype 와 같은 패턴)
    ///
    ///   CrowdAmbience.Volume = slider.value;   // 옵션 볼륨 (자동 저장)
    ///   CrowdAmbience.ForceTier("High");       // 앙코르 연출 중 강제 고조
    ///   CrowdAmbience.ReleaseForcedTier();     // 연출 끝나면 호응도 추종으로 복귀
    /// </summary>
    public static class CrowdAmbience
    {
        public static bool Exists => CrowdAmbienceSystem.HasInstance;

        /// <summary>앰비언스 채널 볼륨(0~1). 씬에 시스템이 없으면 저장값만 읽고 쓴다.</summary>
        public static float Volume
        {
            get => CrowdAmbienceSystem.HasInstance
                ? CrowdAmbienceSystem.Instance.AmbienceVolume
                : Save.GetFloat(CrowdAmbienceSystem.VolumeSaveKey, 1f);
            set
            {
                if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.AmbienceVolume = value;
                else Save.SetFloat(CrowdAmbienceSystem.VolumeSaveKey, Mathf.Clamp01(value));
            }
        }

        public static string CurrentTier => CrowdAmbienceSystem.HasInstance
            ? CrowdAmbienceSystem.Instance.CurrentTierName : "-";

        public static void Play()  { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.StartAmbience(); }
        public static void Stop()  { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.StopAmbience(); }

        public static void ForceTier(string tierName) { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.ForceTier(tierName); }
        public static void ForceTier(int index)       { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.ForceTier(index); }
        public static void ReleaseForcedTier()        { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.ReleaseForcedTier(); }
        public static void SetMuted(bool muted)       { if (CrowdAmbienceSystem.HasInstance) CrowdAmbienceSystem.Instance.SetMuted(muted); }
    }
}
