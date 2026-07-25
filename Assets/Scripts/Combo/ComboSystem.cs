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
        [Header("Score Multipliers")]
        [SerializeField, Min(1f)] float twoComboMultiplier = 1.2f;
        [SerializeField, Min(1f)] float fourComboMultiplier = 1.5f;
        [SerializeField, Min(1f)] float sixComboMultiplier = 2f;

        int _currentCombo;

        protected override bool Persistent => false;

        public int CurrentCombo => _currentCombo;
        public float CurrentMultiplier => ResolveMultiplier(_currentCombo);

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
            _currentCombo = succeeded ? previous + 1 : 0;
            float multiplier = ResolveMultiplier(_currentCombo);

            EventBus.Raise(new ComboChanged(
                previous,
                _currentCombo,
                multiplier,
                !succeeded && previous > 0));

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
        {
            if (combo >= 6) return Mathf.Max(1f, sixComboMultiplier);
            if (combo >= 4) return Mathf.Max(1f, fourComboMultiplier);
            if (combo >= 2) return Mathf.Max(1f, twoComboMultiplier);
            return 1f;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready ||
                e.Current == GameState.GameOver)
                ResetCombo();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            twoComboMultiplier = Mathf.Max(1f, twoComboMultiplier);
            fourComboMultiplier = Mathf.Max(
                twoComboMultiplier,
                fourComboMultiplier);
            sixComboMultiplier = Mathf.Max(
                fourComboMultiplier,
                sixComboMultiplier);
        }
#endif
    }
}
