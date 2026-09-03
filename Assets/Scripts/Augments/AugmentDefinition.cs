using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public sealed class AugmentEffectValue
    {
        [SerializeField] AugmentEffectType effectType;
        [SerializeField] float value;

        public AugmentEffectType EffectType => effectType;
        public float Value => value;
    }

    /// <summary>
    /// 기존 카드의 기능과 표현을 런타임에 덧씌운다.
    /// 카드 원본 프리팹은 바꾸지 않으므로, 추후 강화 일러스트를 이 데이터에만 연결할 수 있다.
    /// </summary>
    [Serializable]
    public sealed class CardUpgradeData
    {
        [Header("Gameplay")]
        [SerializeField] CardDefinition targetCard;
        [SerializeField, Min(0)] int extraCardsAfterUse;
        [SerializeField, Min(0), FormerlySerializedAs("audienceEngagementBonus")]
        int audienceReactionBonus;

        [Header("Optional Presentation")]
        [SerializeField] Sprite artwork;
        [SerializeField] string displayNameOverride = string.Empty;
        [SerializeField, TextArea(2, 4)] string descriptionOverride = string.Empty;
        [SerializeField] int presentationPriority;

        public CardDefinition TargetCard => targetCard;
        public int ExtraCardsAfterUse => Mathf.Max(0, extraCardsAfterUse);
        public int AudienceReactionBonus => Mathf.Max(0, audienceReactionBonus);
        public Sprite Artwork => artwork;
        public string DisplayNameOverride => displayNameOverride ?? string.Empty;
        public string DescriptionOverride => descriptionOverride ?? string.Empty;
        public int PresentationPriority => presentationPriority;
        public bool IsConfigured => targetCard != null;
    }

    [Serializable]
    public sealed class AugmentTierData
    {
        [SerializeField] AugmentTier tier;
        [SerializeField] bool enabled = true;
        [SerializeField] float value;
        [SerializeField, TextArea(2, 4)] string description = string.Empty;
        [SerializeField] CardDefinition grantedCard;
        [SerializeField] List<AugmentEffectValue> additionalEffects =
            new List<AugmentEffectValue>();
        [SerializeField] CardUpgradeData cardUpgrade = new CardUpgradeData();

        public AugmentTier Tier => tier;
        public bool Enabled => enabled;
        public float Value => value;
        public string Description => description ?? string.Empty;
        public CardDefinition GrantedCard => grantedCard;
        public IReadOnlyList<AugmentEffectValue> AdditionalEffects =>
            additionalEffects ?? (IReadOnlyList<AugmentEffectValue>)
                Array.Empty<AugmentEffectValue>();
        public CardUpgradeData CardUpgrade => cardUpgrade;
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
