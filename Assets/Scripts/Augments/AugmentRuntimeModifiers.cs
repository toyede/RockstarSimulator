using System;
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
            int minimumHandSizeDelta,
            int comboGainPerSuccessfulCard,
            float performanceDurationMultiplier,
            float performanceScoreMultiplier,
            float perfectClearScoreMultiplier,
            bool revealAudiencePreferences,
            float periodicIdleDrawIntervalSeconds,
            IReadOnlyList<CardUpgradeModifiers> cardUpgrades)
        {
            FeverDurationBonus = Mathf.Max(0f, feverDurationBonus);
            InitialAudienceBonus = Mathf.Max(0, initialAudienceBonus);
            AudienceArrivalIntervalReduction = Mathf.Max(
                0f,
                audienceArrivalIntervalReduction);
            ComboBreakPreventionCount = Mathf.Max(0, comboBreakPreventionCount);
            MinimumHandSizeDelta = minimumHandSizeDelta;
            ComboGainPerSuccessfulCard = Mathf.Max(1, comboGainPerSuccessfulCard);
            PerformanceDurationMultiplier = Mathf.Max(0.01f, performanceDurationMultiplier);
            PerformanceScoreMultiplier = Mathf.Max(0f, performanceScoreMultiplier);
            PerfectClearScoreMultiplier = Mathf.Max(1f, perfectClearScoreMultiplier);
            RevealAudiencePreferences = revealAudiencePreferences;
            PeriodicIdleDrawIntervalSeconds = Mathf.Max(
                0f,
                periodicIdleDrawIntervalSeconds);
            CardUpgrades = cardUpgrades ?? Array.Empty<CardUpgradeModifiers>();
        }

        public float FeverDurationBonus { get; }
        public int InitialAudienceBonus { get; }
        public float AudienceArrivalIntervalReduction { get; }
        public int ComboBreakPreventionCount { get; }
        public int MinimumHandSizeDelta { get; }
        public int ComboGainPerSuccessfulCard { get; }
        public float PerformanceDurationMultiplier { get; }
        public float PerformanceScoreMultiplier { get; }
        public float PerfectClearScoreMultiplier { get; }
        public bool RevealAudiencePreferences { get; }
        public float PeriodicIdleDrawIntervalSeconds { get; }
        public IReadOnlyList<CardUpgradeModifiers> CardUpgrades { get; }

        public CardUpgradeModifiers ResolveCardUpgrade(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId) || CardUpgrades == null)
                return default;

            int extraCards = 0;
            int audienceReactionBonus = 0;
            Sprite artwork = null;
            string displayName = string.Empty;
            string description = string.Empty;
            int presentationPriority = int.MinValue;
            bool found = false;

            for (int i = 0; i < CardUpgrades.Count; i++)
            {
                CardUpgradeModifiers upgrade = CardUpgrades[i];
                if (!string.Equals(upgrade.TargetCardId, cardId, StringComparison.Ordinal))
                    continue;

                found = true;
                extraCards += upgrade.ExtraCardsAfterUse;
                audienceReactionBonus += upgrade.AudienceReactionBonus;

                if (!upgrade.HasPresentationOverride ||
                    upgrade.PresentationPriority < presentationPriority)
                    continue;

                artwork = upgrade.Artwork;
                displayName = upgrade.DisplayNameOverride;
                description = upgrade.DescriptionOverride;
                presentationPriority = upgrade.PresentationPriority;
            }

            return found
                ? new CardUpgradeModifiers(
                    cardId,
                    extraCards,
                    audienceReactionBonus,
                    artwork,
                    displayName,
                    description,
                    presentationPriority)
                : default;
        }
    }

    public readonly struct CardUpgradeModifiers
    {
        public CardUpgradeModifiers(
            string targetCardId,
            int extraCardsAfterUse,
            int audienceReactionBonus,
            Sprite artwork,
            string displayNameOverride,
            string descriptionOverride,
            int presentationPriority)
        {
            TargetCardId = targetCardId ?? string.Empty;
            ExtraCardsAfterUse = Mathf.Max(0, extraCardsAfterUse);
            AudienceReactionBonus = Mathf.Max(0, audienceReactionBonus);
            Artwork = artwork;
            DisplayNameOverride = displayNameOverride ?? string.Empty;
            DescriptionOverride = descriptionOverride ?? string.Empty;
            PresentationPriority = presentationPriority;
        }

        public string TargetCardId { get; }
        public int ExtraCardsAfterUse { get; }
        public int AudienceReactionBonus { get; }
        public bool HasAudienceReactionBonus => AudienceReactionBonus > 0;
        public Sprite Artwork { get; }
        public string DisplayNameOverride { get; }
        public string DescriptionOverride { get; }
        public int PresentationPriority { get; }
        public bool HasPresentationOverride =>
            Artwork != null ||
            !string.IsNullOrWhiteSpace(DisplayNameOverride) ||
            !string.IsNullOrWhiteSpace(DescriptionOverride);
    }

    /// <summary>
    /// 보유 결과를 매번 원본 프리팹 데이터에서 다시 계산한다.
    /// ScriptableObject와 프리팹 원본 수치는 런타임에 수정하지 않는다.
    /// </summary>
    public static class AugmentRuntime
    {
        static readonly AugmentRuntimeModifiers DefaultModifiers =
            new AugmentRuntimeModifiers(
                0f,
                0,
                0f,
                0,
                0,
                1,
                1f,
                1f,
                1f,
                false,
                0f,
                Array.Empty<CardUpgradeModifiers>());

        public static AugmentRuntimeModifiers Current
        {
            get
            {
                if (!TourRunManager.HasInstance) return DefaultModifiers;
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
                return DefaultModifiers;

            float feverDuration = 0f;
            int initialAudience = 0;
            float audienceArrivalIntervalReduction = 0f;
            int comboBreakPreventionCount = 0;
            int minimumHandSizeDelta = 0;
            int comboGainPerSuccessfulCard = 1;
            float performanceDurationMultiplier = 1f;
            float performanceScoreMultiplier = 1f;
            float perfectClearScoreMultiplier = 1f;
            bool revealAudiencePreferences = false;
            float periodicIdleDrawIntervalSeconds = 0f;
            var cardUpgrades = new List<CardUpgradeModifiers>();
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

                ApplyEffect(
                    definition.EffectType,
                    tierData.Value,
                    tierData,
                    cardUpgrades,
                    ref feverDuration,
                    ref initialAudience,
                    ref audienceArrivalIntervalReduction,
                    ref comboBreakPreventionCount,
                    ref minimumHandSizeDelta,
                    ref comboGainPerSuccessfulCard,
                    ref performanceDurationMultiplier,
                    ref performanceScoreMultiplier,
                    ref perfectClearScoreMultiplier,
                    ref revealAudiencePreferences,
                    ref periodicIdleDrawIntervalSeconds);

                IReadOnlyList<AugmentEffectValue> additionalEffects =
                    tierData.AdditionalEffects;
                for (int effectIndex = 0;
                     effectIndex < additionalEffects.Count;
                     effectIndex++)
                {
                    AugmentEffectValue effect = additionalEffects[effectIndex];
                    if (effect == null) continue;

                    ApplyEffect(
                        effect.EffectType,
                        effect.Value,
                        tierData,
                        cardUpgrades,
                        ref feverDuration,
                        ref initialAudience,
                        ref audienceArrivalIntervalReduction,
                        ref comboBreakPreventionCount,
                        ref minimumHandSizeDelta,
                        ref comboGainPerSuccessfulCard,
                        ref performanceDurationMultiplier,
                        ref performanceScoreMultiplier,
                        ref perfectClearScoreMultiplier,
                        ref revealAudiencePreferences,
                        ref periodicIdleDrawIntervalSeconds);
                }
            }

            return new AugmentRuntimeModifiers(
                feverDuration,
                initialAudience,
                audienceArrivalIntervalReduction,
                comboBreakPreventionCount,
                minimumHandSizeDelta,
                comboGainPerSuccessfulCard,
                performanceDurationMultiplier,
                performanceScoreMultiplier,
                perfectClearScoreMultiplier,
                revealAudiencePreferences,
                periodicIdleDrawIntervalSeconds,
                cardUpgrades);
        }

        static void ApplyEffect(
            AugmentEffectType effectType,
            float value,
            AugmentTierData tierData,
            List<CardUpgradeModifiers> cardUpgrades,
            ref float feverDuration,
            ref int initialAudience,
            ref float audienceArrivalIntervalReduction,
            ref int comboBreakPreventionCount,
            ref int minimumHandSizeDelta,
            ref int comboGainPerSuccessfulCard,
            ref float performanceDurationMultiplier,
            ref float performanceScoreMultiplier,
            ref float perfectClearScoreMultiplier,
            ref bool revealAudiencePreferences,
            ref float periodicIdleDrawIntervalSeconds)
        {
            switch (effectType)
            {
                case AugmentEffectType.FeverDurationSeconds:
                    feverDuration += Mathf.Max(0f, value);
                    break;
                case AugmentEffectType.InitialAudienceCount:
                    initialAudience += Mathf.Max(0, Mathf.RoundToInt(value));
                    break;
                case AugmentEffectType.AudienceArrivalIntervalReductionSeconds:
                    audienceArrivalIntervalReduction += Mathf.Max(0f, value);
                    break;
                case AugmentEffectType.ComboBreakPreventionCount:
                    comboBreakPreventionCount += Mathf.Max(0, Mathf.RoundToInt(value));
                    break;
                case AugmentEffectType.MinimumHandSizeIncrease:
                case AugmentEffectType.MinimumHandSizeDelta:
                    minimumHandSizeDelta += Mathf.RoundToInt(value);
                    break;
                case AugmentEffectType.ComboGainPerSuccessfulCard:
                    comboGainPerSuccessfulCard = Mathf.Max(
                        comboGainPerSuccessfulCard,
                        Mathf.Max(1, Mathf.RoundToInt(value)));
                    break;
                case AugmentEffectType.PerformanceDurationMultiplier:
                    if (value > 0f) performanceDurationMultiplier *= value;
                    break;
                case AugmentEffectType.PerformanceScoreMultiplier:
                    if (value > 0f) performanceScoreMultiplier *= value;
                    break;
                case AugmentEffectType.PerfectClearScoreMultiplier:
                    if (value > 0f) perfectClearScoreMultiplier *= value;
                    break;
                case AugmentEffectType.CardUpgrade:
                    CardUpgradeData upgrade = tierData == null
                        ? null
                        : tierData.CardUpgrade;
                    if (upgrade != null && upgrade.IsConfigured)
                    {
                        cardUpgrades.Add(new CardUpgradeModifiers(
                            upgrade.TargetCard.Id,
                            upgrade.ExtraCardsAfterUse,
                            upgrade.AudienceReactionBonus,
                            upgrade.Artwork,
                            upgrade.DisplayNameOverride,
                            upgrade.DescriptionOverride,
                            upgrade.PresentationPriority));
                    }
                    break;
                case AugmentEffectType.RevealAudiencePreferences:
                    revealAudiencePreferences |= value > 0f;
                    break;
                case AugmentEffectType.PeriodicIdleDrawSeconds:
                    if (value > 0f &&
                        (periodicIdleDrawIntervalSeconds <= 0f ||
                         value < periodicIdleDrawIntervalSeconds))
                    {
                        // OneTierPerRun 데이터가 잘못 중복돼도 가장 강한(짧은) 주기만 쓴다.
                        periodicIdleDrawIntervalSeconds = value;
                    }
                    break;
            }
        }
    }
}
