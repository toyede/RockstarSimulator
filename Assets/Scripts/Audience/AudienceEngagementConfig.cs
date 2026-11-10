using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AudienceEngagementConfig",
        menuName = "ContextStage/Audience/Engagement Config")]
    public sealed class AudienceEngagementConfig : ScriptableObject
    {
        [Header("Range")]
        [SerializeField, Min(0.01f)] float maxEngagement = 100f;
        [SerializeField, Min(0f)] float initialEngagementMin = 30f;
        [SerializeField, Min(0f)] float initialEngagementMax = 50f;

        [Header("Display Stages")]
        [SerializeField, Min(0.01f), Tooltip("하/상 경계. 이 값 이하면 하(Calm), 초과면 상(Excited)")]
        float calmUpperBound = 50f;
        [SerializeField, Min(0.01f), Tooltip("미사용 — 몰입도는 하/상 2단계만 쓴다")]
        float middleUpperBound = 66f;
        [SerializeField, Min(0.01f), Tooltip("이 값 이하로 떨어지면 관객 머리 위에 이탈 경고 바를 표시한다")]
        float warningUpperBound = 33f;

        [Header("Balance")]
        [SerializeField, Min(0f)] float naturalDecayPerSecond = 1f;
        [SerializeField, Min(0f)] float engagementPerReactionPoint = 1f;

        public float MaxEngagement => maxEngagement;
        public float InitialEngagementMin => initialEngagementMin;
        public float InitialEngagementMax => initialEngagementMax;
        public float CalmUpperBound => calmUpperBound;
        public float MiddleUpperBound => middleUpperBound;
        public float WarningUpperBound => warningUpperBound;
        public float NaturalDecayPerSecond => naturalDecayPerSecond;
        public float EngagementPerReactionPoint => engagementPerReactionPoint;

        public AudienceEngagementRules CreateRules() =>
            new AudienceEngagementRules(
                maxEngagement,
                initialEngagementMin,
                initialEngagementMax,
                calmUpperBound,
                middleUpperBound,
                naturalDecayPerSecond,
                engagementPerReactionPoint);

        public bool TryValidate(out string error) => CreateRules().TryValidate(out error);

#if UNITY_EDITOR
        void OnValidate()
        {
            maxEngagement = Mathf.Max(0.03f, maxEngagement);
            initialEngagementMin = Mathf.Clamp(initialEngagementMin, 0f, maxEngagement);
            initialEngagementMax = Mathf.Clamp(
                initialEngagementMax,
                initialEngagementMin,
                maxEngagement);

            calmUpperBound = Mathf.Clamp(
                calmUpperBound,
                0.01f,
                maxEngagement - 0.02f);
            middleUpperBound = Mathf.Clamp(
                middleUpperBound,
                calmUpperBound + 0.01f,
                maxEngagement - 0.01f);
            warningUpperBound = Mathf.Clamp(warningUpperBound, 0.01f, maxEngagement);
            naturalDecayPerSecond = Mathf.Max(0f, naturalDecayPerSecond);
            engagementPerReactionPoint = Mathf.Max(0f, engagementPerReactionPoint);
        }
#endif
    }
}
