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
        RecruitPreferenceAndWeakenOthers
    }

    [Serializable]
    public sealed class SpecialCardTargetEffect
    {
        [SerializeField] SpecialCardTargetEffectType effectType;

        [Header("Random Audience Arrival")]
        [SerializeField, Min(0)] int minimumArrivalCount = 1;
        [SerializeField, Min(0)] int maximumArrivalCount = 3;

        [Header("Boost All Engagement")]
        [SerializeField, Min(0f)] float engagementIncrease = 15f;

        [Header("Recruit Preference And Weaken Others")]
        [SerializeField] CrowdPreference recruitedPreference = CrowdPreference.Mosh;
        [SerializeField, Range(0f, 1f)] float arrivalCapacityRatio = 0.2f;
        [SerializeField, Min(0f)] float otherPreferenceEngagement = 1f;

        public SpecialCardTargetEffectType EffectType => effectType;
        public int MinimumArrivalCount => Mathf.Max(0, minimumArrivalCount);
        public int MaximumArrivalCount => Mathf.Max(0, maximumArrivalCount);
        public float EngagementIncrease => Mathf.Max(0f, engagementIncrease);
        public CrowdPreference RecruitedPreference => recruitedPreference;
        public float ArrivalCapacityRatio => Mathf.Clamp01(arrivalCapacityRatio);
        public float OtherPreferenceEngagement =>
            Mathf.Max(0f, otherPreferenceEngagement);

        public bool TryValidate(out string error)
        {
            if (effectType == SpecialCardTargetEffectType.None)
            {
                error = "A targeted special-card effect is required.";
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

            error = string.Empty;
            return true;
        }
    }

    public readonly struct SpecialCardTargetEffectResult
    {
        public SpecialCardTargetEffectResult(int affectedCount, int joinedCount)
        {
            AffectedCount = affectedCount;
            JoinedCount = joinedCount;
        }

        public int AffectedCount { get; }
        public int JoinedCount { get; }
    }

    public static class SpecialCardTargetEffectExecutor
    {
        public static bool TryExecute(
            SpecialCardTargetEffect effect,
            AudienceRosterSystem roster,
            List<AudienceId> audienceBuffer,
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
            CaptureAudienceIds(
                roster,
                audienceBuffer,
                effect.RecruitedPreference);

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

            return new SpecialCardTargetEffectResult(
                affectedCount + joinedCount,
                joinedCount);
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
