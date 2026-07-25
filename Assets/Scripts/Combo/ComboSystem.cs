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

        protected override bool Persistent => false;

        public int CurrentCombo => _currentCombo;
        public float CurrentMultiplier => ResolveMultiplier(_currentCombo);

        protected override void OnAwake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[ComboSystem] A ComboFeverConfig reference is required.",
                    this);
            }
        }

        void OnEnable() =>
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable() =>
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        public ComboResolution ResolveCard(
            CardRole role,
            int rawScore,
            bool isSpecialHit)
        {
            if (role == CardRole.Utility)
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

            if (succeeded)
                _currentCombo = previous + 1;
            else if (failed)
                _currentCombo = 0;

            float multiplier = ResolveMultiplier(_currentCombo);

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

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready ||
                e.Current == GameState.GameOver)
                ResetCombo();
        }

    }
}
