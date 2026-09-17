using System;

namespace ContextStage
{
    public enum AugmentTier
    {
        Bronze,
        Silver,
        Gold
    }

    public enum AugmentEffectType
    {
        // 프리팹에 저장되는 번호이므로 기존 값을 변경하지 않는다.
        FeverDurationSeconds = 0,
        PerformanceDurationSeconds = 1,
        InitialAudienceCount = 2,
        AudienceArrivalIntervalReductionSeconds = 3,
        ComboBreakPreventionCount = 4,
        GrantCard = 5,
        MinimumHandSizeIncrease = 6,
        MinimumHandSizeDelta = 7,
        ComboGainPerSuccessfulCard = 8,
        PerformanceDurationMultiplier = 9,
        PerformanceScoreMultiplier = 10,
        PerfectClearScoreMultiplier = 11,
        CardUpgrade = 12,
        RevealAudiencePreferences = 13,
        PeriodicIdleDrawSeconds = 14
    }

    public enum AugmentTierOwnershipPolicy
    {
        IndependentTiers,
        OneTierPerRun
    }

    [Serializable]
    public sealed class OwnedAugmentState
    {
        public string definitionId;
        public AugmentTier tier;

        public OwnedAugmentState() { }

        public OwnedAugmentState(string definitionId, AugmentTier tier)
        {
            this.definitionId = definitionId == null ? string.Empty : definitionId.Trim();
            this.tier = tier;
        }

        public bool Matches(string otherDefinitionId, AugmentTier otherTier)
        {
            return tier == otherTier &&
                   string.Equals(definitionId, otherDefinitionId, StringComparison.Ordinal);
        }
    }

    public readonly struct AugmentKey : IEquatable<AugmentKey>
    {
        public AugmentKey(string definitionId, AugmentTier tier)
        {
            DefinitionId = definitionId == null ? string.Empty : definitionId.Trim();
            Tier = tier;
        }

        public string DefinitionId { get; }
        public AugmentTier Tier { get; }

        public bool Equals(AugmentKey other)
        {
            return Tier == other.Tier &&
                   string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is AugmentKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((DefinitionId != null
                    ? StringComparer.Ordinal.GetHashCode(DefinitionId)
                    : 0) * 397) ^ (int)Tier;
            }
        }

        public override string ToString() => $"{DefinitionId}:{Tier}";
    }
}
