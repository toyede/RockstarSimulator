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
        [SerializeField, Min(0.01f)] float calmUpperBound = 33f;
        [SerializeField, Min(0.01f)] float middleUpperBound = 66f;

        [Header("Balance")]
        [SerializeField, Min(0f)] float naturalDecayPerSecond = 1f;
        [SerializeField, Min(0f)] float engagementPerReactionPoint = 1f;

        public float MaxEngagement => maxEngagement;
        public float InitialEngagementMin => initialEngagementMin;
        public float InitialEngagementMax => initialEngagementMax;
        public float CalmUpperBound => calmUpperBound;
        public float MiddleUpperBound => middleUpperBound;
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
            naturalDecayPerSecond = Mathf.Max(0f, naturalDecayPerSecond);
            engagementPerReactionPoint = Mathf.Max(0f, engagementPerReactionPoint);
        }
#endif
    }
}
