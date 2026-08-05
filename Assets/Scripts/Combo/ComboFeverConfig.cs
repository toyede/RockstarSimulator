using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "ComboFeverConfig",
        menuName = "Context Stage/Combo Fever Config")]
    public sealed class ComboFeverConfig : ScriptableObject
    {
        [Header("Combo Score Multipliers")]
        [SerializeField, Min(1f)] float twoComboMultiplier = 1.35f;
        [SerializeField, Min(1f)] float fourComboMultiplier = 2f;
        [SerializeField, Min(1f)] float sixComboMultiplier = 3f;

        [Header("Fever")]
        [SerializeField, Min(1)] int feverComboInterval = 5;
        [SerializeField, Min(0.1f)] float feverDuration = 3f;
        [SerializeField, Min(0)] int feverScorePerAudience = 10;
        [SerializeField, Min(0f)] float feverEngagementGainPerAudience = 2f;

        public int FeverComboInterval => Mathf.Max(1, feverComboInterval);
        public float FeverDuration => Mathf.Max(0.1f, feverDuration);
        public int FeverScorePerAudience => Mathf.Max(0, feverScorePerAudience);
        public float FeverEngagementGainPerAudience =>
            Mathf.Max(0f, feverEngagementGainPerAudience);

        public float ResolveMultiplier(int combo)
        {
            if (combo >= 6) return Mathf.Max(1f, sixComboMultiplier);
            if (combo >= 4) return Mathf.Max(1f, fourComboMultiplier);
            if (combo >= 2) return Mathf.Max(1f, twoComboMultiplier);
            return 1f;
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
            feverComboInterval = Mathf.Max(1, feverComboInterval);
            feverDuration = Mathf.Max(0.1f, feverDuration);
            feverScorePerAudience = Mathf.Max(0, feverScorePerAudience);
            feverEngagementGainPerAudience = Mathf.Max(
                0f,
                feverEngagementGainPerAudience);
        }
#endif
    }
}
