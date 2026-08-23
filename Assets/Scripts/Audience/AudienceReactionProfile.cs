using System;
using UnityEngine;

namespace ContextStage
{
    public enum AudienceReactionMode
    {
        PreferenceAndEngagement,
        FixedAllAudience
    }

    [Serializable]
    public sealed class AudienceReactionProfile
    {
        [SerializeField] private bool appliesToAudience = true;

        [Header("계산 방식")]
        [SerializeField] private AudienceReactionMode reactionMode;
        [SerializeField] private int fixedReactionValue = 1;

        [Header("성향별 값")]
        [SerializeField] private int chillScore = 1;
        [SerializeField] private int singalongScore = 1;
        [SerializeField] private int moshScore = 1;

        [Header("몰입도별 값")]
        [SerializeField] private int calmScore = 1;
        [SerializeField] private int middleScore = 2;
        [SerializeField] private int excitedScore = 3;

        [Header("몰입도 적용")]
        [SerializeField, Min(0f)] private float engagementMultiplier = 1f;

        public AudienceReactionProfile()
        {
        }

        public AudienceReactionProfile(
            bool appliesToAudience,
            int chillScore,
            int singalongScore,
            int moshScore,
            int calmScore,
            int middleScore,
            int excitedScore,
            float engagementMultiplier)
        {
            this.appliesToAudience = appliesToAudience;
            this.chillScore = chillScore;
            this.singalongScore = singalongScore;
            this.moshScore = moshScore;
            this.calmScore = calmScore;
            this.middleScore = middleScore;
            this.excitedScore = excitedScore;
            this.engagementMultiplier = engagementMultiplier;
        }

        public bool AppliesToAudience => appliesToAudience;
        public AudienceReactionMode ReactionMode => reactionMode;
        public int FixedReactionValue => fixedReactionValue;
        public int ChillScore => chillScore;
        public int SingalongScore => singalongScore;
        public int MoshScore => moshScore;
        public int CalmScore => calmScore;
        public int MiddleScore => middleScore;
        public int ExcitedScore => excitedScore;
        public float EngagementMultiplier => Mathf.Max(0f, engagementMultiplier);

        public int ScoreFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillScore;
                case CrowdPreference.Singalong: return singalongScore;
                case CrowdPreference.Mosh: return moshScore;
                default: throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
            }
        }

        public int ScoreFor(AudienceEngagementStage stage)
        {
            switch (stage)
            {
                case AudienceEngagementStage.Calm: return calmScore;
                case AudienceEngagementStage.Middle: return middleScore;
                case AudienceEngagementStage.Excited: return excitedScore;
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        public bool TryValidate(out string error)
        {
            if (!appliesToAudience)
            {
                error = string.Empty;
                return true;
            }

            if (engagementMultiplier < 0f)
            {
                error = "Engagement multiplier cannot be negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public readonly struct AudienceReactionResult
    {
        public AudienceReactionResult(
            AudienceId audienceId,
            int preferenceScore,
            int stageScore)
        {
            AudienceId = audienceId;
            PreferenceScore = preferenceScore;
            StageScore = stageScore;
            Value = preferenceScore + stageScore;
        }

        public AudienceReactionResult(AudienceId audienceId, int fixedValue)
        {
            AudienceId = audienceId;
            PreferenceScore = 0;
            StageScore = 0;
            Value = fixedValue;
        }

        public AudienceId AudienceId { get; }
        public int PreferenceScore { get; }
        public int StageScore { get; }
        public int Value { get; }
    }

    public static class AudienceReactionResolver
    {
        public static AudienceReactionResult Resolve(
            AudienceReactionProfile profile,
            AudienceSnapshot audience)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (!profile.AppliesToAudience)
                return new AudienceReactionResult(audience.Id, 0, 0);

            if (profile.ReactionMode == AudienceReactionMode.FixedAllAudience)
                return new AudienceReactionResult(
                    audience.Id,
                    profile.FixedReactionValue);

            return new AudienceReactionResult(
                audience.Id,
                profile.ScoreFor(audience.Preference),
                profile.ScoreFor(audience.Stage));
        }
    }
}
