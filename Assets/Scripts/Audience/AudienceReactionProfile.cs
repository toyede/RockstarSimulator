using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    public enum AudienceReactionMode
    {
        PreferenceAndEngagement,
        FixedAllAudience,
        // (성향 점수 + 몰입도 점수) x 표 거리 배율. 프리팹에 번호로 저장되므로 뒤에만 추가한다.
        GridDistance
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

        [Header("표 거리 (GridDistance 모드)")]
        [SerializeField, Tooltip("정답 칸의 몰입도. 정답 열은 카드의 Target Preference. 하=Calm, 상=Excited")]
        private AudienceEngagementStage targetEngagement = AudienceEngagementStage.Excited;

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
        public AudienceEngagementStage TargetEngagement => targetEngagement;
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

            if (reactionMode == AudienceReactionMode.GridDistance)
            {
                // 부호는 거리 배율이 정한다. 점수는 크기만 담으므로 음수를 막는다.
                if (chillScore < 0 || singalongScore < 0 || moshScore < 0 ||
                    calmScore < 0 || excitedScore < 0)
                {
                    error = "GridDistance scores must be zero or positive.";
                    return false;
                }

                if (targetEngagement == AudienceEngagementStage.Middle)
                {
                    error = "GridDistance target engagement must be Calm or Excited.";
                    return false;
                }
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

            if (profile.ReactionMode == AudienceReactionMode.GridDistance)
                throw new InvalidOperationException(
                    "GridDistance needs the card's target preference and distance multipliers.");

            return new AudienceReactionResult(
                audience.Id,
                profile.ScoreFor(audience.Preference),
                profile.ScoreFor(audience.Stage));
        }

        /// <summary>
        /// 카드 문맥(정답 열, 거리 배율표)이 필요한 계산. GridDistance 가 아니면 기본 계산과 같다.
        /// 반응 = (관객 성향 점수 + 관객 몰입도 점수) x 배율[정답 칸과의 거리].
        /// </summary>
        public static AudienceReactionResult Resolve(
            AudienceReactionProfile profile,
            AudienceSnapshot audience,
            CrowdPreference targetPreference,
            IReadOnlyList<float> distanceMultipliers)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (!profile.AppliesToAudience ||
                profile.ReactionMode != AudienceReactionMode.GridDistance)
                return Resolve(profile, audience);

            if (distanceMultipliers == null || distanceMultipliers.Count == 0)
                throw new ArgumentException(
                    "Distance multipliers are required for GridDistance.",
                    nameof(distanceMultipliers));

            int distance = GridDistance(
                targetPreference,
                profile.TargetEngagement,
                audience.Preference,
                audience.Stage);
            float multiplier =
                distanceMultipliers[Mathf.Min(distance, distanceMultipliers.Count - 1)];
            int magnitude =
                profile.ScoreFor(audience.Preference) + profile.ScoreFor(audience.Stage);
            return new AudienceReactionResult(
                audience.Id,
                Mathf.RoundToInt(magnitude * multiplier));
        }

        /// <summary>
        /// 성향 열(Chill-Singalong-Mosh)과 몰입도 행(상/하)으로 만든 표에서의 칸 거리 (0~3).
        /// </summary>
        public static int GridDistance(
            CrowdPreference cardPreference,
            AudienceEngagementStage cardEngagement,
            CrowdPreference audiencePreference,
            AudienceEngagementStage audienceStage)
        {
            int column = Mathf.Abs((int)cardPreference - (int)audiencePreference);
            int row = audienceStage == cardEngagement ? 0 : 1;
            return column + row;
        }
    }
}
