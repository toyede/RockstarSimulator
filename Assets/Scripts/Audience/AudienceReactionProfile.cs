using System;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public sealed class AudienceReactionProfile
    {
        [SerializeField] bool appliesToAudience = true;

        [Header("Preference Score")]
        [SerializeField] int chillScore = 1;
        [SerializeField] int singalongScore = 1;
        [SerializeField] int moshScore = 1;

        [Header("Engagement Stage Score")]
        [SerializeField] int calmScore = 1;
        [SerializeField] int middleScore = 2;
        [SerializeField] int excitedScore = 3;

        [SerializeField, Min(0f)] float engagementMultiplier = 1f;

        public bool AppliesToAudience => appliesToAudience;
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
            Value = Mathf.Max(0, preferenceScore + stageScore);
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

            return new AudienceReactionResult(
                audience.Id,
                profile.ScoreFor(audience.Preference),
                profile.ScoreFor(audience.Stage));
        }
    }
}
