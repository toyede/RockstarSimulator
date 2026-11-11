using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    public enum SpecialCardTargetEffectType
    {
        None,
        RandomAudienceArrival,
        BoostAllEngagement,
        RecruitPreferenceAndWeakenOthers,
        // 아래부터 성향별 저격 효과. 프리팹에 번호로 저장되므로 뒤에만 추가한다.
        ReviveLowEngagement,   // Chill: 하 관객만, 몰입도가 낮을수록 점수·회복이 크다
        HeatBurst,             // Mosh: 상 관객만, 몰입도가 높을수록 점수가 크다
        Chorus                 // Singalong: 관객 수 단계별 점수 + 전원 몰입도 증가
    }

    /// <summary>떼창 점수 단계. 관객 수가 minAudience 이상이면 한 명당 pointsPerAudience.</summary>
    [Serializable]
    public sealed class ChorusTier
    {
        [Min(1)] public int minAudience = 1;
        [Min(0)] public int pointsPerAudience;

        public ChorusTier() { }

        public ChorusTier(int minAudience, int pointsPerAudience)
        {
            this.minAudience = minAudience;
            this.pointsPerAudience = pointsPerAudience;
        }
    }

    [Serializable]
    public sealed class SpecialCardTargetEffect
    {
        [SerializeField] SpecialCardTargetEffectType effectType;

        [Header("Target Hit Reward")]
        [SerializeField, Min(0)] int targetScoreBonus;

        [Header("성향별 저격 공통: 같은 성향 관객 입장")]
        [SerializeField, Min(0), Tooltip("저격 후 카드와 같은 성향 관객이 들어오는 최소 인원. 정원이 차면 들어올 수 있는 만큼만")]
        int sameArrivalMin = 1;
        [SerializeField, Min(0), Tooltip("저격 후 카드와 같은 성향 관객이 들어오는 최대 인원")]
        int sameArrivalMax = 2;

        [Header("되살리기 (Chill): 하 관객만, 낮을수록 크게")]
        [SerializeField, Min(0f), Tooltip("관객마다 (하/상 경계 − 몰입도) × 이 값을 점수로 준다")]
        float reviveScorePerPoint = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("관객마다 (하/상 경계 − 몰입도) × 이 값만큼 몰입도를 회복한다")]
        float reviveRecoveryRatio = 0.6f;

        [Header("열기 폭발 (Mosh): 상 관객만, 높을수록 크게")]
        [SerializeField, Min(0f), Tooltip("관객마다 (몰입도 − 하/상 경계) × 이 값을 점수로 준다. 몰입도는 바꾸지 않는다")]
        float heatScorePerPoint = 2f;

        [Header("떼창 (Singalong): 관객 수 단계별, 몰입도와 무관")]
        [SerializeField, Tooltip("관객 수가 minAudience 이상인 단계 중 가장 높은 단계의 한 명당 점수를 쓴다")]
        List<ChorusTier> chorusTiers = new List<ChorusTier>
        {
            new ChorusTier(1, 8),
            new ChorusTier(6, 12),
            new ChorusTier(10, 16),
        };
        [SerializeField, Min(1), Tooltip("점수에 세는 최대 인원 (보스 무대 점수 폭주 방지)")]
        int chorusMaxCountedAudience = 12;
        [SerializeField, Min(0f), Tooltip("관객 전원의 몰입도 증가량")]
        float chorusEngagementIncrease = 10f;

        [Header("Random Audience Arrival")]
        [SerializeField, Min(0)] int minimumArrivalCount = 1;
        [SerializeField, Min(0)] int maximumArrivalCount = 3;

        [Header("Boost All Engagement")]
        [SerializeField, Min(0f)] float engagementIncrease = 15f;

        [Header("Recruit Preference And Cap Audience Engagement")]
        [SerializeField] CrowdPreference recruitedPreference = CrowdPreference.Mosh;
        [SerializeField, Range(0f, 1f)] float arrivalCapacityRatio = 0.2f;
        [SerializeField, Min(0f), Tooltip(
            "After recruiting, audience members above this engagement are capped at this value. " +
            "Audience members already below the cap keep their current engagement.")]
        float otherPreferenceEngagement = 1f;

        public SpecialCardTargetEffectType EffectType => effectType;
        public int TargetScoreBonus => Mathf.Max(0, targetScoreBonus);
        public int MinimumArrivalCount => Mathf.Max(0, minimumArrivalCount);
        public int MaximumArrivalCount => Mathf.Max(0, maximumArrivalCount);
        public float EngagementIncrease => Mathf.Max(0f, engagementIncrease);
        public CrowdPreference RecruitedPreference => recruitedPreference;
        public float ArrivalCapacityRatio => Mathf.Clamp01(arrivalCapacityRatio);
        public float OtherPreferenceEngagement =>
            Mathf.Max(0f, otherPreferenceEngagement);
        public int SameArrivalMin => Mathf.Max(0, sameArrivalMin);
        public int SameArrivalMax => Mathf.Max(0, sameArrivalMax);
        public float ReviveScorePerPoint => Mathf.Max(0f, reviveScorePerPoint);
        public float ReviveRecoveryRatio => Mathf.Clamp01(reviveRecoveryRatio);
        public float HeatScorePerPoint => Mathf.Max(0f, heatScorePerPoint);
        public int ChorusMaxCountedAudience => Mathf.Max(1, chorusMaxCountedAudience);
        public float ChorusEngagementIncrease => Mathf.Max(0f, chorusEngagementIncrease);

        /// <summary>센 인원에 맞는 떼창 단계의 한 명당 점수. 맞는 단계가 없으면 0.</summary>
        public int ChorusPointsPerAudience(int countedAudience)
        {
            int bestMin = int.MinValue;
            int points = 0;
            if (chorusTiers == null) return 0;
            for (int i = 0; i < chorusTiers.Count; i++)
            {
                ChorusTier tier = chorusTiers[i];
                if (tier == null || countedAudience < tier.minAudience || tier.minAudience <= bestMin)
                    continue;
                bestMin = tier.minAudience;
                points = Mathf.Max(0, tier.pointsPerAudience);
            }
            return points;
        }

        bool UsesSameArrival =>
            effectType == SpecialCardTargetEffectType.ReviveLowEngagement ||
            effectType == SpecialCardTargetEffectType.HeatBurst ||
            effectType == SpecialCardTargetEffectType.Chorus;

        public bool TryValidate(out string error)
        {
            if (effectType == SpecialCardTargetEffectType.None)
            {
                error = "A targeted special-card effect is required.";
                return false;
            }

            if (targetScoreBonus < 0)
            {
                error = "Target score bonus cannot be negative.";
                return false;
            }

            if (minimumArrivalCount < 0 ||
                maximumArrivalCount < minimumArrivalCount)
            {
                error =
                    "Random arrival counts must satisfy 0 <= minimum <= maximum.";
                return false;
            }

            if (engagementIncrease < 0f)
            {
                error = "Engagement increase cannot be negative.";
                return false;
            }

            if (arrivalCapacityRatio < 0f || arrivalCapacityRatio > 1f)
            {
                error = "Arrival capacity ratio must be between 0 and 1.";
                return false;
            }

            if (otherPreferenceEngagement < 0f)
            {
                error = "Other-preference engagement cannot be negative.";
                return false;
            }

            if (UsesSameArrival &&
                (sameArrivalMin < 0 || sameArrivalMax < sameArrivalMin))
            {
                error = "Same-preference arrival counts must satisfy 0 <= minimum <= maximum.";
                return false;
            }

            if (effectType == SpecialCardTargetEffectType.Chorus &&
                (chorusTiers == null || chorusTiers.Count == 0))
            {
                error = "Chorus needs at least one tier.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public readonly struct SpecialCardTargetEffectResult
    {
        public SpecialCardTargetEffectResult(int affectedCount, int joinedCount, int score = 0)
        {
            AffectedCount = affectedCount;
            JoinedCount = joinedCount;
            Score = score;
        }

        public int AffectedCount { get; }
        public int JoinedCount { get; }
        /// <summary>효과가 계산한 점수. 카드 점수에 더해진다 (콤보 배율 적용 전).</summary>
        public int Score { get; }
    }

    public static class SpecialCardTargetEffectExecutor
    {
        /// <param name="cardPreference">저격한 카드의 성향. 같은 성향 관객 입장에 쓴다.</param>
        public static bool TryExecute(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            CrowdPreference cardPreference,
            out SpecialCardTargetEffectResult result,
            out string error)
        {
            result = default;
            if (effect == null)
            {
                error = "Target effect data is missing.";
                return false;
            }

            if (roster == null || !roster.IsConfigured)
            {
                error = "A configured audience roster is required.";
                return false;
            }

            if (audienceBuffer == null)
            {
                error = "An audience work buffer is required.";
                return false;
            }

            if (!effect.TryValidate(out error))
                return false;

            switch (effect.EffectType)
            {
                case SpecialCardTargetEffectType.RandomAudienceArrival:
                    result = ExecuteRandomArrival(effect, roster);
                    break;
                case SpecialCardTargetEffectType.BoostAllEngagement:
                    result = ExecuteEngagementBoost(
                        effect,
                        roster,
                        audienceBuffer);
                    break;
                case SpecialCardTargetEffectType.RecruitPreferenceAndWeakenOthers:
                    result = ExecuteRecruitAndWeaken(
                        effect,
                        roster,
                        audienceBuffer);
                    break;
                case SpecialCardTargetEffectType.ReviveLowEngagement:
                    result = ExecuteRevive(effect, roster, audienceBuffer, cardPreference);
                    break;
                case SpecialCardTargetEffectType.HeatBurst:
                    result = ExecuteHeatBurst(effect, roster, audienceBuffer, cardPreference);
                    break;
                case SpecialCardTargetEffectType.Chorus:
                    result = ExecuteChorus(effect, roster, audienceBuffer, cardPreference);
                    break;
                default:
                    error = $"Unsupported target effect: {effect.EffectType}.";
                    return false;
            }

            error = string.Empty;
            return true;
        }

        static SpecialCardTargetEffectResult ExecuteRandomArrival(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster)
        {
            int desiredCount = UnityEngine.Random.Range(
                effect.MinimumArrivalCount,
                effect.MaximumArrivalCount + 1);
            int joinedCount = 0;
            for (int i = 0; i < desiredCount; i++)
            {
                if (!roster.TryAddRandom(
                        AudienceJoinReason.SpecialCardTarget,
                        out _))
                    break;
                joinedCount++;
            }

            return new SpecialCardTargetEffectResult(joinedCount, joinedCount);
        }

        static SpecialCardTargetEffectResult ExecuteEngagementBoost(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer)
        {
            CaptureAudienceIds(roster, audienceBuffer);
            int affectedCount = 0;
            for (int i = 0; i < audienceBuffer.Count; i++)
            {
                if (roster.TryChangeEngagement(
                        audienceBuffer[i],
                        effect.EngagementIncrease,
                        AudienceChangeReason.SpecialCardTarget,
                        out _))
                    affectedCount++;
            }

            return new SpecialCardTargetEffectResult(affectedCount, 0);
        }

        static SpecialCardTargetEffectResult ExecuteRecruitAndWeaken(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer)
        {
            int desiredCount = Mathf.CeilToInt(
                roster.Capacity * effect.ArrivalCapacityRatio);
            int joinedCount = 0;
            for (int i = 0; i < desiredCount; i++)
            {
                if (!roster.TryAdd(
                        effect.RecruitedPreference,
                        AudienceJoinReason.SpecialCardTarget,
                        out _))
                    break;
                joinedCount++;
            }

            CaptureAudienceIdsAboveEngagement(
                roster,
                audienceBuffer,
                effect.OtherPreferenceEngagement);

            int affectedCount = 0;
            for (int i = 0; i < audienceBuffer.Count; i++)
            {
                if (roster.TrySetEngagement(
                        audienceBuffer[i],
                        effect.OtherPreferenceEngagement,
                        AudienceChangeReason.SpecialCardTarget,
                        out _))
                    affectedCount++;
            }

            return new SpecialCardTargetEffectResult(
                affectedCount + joinedCount,
                joinedCount);
        }

        // ---------------- 성향별 저격 효과 ----------------
        // 점수·몰입도는 저격하는 순간 무대에 있던 관객만으로 계산하고, 같은 성향 관객은 그 뒤에 들어온다.

        /// <summary>Chill: 하 관객마다 (경계 − 몰입도)만큼 점수와 회복을 준다. 상 관객은 그대로.</summary>
        static SpecialCardTargetEffectResult ExecuteRevive(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            CrowdPreference cardPreference)
        {
            float boundary = LowHighBoundary(roster);
            CaptureAudienceIds(roster, audienceBuffer);

            float score = 0f;
            int affectedCount = 0;
            for (int i = 0; i < audienceBuffer.Count; i++)
            {
                if (!roster.TryGet(audienceBuffer[i], out AudienceSnapshot audience) ||
                    audience.Engagement > boundary)
                    continue;

                float gap = boundary - audience.Engagement;
                score += gap * effect.ReviveScorePerPoint;
                affectedCount++;

                float recovery = gap * effect.ReviveRecoveryRatio;
                if (recovery > 0f)
                    roster.TryChangeEngagement(
                        audience.Id,
                        recovery,
                        AudienceChangeReason.SpecialCardTarget,
                        out _);
            }

            int joinedCount = AddSamePreference(effect, roster, cardPreference);
            return new SpecialCardTargetEffectResult(
                affectedCount,
                joinedCount,
                Mathf.RoundToInt(score));
        }

        /// <summary>Mosh: 상 관객마다 (몰입도 − 경계)만큼 점수를 준다. 몰입도는 바꾸지 않는다.</summary>
        static SpecialCardTargetEffectResult ExecuteHeatBurst(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            CrowdPreference cardPreference)
        {
            float boundary = LowHighBoundary(roster);
            CaptureAudienceIds(roster, audienceBuffer);

            float score = 0f;
            int affectedCount = 0;
            for (int i = 0; i < audienceBuffer.Count; i++)
            {
                if (!roster.TryGet(audienceBuffer[i], out AudienceSnapshot audience) ||
                    audience.Engagement <= boundary)
                    continue;

                score += (audience.Engagement - boundary) * effect.HeatScorePerPoint;
                affectedCount++;
            }

            int joinedCount = AddSamePreference(effect, roster, cardPreference);
            return new SpecialCardTargetEffectResult(
                affectedCount,
                joinedCount,
                Mathf.RoundToInt(score));
        }

        /// <summary>Singalong: (센 인원 × 단계별 한 명당 점수), 관객 전원 몰입도 증가.</summary>
        static SpecialCardTargetEffectResult ExecuteChorus(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            CrowdPreference cardPreference)
        {
            CaptureAudienceIds(roster, audienceBuffer);
            int counted = Mathf.Min(audienceBuffer.Count, effect.ChorusMaxCountedAudience);
            int score = counted * effect.ChorusPointsPerAudience(counted);

            if (effect.ChorusEngagementIncrease > 0f)
            {
                for (int i = 0; i < audienceBuffer.Count; i++)
                {
                    roster.TryChangeEngagement(
                        audienceBuffer[i],
                        effect.ChorusEngagementIncrease,
                        AudienceChangeReason.SpecialCardTarget,
                        out _);
                }
            }

            int joinedCount = AddSamePreference(effect, roster, cardPreference);
            return new SpecialCardTargetEffectResult(counted, joinedCount, score);
        }

        /// <summary>카드와 같은 성향 관객을 최소~최대 인원 중 무작위로 입장시킨다. 정원이 차면 멈춘다.</summary>
        static int AddSamePreference(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            CrowdPreference preference)
        {
            int desiredCount = UnityEngine.Random.Range(
                effect.SameArrivalMin,
                effect.SameArrivalMax + 1);
            int joinedCount = 0;
            for (int i = 0; i < desiredCount; i++)
            {
                if (!roster.TryAdd(preference, AudienceJoinReason.SpecialCardTarget, out _))
                    break;
                joinedCount++;
            }
            return joinedCount;
        }

        /// <summary>몰입도 하/상 경계. 이 값 이하는 하, 초과는 상이다.</summary>
        static float LowHighBoundary(AudienceRosterSystem roster) =>
            roster.EngagementConfig != null ? roster.EngagementConfig.CalmUpperBound : 50f;

        static void CaptureAudienceIdsAboveEngagement(
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            float engagementCap)
        {
            audienceBuffer.Clear();
            IReadOnlyList<AudienceSnapshot> members = roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Engagement <= engagementCap)
                    continue;
                audienceBuffer.Add(members[i].Id);
            }
        }

        static void CaptureAudienceIds(
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
            CrowdPreference? excludedPreference = null)
        {
            audienceBuffer.Clear();
            IReadOnlyList<AudienceSnapshot> members = roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (excludedPreference.HasValue &&
                    members[i].Preference == excludedPreference.Value)
                    continue;
                audienceBuffer.Add(members[i].Id);
            }
        }
    }
}
