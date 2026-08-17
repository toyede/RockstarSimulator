using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    public readonly struct AugmentRuntimeModifiers
    {
        public AugmentRuntimeModifiers(
            float feverDurationBonus,
            float performanceDurationBonus,
            int initialAudienceBonus)
        {
            FeverDurationBonus = Mathf.Max(0f, feverDurationBonus);
            PerformanceDurationBonus = Mathf.Max(0f, performanceDurationBonus);
            InitialAudienceBonus = Mathf.Max(0, initialAudienceBonus);
        }

        public float FeverDurationBonus { get; }
        public float PerformanceDurationBonus { get; }
        public int InitialAudienceBonus { get; }
    }

    /// <summary>
    /// 보유 결과를 매번 원본 프리팹 데이터에서 다시 계산한다.
    /// ScriptableObject와 프리팹 원본 수치는 런타임에 수정하지 않는다.
    /// </summary>
    public static class AugmentRuntime
    {
        public static AugmentRuntimeModifiers Current
        {
            get
            {
                if (!TourRunManager.HasInstance) return default;
                return Calculate(
                    TourRunManager.Instance.CurrentRun,
                    AugmentCatalog.LoadDefault());
            }
        }

        public static AugmentRuntimeModifiers Calculate(
            TourRunState run,
            AugmentCatalog catalog)
        {
            if (run == null || catalog == null || run.ownedAugments == null)
                return default;

            float feverDuration = 0f;
            float performanceDuration = 0f;
            int initialAudience = 0;
            var applied = new HashSet<AugmentKey>();

            for (int i = 0; i < run.ownedAugments.Count; i++)
            {
                OwnedAugmentState owned = run.ownedAugments[i];
                if (owned == null) continue;

                var key = new AugmentKey(owned.definitionId, owned.tier);
                if (!applied.Add(key) ||
                    !catalog.TryGetDefinition(owned.definitionId, out AugmentDefinition definition) ||
                    !definition.TryGetTierData(owned.tier, out AugmentTierData tierData))
                    continue;

                switch (definition.EffectType)
                {
                    case AugmentEffectType.FeverDurationSeconds:
                        feverDuration += tierData.Value;
                        break;
                    case AugmentEffectType.PerformanceDurationSeconds:
                        performanceDuration += tierData.Value;
                        break;
                    case AugmentEffectType.InitialAudienceCount:
                        initialAudience += Mathf.RoundToInt(tierData.Value);
                        break;
                }
            }

            return new AugmentRuntimeModifiers(
                feverDuration,
                performanceDuration,
                initialAudience);
        }
    }
}
