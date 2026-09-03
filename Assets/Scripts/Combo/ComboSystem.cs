using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    public readonly struct ComboResolution
    {
        public ComboResolution(
            int combo,
            float multiplier,
            bool succeeded)
        {
            Combo = combo;
            Multiplier = multiplier;
            Succeeded = succeeded;
        }

        public int Combo { get; }
        public float Multiplier { get; }
        public bool Succeeded { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ComboSystem : MonoSingleton<ComboSystem>
    {
        [SerializeField] ComboFeverConfig config;

        int _currentCombo;
        int _remainingComboBreakPreventions;

        protected override bool Persistent => false;

        public int CurrentCombo => _currentCombo;
        public float CurrentMultiplier => ResolveMultiplier(_currentCombo);
        public int RemainingComboBreakPreventions => _remainingComboBreakPreventions;

        protected override void OnAwake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[ComboSystem] A ComboFeverConfig reference is required.",
                    this);
            }

            RestoreComboBreakPreventions();
        }

        void OnEnable() =>
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable() =>
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        public ComboResolution ResolveCard(
            CardRole role,
            int rawScore,
            bool isSpecialHit,
            bool countsAsPerformance = false)
        {
            if (role == CardRole.Utility && !countsAsPerformance)
            {
                return new ComboResolution(
                    _currentCombo,
                    CurrentMultiplier,
                    false);
            }

            int previous = _currentCombo;
            bool succeeded = isSpecialHit || rawScore > 0;
            bool failed = !isSpecialHit && rawScore < 0;

            if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive)
            {
                return new ComboResolution(
                    _currentCombo,
                    CurrentMultiplier,
                    succeeded);
            }

            bool comboLossPrevented =
                failed &&
                previous > 0 &&
                TryPreventComboLoss(previous);

            if (succeeded)
                _currentCombo = previous +
                    AugmentRuntime.Current.ComboGainPerSuccessfulCard;
            else if (failed && !comboLossPrevented)
                _currentCombo = 0;

            // 보호가 발동한 실패 카드의 손실까지 기존 콤보 배율로 키우지는 않는다.
            // 콤보 상태만 유지하고, 유지된 배율은 다음 카드부터 다시 적용한다.
            float multiplier = comboLossPrevented
                ? 1f
                : ResolveMultiplier(_currentCombo);

            if (_currentCombo != previous)
            {
                EventBus.Raise(new ComboChanged(
                    previous,
                    _currentCombo,
                    multiplier,
                    failed && previous > 0));
            }

            return new ComboResolution(
                _currentCombo,
                multiplier,
                succeeded);
        }

        public void ResetCombo()
        {
            int previous = _currentCombo;
            _currentCombo = 0;
            EventBus.Raise(new ComboChanged(
                previous,
                0,
                1f,
                false));
        }

        float ResolveMultiplier(int combo)
            => config != null ? config.ResolveMultiplier(combo) : 1f;

        bool TryPreventComboLoss(int protectedCombo)
        {
            if (_remainingComboBreakPreventions <= 0) return false;

            _remainingComboBreakPreventions--;
            EventBus.Raise(new ComboLossPrevented(
                protectedCombo,
                _remainingComboBreakPreventions));
            return true;
        }

        void RestoreComboBreakPreventions()
        {
            _remainingComboBreakPreventions =
                AugmentRuntime.Current.ComboBreakPreventionCount;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
            {
                ResetCombo();
                RestoreComboBreakPreventions();
            }
            else if (e.Current == GameState.GameOver)
            {
                ResetCombo();
            }
        }

    }
}
