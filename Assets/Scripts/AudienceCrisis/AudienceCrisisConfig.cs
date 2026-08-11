using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AudienceCrisisConfig",
        menuName = "ContextStage/Audience/Crisis Config")]
    public sealed class AudienceCrisisConfig : ScriptableObject
    {
        [Header("Encounter")]
        [SerializeField, Range(0f, 1f)] float encounterChance = 0.5f;
        [SerializeField, Range(0f, 1f)] float earliestPerformanceRatio = 0.3f;
        [SerializeField, Range(0f, 1f)] float latestPerformanceRatio = 0.6f;
        [SerializeField] int randomSeed = 9187;

        [Header("Adaptive Director")]
        [SerializeField] bool useAdaptiveTrigger = true;
        [SerializeField, Range(0f, 1f)] float adaptiveEvaluationStartRatio = 0.25f;
        [SerializeField, Range(0f, 1f)] float adaptiveEvaluationEndRatio = 0.78f;
        [SerializeField, Min(1f)] float highPerformancePace = 1.25f;
        [SerializeField, Range(0.05f, 1f)] float lowPerformancePace = 0.65f;
        [SerializeField, Min(0f)] float highPerformanceEngagement = 70f;
        [SerializeField, Min(0.25f)] float adaptiveSustainDuration = 4f;
        [SerializeField, Min(0.1f)] float adaptiveCheckInterval = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("이 시점까지 성적 조건이 유지되지 않으면 현재 점수 페이스로 이벤트를 한 번 확정합니다.")]
        float adaptiveFallbackRatio = 0.55f;
        [SerializeField, Min(0f), Tooltip("확정 이벤트 시 이 페이스 이상이면 이탈 위기, 미만이면 관객 지원을 우선합니다.")]
        float fallbackCrisisPace = 0.95f;

        [Header("Comeback Event")]
        [SerializeField, Min(0.1f)] float comebackWarningDuration = 1.75f;
        [SerializeField, Min(1)] int comebackAudienceCount = 2;
        [SerializeField, Range(0f, 100f)] float comebackAudienceEngagement = 55f;
        [SerializeField, Min(0f)] float comebackEngagementBoost = 8f;

        [Header("Warning")]
        [SerializeField, Min(0.5f)] float warningDuration = 6f;
        [SerializeField, Min(1)] int minimumAudienceCount = 6;

        [Header("Departure")]
        [SerializeField, Range(0.05f, 1f)] float threatenedRatio = 0.33f;
        [SerializeField, Min(1)] int maximumThreatenedCount = 4;
        [SerializeField, Min(1)] int minimumSurvivorCount = 3;
        [SerializeField, Min(0f)] float retentionEngagement = 50f;
        [SerializeField, Min(0f)] float departureStagger = 0.08f;
        [SerializeField, Min(0f)] float departureSettleDuration = 0.55f;

        [Header("Special Response")]
        [SerializeField, Min(0)] int successfulSpecialSaveCount = 1;

        public float EncounterChance => encounterChance;
        public float EarliestPerformanceRatio => earliestPerformanceRatio;
        public float LatestPerformanceRatio => latestPerformanceRatio;
        public int RandomSeed => randomSeed;
        public bool UseAdaptiveTrigger => useAdaptiveTrigger;
        public float AdaptiveEvaluationStartRatio => adaptiveEvaluationStartRatio;
        public float AdaptiveEvaluationEndRatio => adaptiveEvaluationEndRatio;
        public float HighPerformancePace => highPerformancePace;
        public float LowPerformancePace => lowPerformancePace;
        public float HighPerformanceEngagement => highPerformanceEngagement;
        public float AdaptiveSustainDuration => adaptiveSustainDuration;
        public float AdaptiveCheckInterval => adaptiveCheckInterval;
        public float AdaptiveFallbackRatio => adaptiveFallbackRatio;
        public float FallbackCrisisPace => fallbackCrisisPace;
        public float ComebackWarningDuration => comebackWarningDuration;
        public int ComebackAudienceCount => comebackAudienceCount;
        public float ComebackAudienceEngagement => comebackAudienceEngagement;
        public float ComebackEngagementBoost => comebackEngagementBoost;
        public float WarningDuration => warningDuration;
        public int MinimumAudienceCount => minimumAudienceCount;
        public float ThreatenedRatio => threatenedRatio;
        public int MaximumThreatenedCount => maximumThreatenedCount;
        public int MinimumSurvivorCount => minimumSurvivorCount;
        public float RetentionEngagement => retentionEngagement;
        public float DepartureStagger => departureStagger;
        public float DepartureSettleDuration => departureSettleDuration;
        public int SuccessfulSpecialSaveCount => successfulSpecialSaveCount;

        public bool TryValidate(out string error)
        {
            if (latestPerformanceRatio < earliestPerformanceRatio)
                error = "Latest crisis time must follow earliest time.";
            else if (minimumAudienceCount <= minimumSurvivorCount)
                error = "Minimum audience must exceed minimum survivors.";
            else if (maximumThreatenedCount < 1 || warningDuration <= 0f)
                error = "Threat count and warning duration must be positive.";
            else
            {
                error = string.Empty;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            encounterChance = Mathf.Clamp01(encounterChance);
            earliestPerformanceRatio = Mathf.Clamp01(earliestPerformanceRatio);
            latestPerformanceRatio = Mathf.Clamp(
                latestPerformanceRatio,
                earliestPerformanceRatio,
                1f);
            adaptiveEvaluationStartRatio = Mathf.Clamp01(adaptiveEvaluationStartRatio);
            adaptiveEvaluationEndRatio = Mathf.Clamp(
                adaptiveEvaluationEndRatio,
                adaptiveEvaluationStartRatio,
                1f);
            highPerformancePace = Mathf.Max(1f, highPerformancePace);
            lowPerformancePace = Mathf.Clamp(lowPerformancePace, 0.05f, 1f);
            highPerformanceEngagement = Mathf.Max(0f, highPerformanceEngagement);
            adaptiveSustainDuration = Mathf.Max(0.25f, adaptiveSustainDuration);
            adaptiveCheckInterval = Mathf.Max(0.1f, adaptiveCheckInterval);
            adaptiveFallbackRatio = Mathf.Clamp(
                adaptiveFallbackRatio,
                adaptiveEvaluationStartRatio,
                adaptiveEvaluationEndRatio);
            fallbackCrisisPace = Mathf.Max(0f, fallbackCrisisPace);
            comebackWarningDuration = Mathf.Max(0.1f, comebackWarningDuration);
            comebackAudienceCount = Mathf.Max(1, comebackAudienceCount);
            comebackAudienceEngagement = Mathf.Clamp(comebackAudienceEngagement, 0f, 100f);
            comebackEngagementBoost = Mathf.Max(0f, comebackEngagementBoost);
            warningDuration = Mathf.Max(0.5f, warningDuration);
            minimumSurvivorCount = Mathf.Max(1, minimumSurvivorCount);
            minimumAudienceCount = Mathf.Max(
                minimumSurvivorCount + 1,
                minimumAudienceCount);
            threatenedRatio = Mathf.Clamp(threatenedRatio, 0.05f, 1f);
            maximumThreatenedCount = Mathf.Max(1, maximumThreatenedCount);
            retentionEngagement = Mathf.Max(0f, retentionEngagement);
            departureStagger = Mathf.Max(0f, departureStagger);
            departureSettleDuration = Mathf.Max(0f, departureSettleDuration);
            successfulSpecialSaveCount = Mathf.Max(
                0,
                successfulSpecialSaveCount);
        }
#endif
    }
}
