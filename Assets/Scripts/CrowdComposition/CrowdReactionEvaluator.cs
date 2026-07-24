using UnityEngine;

namespace ContextStage
{
    /// <summary>Pure, scene-independent preference evaluation.</summary>
    public static class CrowdReactionEvaluator
    {
        public const float GoodThreshold = 0.10f;
        const float RatioEpsilon = 0.0001f;

        public static CrowdReactionGrade Evaluate(
            CrowdCompositionSnapshot composition,
            CrowdPreference targetPreference)
        {
            if (composition.TotalCount <= 0)
                return CrowdReactionGrade.Good;

            float highest = Mathf.Max(
                composition.GetRatio(CrowdPreference.Chill),
                Mathf.Max(
                    composition.GetRatio(CrowdPreference.Singalong),
                    composition.GetRatio(CrowdPreference.Mosh)));

            float target = composition.GetRatio(targetPreference);
            float difference = highest - target;

            if (difference <= RatioEpsilon)
                return CrowdReactionGrade.Great;

            return difference <= GoodThreshold + RatioEpsilon
                ? CrowdReactionGrade.Good
                : CrowdReactionGrade.Weak;
        }
    }
}
