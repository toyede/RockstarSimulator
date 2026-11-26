using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>이 콤보 수 이상일 때 적용할 점수 배율 한 단계.</summary>
    [Serializable]
    public struct ComboMultiplierStep
    {
        [Min(1)] public int minCombo;
        [Min(1f)] public float multiplier;
    }

    [CreateAssetMenu(
        fileName = "ComboFeverConfig",
        menuName = "Context Stage/Combo Fever Config")]
    public sealed class ComboFeverConfig : ScriptableObject
    {
        [Header("Combo Score Multipliers")]
        [SerializeField, Tooltip("콤보 단계별 배율. 비어 있으면 아래 2·4·6 콤보 값을 사용한다")]
        List<ComboMultiplierStep> multiplierSteps = new List<ComboMultiplierStep>();
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
            if (multiplierSteps != null && multiplierSteps.Count > 0)
            {
                // 순서와 무관하게 조건을 만족하는 가장 높은 단계를 쓴다.
                int bestCombo = 0;
                float best = 1f;
                for (int i = 0; i < multiplierSteps.Count; i++)
                {
                    ComboMultiplierStep step = multiplierSteps[i];
                    if (combo < step.minCombo || step.minCombo < bestCombo) continue;
                    bestCombo = step.minCombo;
                    best = Mathf.Max(1f, step.multiplier);
                }
                return best;
            }

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
