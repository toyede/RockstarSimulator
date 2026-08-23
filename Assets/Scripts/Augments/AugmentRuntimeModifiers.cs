using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    public readonly struct AugmentRuntimeModifiers
    {
        public AugmentRuntimeModifiers(
            float feverDurationBonus,
            int initialAudienceBonus,
            float audienceArrivalIntervalReduction,
            int comboBreakPreventionCount,
            int minimumHandSizeBonus,
            bool revealAudiencePreferences,
            float periodicIdleDrawIntervalSeconds)
        {
            FeverDurationBonus = Mathf.Max(0f, feverDurationBonus);
            InitialAudienceBonus = Mathf.Max(0, initialAudienceBonus);
            AudienceArrivalIntervalReduction = Mathf.Max(
                0f,
                audienceArrivalIntervalReduction);
            ComboBreakPreventionCount = Mathf.Max(0, comboBreakPreventionCount);
            MinimumHandSizeBonus = Mathf.Max(0, minimumHandSizeBonus);
            RevealAudiencePreferences = revealAudiencePreferences;
            PeriodicIdleDrawIntervalSeconds = Mathf.Max(
                0f,
                periodicIdleDrawIntervalSeconds);
        }

        public float FeverDurationBonus { get; }
        public int InitialAudienceBonus { get; }
        public float AudienceArrivalIntervalReduction { get; }
        public int ComboBreakPreventionCount { get; }
        public int MinimumHandSizeBonus { get; }
        public bool RevealAudiencePreferences { get; }
        public float PeriodicIdleDrawIntervalSeconds { get; }
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
            int initialAudience = 0;
            float audienceArrivalIntervalReduction = 0f;
            int comboBreakPreventionCount = 0;
            int minimumHandSizeBonus = 0;
            bool revealAudiencePreferences = false;
            float periodicIdleDrawIntervalSeconds = 0f;
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
                    case AugmentEffectType.InitialAudienceCount:
                        initialAudience += Mathf.RoundToInt(tierData.Value);
                        break;
                    case AugmentEffectType.AudienceArrivalIntervalReductionSeconds:
                        audienceArrivalIntervalReduction += tierData.Value;
                        break;
                    case AugmentEffectType.ComboBreakPreventionCount:
                        comboBreakPreventionCount += Mathf.RoundToInt(tierData.Value);
                        break;
                    case AugmentEffectType.MinimumHandSizeIncrease:
                        minimumHandSizeBonus += Mathf.RoundToInt(tierData.Value);
                        break;
                    case AugmentEffectType.RevealAudiencePreferences:
                        revealAudiencePreferences |= tierData.Value > 0f;
                        break;
                    case AugmentEffectType.PeriodicIdleDrawSeconds:
                        if (tierData.Value > 0f &&
                            (periodicIdleDrawIntervalSeconds <= 0f ||
                             tierData.Value < periodicIdleDrawIntervalSeconds))
                        {
                            // OneTierPerRun 데이터가 잘못 중복돼도 가장 강한(짧은) 주기만 쓴다.
                            periodicIdleDrawIntervalSeconds = tierData.Value;
                        }
                        break;
                }
            }

            return new AugmentRuntimeModifiers(
                feverDuration,
                initialAudience,
                audienceArrivalIntervalReduction,
                comboBreakPreventionCount,
                minimumHandSizeBonus,
                revealAudiencePreferences,
                periodicIdleDrawIntervalSeconds);
        }
    }
}
