using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AudienceFlowConfig",
        menuName = "ContextStage/Audience/Flow Config")]
    public sealed class AudienceFlowConfig : ScriptableObject
    {
        [Header("Roster")]
        [SerializeField, Min(1)] int initialAudienceCount = 3;
        [SerializeField, Min(1)] int maximumAudienceCount = 10;

        [Header("Preference Weights")]
        [SerializeField, Min(0f)] float chillWeight = 1f;
        [SerializeField, Min(0f)] float singalongWeight = 1f;
        [SerializeField, Min(0f)] float moshWeight = 1f;

        [Header("Determinism")]
        [SerializeField] int randomSeed = 4861;

        [Header("Natural Arrival")]
        [SerializeField, Min(0f), Tooltip("유입 판정 주기(초). 0이면 신규 유입을 끈다.")]
        float arrivalCheckInterval = 5f;
        [SerializeField, Range(0f, 1f), Tooltip("판정 1회당 신규 관객 유입 확률.")]
        float arrivalChance = 0.3f;

        public int InitialAudienceCount => initialAudienceCount;
        public int MaximumAudienceCount => maximumAudienceCount;
        public float ChillWeight => chillWeight;
        public float SingalongWeight => singalongWeight;
        public float MoshWeight => moshWeight;
        public int RandomSeed => randomSeed;
        public float ArrivalCheckInterval => arrivalCheckInterval;
        public float ArrivalChance => arrivalChance;

        public AudienceFlowRules CreateRules() =>
            new AudienceFlowRules(
                initialAudienceCount,
                maximumAudienceCount,
                chillWeight,
                singalongWeight,
                moshWeight,
                randomSeed,
                arrivalCheckInterval,
                arrivalChance);

        public bool TryValidate(out string error) => CreateRules().TryValidate(out error);

#if UNITY_EDITOR
        void OnValidate()
        {
            initialAudienceCount = Mathf.Max(1, initialAudienceCount);
            maximumAudienceCount = Mathf.Max(initialAudienceCount, maximumAudienceCount);
            chillWeight = Mathf.Max(0f, chillWeight);
            singalongWeight = Mathf.Max(0f, singalongWeight);
            moshWeight = Mathf.Max(0f, moshWeight);
            arrivalCheckInterval = Mathf.Max(0f, arrivalCheckInterval);
            arrivalChance = Mathf.Clamp01(arrivalChance);
        }
#endif
    }
}
