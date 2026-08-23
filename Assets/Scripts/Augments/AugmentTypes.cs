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
        FeverDurationSeconds,
        PerformanceDurationSeconds,
        InitialAudienceCount,
        AudienceArrivalIntervalReductionSeconds,
        ComboBreakPreventionCount,
        GrantCard,
        MinimumHandSizeIncrease,
        RevealAudiencePreferences,
        PeriodicIdleDrawSeconds
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
