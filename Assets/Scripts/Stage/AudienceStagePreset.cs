using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 스테이지별 관객 데이터. (기획서 §10 AudienceStagePreset)
    ///
    /// 전역 AudienceFlowConfig / AudienceEngagementConfig 에셋의 값을 런타임에 바꾸지 않고,
    /// 이 프리셋으로 규칙 값을 만들어 AudienceRosterSystem.ConfigureForStage 에 넘긴다.
    /// 호응도 단계 경계·최대치·반응당 호응도는 전역 EngagementConfig 를 그대로 쓰고,
    /// 시작 호응도 범위와 자연 감소만 스테이지가 덮어쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudienceStagePreset", menuName = "ContextStage/Tour/Audience Stage Preset")]
    public sealed class AudienceStagePreset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("StageDefinition.audiencePresetId 와 같은 값")]
        string presetId = "audience_preset";

        [Header("Roster")]
        [SerializeField, Min(1)] int initialAudienceCount = 3;
        [SerializeField, Min(1)] int maximumAudienceCount = 8;

        [Header("Preference Weights (Chill : Singalong : Mosh)")]
        [SerializeField, Min(0f)] float chillWeight = 1f;
        [SerializeField, Min(0f)] float singalongWeight = 1f;
        [SerializeField, Min(0f)] float moshWeight = 1f;

        [Header("Engagement")]
        [SerializeField, Min(0f), Tooltip("시작 호응도 최소")] float initialEngagementMin = 45f;
        [SerializeField, Min(0f), Tooltip("시작 호응도 최대")] float initialEngagementMax = 60f;
        [SerializeField, Min(0f), Tooltip("초당 자연 감소")] float naturalDecayPerSecond = 0.7f;

        [Header("Natural Arrival")]
        [SerializeField, Min(0f), Tooltip("유입 판정 주기(초). 0이면 자연 유입 없음")]
        float arrivalCheckInterval = 6f;
        [SerializeField, Range(0f, 1f), Tooltip("판정 1회당 유입 확률")]
        float arrivalChance = 0.6f;

        [Header("Determinism")]
        [SerializeField] int randomSeed = 4861;

        public string PresetId => presetId;
        public int InitialAudienceCount => initialAudienceCount;
        public int MaximumAudienceCount => maximumAudienceCount;
        public float ArrivalCheckInterval => arrivalCheckInterval;
        public bool HasNaturalArrival => arrivalCheckInterval > 0f && arrivalChance > 0f;

        /// <summary>전역 EngagementConfig 의 경계값 위에 이 프리셋의 시작 호응도·감소를 얹는다.</summary>
        public AudienceEngagementRules CreateEngagementRules(AudienceEngagementConfig baseConfig)
        {
            float max = baseConfig != null ? baseConfig.MaxEngagement : 100f;
            float calm = baseConfig != null ? baseConfig.CalmUpperBound : 33f;
            float middle = baseConfig != null ? baseConfig.MiddleUpperBound : 66f;
            float perPoint = baseConfig != null ? baseConfig.EngagementPerReactionPoint : 1f;

            float min = Mathf.Clamp(initialEngagementMin, 0f, max);
            float maxInitial = Mathf.Clamp(Mathf.Max(initialEngagementMax, min), 0f, max);

            return new AudienceEngagementRules(
                max,
                min,
                maxInitial,
                calm,
                middle,
                Mathf.Max(0f, naturalDecayPerSecond),
                perPoint);
        }

        /// <summary>증강(사전 홍보·공연 홍보) 보정을 프리셋 위에 합산한 유입 규칙.</summary>
        public AudienceFlowRules CreateFlowRules(
            int additionalInitialAudience,
            float arrivalCheckIntervalReduction)
        {
            const float minimumEnabledInterval = 0.1f;

            int effectiveInitial = Mathf.Clamp(
                initialAudienceCount + Mathf.Max(0, additionalInitialAudience),
                1,
                maximumAudienceCount);
            float effectiveInterval = arrivalCheckInterval <= 0f
                ? 0f
                : Mathf.Max(
                    minimumEnabledInterval,
                    arrivalCheckInterval - Mathf.Max(0f, arrivalCheckIntervalReduction));

            return new AudienceFlowRules(
                effectiveInitial,
                maximumAudienceCount,
                chillWeight,
                singalongWeight,
                moshWeight,
                randomSeed,
                effectiveInterval,
                arrivalChance);
        }

        public bool TryValidate(AudienceEngagementConfig baseConfig, out string error)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                error = $"'{name}' 프리셋에 presetId 가 없습니다.";
                return false;
            }

            if (!CreateEngagementRules(baseConfig).TryValidate(out error)) return false;
            return CreateFlowRules(0, 0f).TryValidate(out error);
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorInitialize(
            string id,
            int initialCount,
            int maximumCount,
            float chill,
            float singalong,
            float mosh,
            float engagementMin,
            float engagementMax,
            float decayPerSecond,
            float checkInterval,
            float chance)
        {
            presetId = id;
            initialAudienceCount = initialCount;
            maximumAudienceCount = maximumCount;
            chillWeight = chill;
            singalongWeight = singalong;
            moshWeight = mosh;
            initialEngagementMin = engagementMin;
            initialEngagementMax = engagementMax;
            naturalDecayPerSecond = decayPerSecond;
            arrivalCheckInterval = checkInterval;
            arrivalChance = chance;
        }

        void OnValidate()
        {
            presetId = presetId == null ? string.Empty : presetId.Trim();
            initialAudienceCount = Mathf.Max(1, initialAudienceCount);
            maximumAudienceCount = Mathf.Max(initialAudienceCount, maximumAudienceCount);
            initialEngagementMax = Mathf.Max(initialEngagementMin, initialEngagementMax);
            arrivalCheckInterval = Mathf.Max(0f, arrivalCheckInterval);
        }
#endif
    }
}
