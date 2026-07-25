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
