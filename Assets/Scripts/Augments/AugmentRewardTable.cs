using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AugmentRewardTable",
        menuName = "Context Stage/Augments/Reward Table")]
    public sealed class AugmentRewardTable : ScriptableObject
    {
        const string ResourcesFolder = "Augments/RewardTables";

        [SerializeField] string tableId = "reward_basic";
        [SerializeField, Min(0f)] float bronzeWeight = 1f;
        [SerializeField, Min(0f)] float silverWeight = 1f;
        [SerializeField, Min(0f)] float goldWeight = 1f;
        [SerializeField, Min(0)] int rerollsPerSlot = 1;

        public string TableId => tableId;
        public int RerollsPerSlot => Mathf.Max(0, rerollsPerSlot);

        public static AugmentRewardTable Load(string rewardTableId)
        {
            if (string.IsNullOrWhiteSpace(rewardTableId)) return null;
            return Resources.Load<AugmentRewardTable>(
                $"{ResourcesFolder}/{rewardTableId.Trim()}");
        }

        public float GetTierWeight(AugmentTier tier)
        {
            switch (tier)
            {
                case AugmentTier.Bronze:
                    return Mathf.Max(0f, bronzeWeight);
                case AugmentTier.Silver:
                    return Mathf.Max(0f, silverWeight);
                case AugmentTier.Gold:
                    return Mathf.Max(0f, goldWeight);
                default:
                    return 0f;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            tableId = tableId == null ? string.Empty : tableId.Trim();
            bronzeWeight = Mathf.Max(0f, bronzeWeight);
            silverWeight = Mathf.Max(0f, silverWeight);
            goldWeight = Mathf.Max(0f, goldWeight);
            rerollsPerSlot = Mathf.Max(0, rerollsPerSlot);
        }
#endif
    }
}
