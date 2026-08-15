using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 한 공연의 불변 설정. 이번 런에서 바뀌는 상태는 보관하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDefinition", menuName = "ContextStage/Tour/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string stageId = "stage";
        [SerializeField] string displayName = "Stage";
        [SerializeField] RunNodeType nodeType = RunNodeType.Performance;
        [SerializeField] bool isBoss;

        [Header("Performance")]
        [SerializeField, Min(0)] int targetScore = 5000;
        [SerializeField, Min(1f)] float duration = 120f;

        [Header("Flow IDs")]
        [SerializeField] string preDialogueId = "";
        [SerializeField] string rewardTableId = "";

        [Header("Stage Content IDs")]
        [SerializeField] string audiencePresetId = "";
        [SerializeField] List<string> venueRuleIds = new List<string>();

        public string StageId => stageId;
        public string DisplayName => displayName;
        public RunNodeType NodeType => nodeType;
        public bool IsBoss => isBoss;
        public int TargetScore => Mathf.Max(0, targetScore);
        public float Duration => Mathf.Max(1f, duration);
        public string PreDialogueId => preDialogueId;
        public string RewardTableId => rewardTableId;
        public string AudiencePresetId => audiencePresetId;
        public IReadOnlyList<string> VenueRuleIds => venueRuleIds;

#if UNITY_EDITOR
        void OnValidate()
        {
            stageId = stageId == null ? "" : stageId.Trim();
            displayName = displayName == null ? "" : displayName.Trim();
            targetScore = Mathf.Max(0, targetScore);
            duration = Mathf.Max(1f, duration);
        }
#endif
    }
}
