using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class FeverSystem : MonoSingleton<FeverSystem>
    {
        [SerializeField] ComboFeverConfig config;

        bool _isActive;
        bool _startAfterCurrentCard;
        bool _automaticTriggerEnabled = true;
        bool _bossPresentationPaused;
        float _remaining;
        float _activeDuration;
        int _triggerCombo;
        float _pendingDurationOverride = -1f; // 앙코르 피버 예약 시간. 음수면 config 기본값

        protected override bool Persistent => false;

        public bool IsActive => _isActive;
        public float Remaining => Mathf.Max(0f, _remaining);
        public ComboFeverConfig Config => config;
        public bool AutomaticTriggerEnabled => _automaticTriggerEnabled;

        protected override void OnAwake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[FeverSystem] A ComboFeverConfig reference is required.",
                    this);
            }
        }

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        void Update()
        {
            if (!_isActive || _bossPresentationPaused ||
                !GameManager.HasInstance ||
                !GameManager.Instance.IsPlaying)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
                EndFever();
        }

        // 보스 카메라 연출로 카드 입력이 잠긴 동안에는 피버 잔여 시간을 보존한다.
        public void SetBossPresentationPaused(bool paused) => _bossPresentationPaused = paused;

        public int CalculateCardBonus(int audienceCount)
        {
            if (!_isActive || config == null) return 0;
            // 피버 중에는 콤보가 멈춰 있으므로 시작 시점의 콤보 배율이 끝까지 유지된다.
            float comboMultiplier = ComboSystem.HasInstance
                ? ComboSystem.Instance.CurrentMultiplier
                : 1f;
            return Mathf.RoundToInt(
                Mathf.Max(0, audienceCount) * config.FeverScorePerAudience * comboMultiplier);
        }

        /// <summary>
        /// 앙코르 카드용. 현재 카드 결과(CardResolved)가 발행된 직후 지정한 시간만큼 피버를 시작한다.
        /// 자동 발동 게이트와 무관하며, 이미 활성 중이거나 예약돼 있으면 false.
        /// </summary>
        public bool ReserveEncoreFever(float duration)
        {
            if (_isActive || _startAfterCurrentCard || config == null || duration <= 0f) return false;
            _startAfterCurrentCard = true;
            _triggerCombo = ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            _pendingDurationOverride = duration;
            return true;
        }

        /// <summary>[튜토리얼 전용] 실제 콤보 조건과 무관하게 피버타임을 즉시 시작한다. 이미 활성 중이면 무시.</summary>
        public void ForceStart()
        {
            if (_isActive) return;
            StartFever(config != null ? config.FeverComboInterval : 0);
        }

        /// <summary>
        /// 자동 콤보 발동만 켜고 끈다. ForceStart는 튜토리얼처럼 명시적으로
        /// 피버를 체험시켜야 하는 경우를 위해 이 게이트를 우회한다.
        /// </summary>
        public void SetAutomaticTriggerEnabled(bool enabled)
        {
            _automaticTriggerEnabled = enabled;
            if (!enabled)
            {
                _startAfterCurrentCard = false;
                _pendingDurationOverride = -1f;
            }
        }

        /// <summary>활성/예약된 피버를 즉시 정리한다. 자동 발동 설정은 유지한다.</summary>
        public void CancelFever() => ResetFever();

        void OnComboChanged(ComboChanged e)
        {
            if (!_automaticTriggerEnabled ||
                _isActive ||
                _startAfterCurrentCard ||
                config == null ||
                e.CurrentCombo <= e.PreviousCombo ||
                e.CurrentCombo <= 0)
                return;

            int interval = Mathf.Max(1, config.FeverComboInterval);
            int previousMilestone = e.PreviousCombo / interval;
            int currentMilestone = e.CurrentCombo / interval;
            if (currentMilestone <= previousMilestone) return;

            _startAfterCurrentCard = true;
            _triggerCombo = currentMilestone * interval;
        }

        void OnCardResolved(CardResolved e)
        {
            if (_startAfterCurrentCard)
            {
                _startAfterCurrentCard = false;
                StartFever(_triggerCombo);
                return;
            }

            if (_isActive && e.FeverBonusScore > 0)
            {
                EventBus.Raise(new FeverBonusAwarded(
                    e.CardId,
                    e.FeverAudienceCount,
                    e.FeverBonusScore));
            }
        }

        void StartFever(int triggerCombo)
        {
            if (config == null) return;

            float baseDuration = _pendingDurationOverride > 0f
                ? _pendingDurationOverride
                : config.FeverDuration;
            _pendingDurationOverride = -1f;
            float duration = baseDuration + AugmentRuntime.Current.FeverDurationBonus;
            _isActive = true;
            _remaining = duration;
            _activeDuration = duration;
            _triggerCombo = triggerCombo;
            EventBus.Raise(new FeverStateChanged(
                true,
                triggerCombo,
                duration));
        }

        void EndFever()
        {
            if (!_isActive) return;

            _isActive = false;
            _remaining = 0f;
            EventBus.Raise(new FeverStateChanged(
                false,
                _triggerCombo,
                _activeDuration));
            _activeDuration = 0f;
        }

        void ResetFever()
        {
            bool wasActive = _isActive;
            _isActive = false;
            _startAfterCurrentCard = false;
            _remaining = 0f;
            _activeDuration = 0f;
            _triggerCombo = 0;
            _pendingDurationOverride = -1f;

            if (wasActive)
                EventBus.Raise(new FeverStateChanged(false, 0, 0f));
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready ||
                e.Current == GameState.GameOver)
                ResetFever();
        }
    }
}
