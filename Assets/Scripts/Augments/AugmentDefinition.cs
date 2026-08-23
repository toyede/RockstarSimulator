using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public sealed class AugmentTierData
    {
        [SerializeField] AugmentTier tier;
        [SerializeField] bool enabled = true;
        [SerializeField, Min(0f)] float value;
        [SerializeField, TextArea(2, 4)] string description = string.Empty;
        [SerializeField] CardDefinition grantedCard;

        public AugmentTier Tier => tier;
        public bool Enabled => enabled;
        public float Value => Mathf.Max(0f, value);
        public string Description => description ?? string.Empty;
        public CardDefinition GrantedCard => grantedCard;
    }

    /// <summary>
    /// 증강 한 종류의 표시 데이터와 티어별 수치를 함께 보관하는 프리팹 컴포넌트다.
    /// 런 상태에는 이 프리팹 대신 ID와 티어만 저장한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AugmentDefinition : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] string augmentId = "augment";
        [SerializeField] string displayName = "Augment";

        [Header("Presentation")]
        [SerializeField] Sprite icon;

        [Header("Effect")]
        [SerializeField] AugmentEffectType effectType;
        [SerializeField] AugmentTierOwnershipPolicy tierOwnershipPolicy;
        [SerializeField] List<AugmentTierData> tiers = new List<AugmentTierData>();

        public string AugmentId => augmentId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public AugmentEffectType EffectType => effectType;
        public AugmentTierOwnershipPolicy TierOwnershipPolicy => tierOwnershipPolicy;
        public IReadOnlyList<AugmentTierData> Tiers =>
            tiers ?? (IReadOnlyList<AugmentTierData>)Array.Empty<AugmentTierData>();

        public bool TryGetTierData(AugmentTier tier, out AugmentTierData tierData)
        {
            if (tiers != null)
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    AugmentTierData candidate = tiers[i];
                    if (candidate != null && candidate.Enabled && candidate.Tier == tier)
                    {
                        tierData = candidate;
                        return true;
                    }
                }
            }

            tierData = null;
            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            augmentId = augmentId == null ? string.Empty : augmentId.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
        }
#endif
    }
}
