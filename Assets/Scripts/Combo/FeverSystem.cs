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
        float _remaining;
        int _triggerCombo;

        protected override bool Persistent => false;

        public bool IsActive => _isActive;
        public float Remaining => Mathf.Max(0f, _remaining);
        public ComboFeverConfig Config => config;

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
            if (!_isActive ||
                !GameManager.HasInstance ||
                !GameManager.Instance.IsPlaying)
                return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
                EndFever();
        }

        public int CalculateCardBonus(int audienceCount)
        {
            if (!_isActive || config == null) return 0;
            return Mathf.Max(0, audienceCount) * config.FeverScorePerAudience;
        }

        /// <summary>[튜토리얼 전용] 실제 콤보 조건과 무관하게 피버타임을 즉시 시작한다. 이미 활성 중이면 무시.</summary>
        public void ForceStart()
        {
            if (_isActive) return;
            StartFever(config != null ? config.FeverComboInterval : 0);
        }

        void OnComboChanged(ComboChanged e)
        {
            if (_isActive ||
                _startAfterCurrentCard ||
                config == null ||
                e.CurrentCombo <= e.PreviousCombo ||
                e.CurrentCombo <= 0 ||
                e.CurrentCombo % config.FeverComboInterval != 0)
                return;

            _startAfterCurrentCard = true;
            _triggerCombo = e.CurrentCombo;
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

            _isActive = true;
            _remaining = config.FeverDuration;
            _triggerCombo = triggerCombo;
            EventBus.Raise(new FeverStateChanged(
                true,
                triggerCombo,
                config.FeverDuration));
        }

        void EndFever()
        {
            if (!_isActive) return;

            _isActive = false;
            _remaining = 0f;
            EventBus.Raise(new FeverStateChanged(
                false,
                _triggerCombo,
                config != null ? config.FeverDuration : 0f));
        }

        void ResetFever()
        {
            bool wasActive = _isActive;
            _isActive = false;
            _startAfterCurrentCard = false;
            _remaining = 0f;
            _triggerCombo = 0;

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
