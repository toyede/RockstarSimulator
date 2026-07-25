using System;
using UnityEngine;

namespace ContextStage
{
    public readonly struct AudienceEngagementRules
    {
        public AudienceEngagementRules(
            float maxEngagement,
            float initialEngagementMin,
            float initialEngagementMax,
            float calmUpperBound,
            float middleUpperBound,
            float naturalDecayPerSecond,
            float engagementPerReactionPoint)
        {
            MaxEngagement = maxEngagement;
            InitialEngagementMin = initialEngagementMin;
            InitialEngagementMax = initialEngagementMax;
            CalmUpperBound = calmUpperBound;
            MiddleUpperBound = middleUpperBound;
            NaturalDecayPerSecond = naturalDecayPerSecond;
            EngagementPerReactionPoint = engagementPerReactionPoint;
        }

        public float MaxEngagement { get; }
        public float InitialEngagementMin { get; }
        public float InitialEngagementMax { get; }
        public float CalmUpperBound { get; }
        public float MiddleUpperBound { get; }
        public float NaturalDecayPerSecond { get; }
        public float EngagementPerReactionPoint { get; }

        public bool TryValidate(out string error)
        {
            if (MaxEngagement <= 0f)
            {
                error = "Max engagement must be greater than zero.";
                return false;
            }

            if (InitialEngagementMin < 0f ||
                InitialEngagementMax < InitialEngagementMin ||
                InitialEngagementMax > MaxEngagement)
            {
                error = "Initial engagement range must stay within 0..max.";
                return false;
            }

            if (CalmUpperBound <= 0f ||
                MiddleUpperBound <= CalmUpperBound ||
                MiddleUpperBound >= MaxEngagement)
            {
                error = "Stage boundaries must satisfy 0 < calm < middle < max.";
                return false;
            }

            if (NaturalDecayPerSecond < 0f)
            {
                error = "Natural decay cannot be negative.";
                return false;
            }

            if (EngagementPerReactionPoint < 0f)
            {
                error = "Engagement per reaction point cannot be negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public float Clamp(float engagement) =>
            Mathf.Clamp(engagement, 0f, MaxEngagement);

        public AudienceEngagementStage ResolveStage(float engagement)
        {
            float clamped = Clamp(engagement);
            if (clamped <= CalmUpperBound) return AudienceEngagementStage.Calm;
            return clamped <= MiddleUpperBound
                ? AudienceEngagementStage.Middle
                : AudienceEngagementStage.Excited;
        }

        public float DrawInitialEngagement(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return Mathf.Lerp(
                InitialEngagementMin,
                InitialEngagementMax,
                (float)random.NextDouble());
        }
    }

    public readonly struct AudienceFlowRules
    {
        public AudienceFlowRules(
            int initialAudienceCount,
            int maximumAudienceCount,
            float chillWeight,
            float singalongWeight,
            float moshWeight,
            int randomSeed,
            float arrivalCheckInterval,
            float arrivalChance)
        {
            InitialAudienceCount = initialAudienceCount;
            MaximumAudienceCount = maximumAudienceCount;
            ChillWeight = chillWeight;
            SingalongWeight = singalongWeight;
            MoshWeight = moshWeight;
            RandomSeed = randomSeed;
            ArrivalCheckInterval = arrivalCheckInterval;
            ArrivalChance = arrivalChance;
        }

        public int InitialAudienceCount { get; }
        public int MaximumAudienceCount { get; }
        public float ChillWeight { get; }
        public float SingalongWeight { get; }
        public float MoshWeight { get; }
        public int RandomSeed { get; }
        public float ArrivalCheckInterval { get; }
        public float ArrivalChance { get; }

        public bool TryValidate(out string error)
        {
            if (InitialAudienceCount < 1 ||
                MaximumAudienceCount < InitialAudienceCount)
            {
                error = "Audience counts must satisfy 1 <= initial <= maximum.";
                return false;
            }

            if (ChillWeight < 0f || SingalongWeight < 0f || MoshWeight < 0f ||
                ChillWeight + SingalongWeight + MoshWeight <= 0f)
            {
                error = "Preference weights must be non-negative with a positive total.";
                return false;
            }

            if (ArrivalCheckInterval < 0f)
            {
                error = "Arrival check interval cannot be negative.";
                return false;
            }

            if (ArrivalChance < 0f || ArrivalChance > 1f)
            {
                error = "Arrival chance must stay within 0..1.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public CrowdPreference DrawPreference(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            double total = ChillWeight + SingalongWeight + MoshWeight;
            double roll = random.NextDouble() * total;
            if (roll < ChillWeight) return CrowdPreference.Chill;
            if (roll < ChillWeight + SingalongWeight) return CrowdPreference.Singalong;
            return CrowdPreference.Mosh;
        }
    }
}
