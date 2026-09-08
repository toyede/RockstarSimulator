using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public sealed class AugmentCardPreviewViewModel
    {
        public Sprite artwork;
        public string displayName = "";
        public string description = "";

        public AugmentCardPreviewViewModel Clone()
        {
            return new AugmentCardPreviewViewModel
            {
                artwork = artwork,
                displayName = displayName,
                description = description
            };
        }
    }

    /// <summary>
    /// 증강 시스템이 화면에 전달하는 순수 표시 데이터다.
    /// 후보의 생성, 중복 검사, 리롤 소비, 효과 적용 규칙은 포함하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class AugmentChoiceViewModel
    {
        public int slotIndex;
        public AugmentTier tier;
        public Sprite icon;
        public string tierLabel = "";
        public Color tierColor = Color.white;
        public string displayName = "";
        public string description = "";
        public AugmentCardPreviewViewModel grantedCard;
        public int rerollsRemaining;
        public bool canSelect = true;

        public AugmentChoiceViewModel Clone()
        {
            return new AugmentChoiceViewModel
            {
                slotIndex = slotIndex,
                tier = tier,
                icon = icon,
                tierLabel = tierLabel,
                tierColor = tierColor,
                displayName = displayName,
                description = description,
                grantedCard = grantedCard?.Clone(),
                rerollsRemaining = Mathf.Max(0, rerollsRemaining),
                canSelect = canSelect
            };
        }
    }

    [Serializable]
    public sealed class AugmentOwnedItemViewModel
    {
        public Sprite icon;
        public string displayName = "";
        public string description = "";

        public AugmentOwnedItemViewModel Clone()
        {
            return new AugmentOwnedItemViewModel
            {
                icon = icon,
                displayName = displayName,
                description = description
            };
        }
    }

    /// <summary>
    /// 증강 선택 팝업 전체를 한 번에 갱신하기 위한 표시 데이터다.
    /// 실제 증강 ID는 화면에 노출하지 않고, 선택과 리롤은 slotIndex로 요청한다.
    /// </summary>
    [Serializable]
    public sealed class AugmentSelectionScreenModel
    {
        public string title = "CHOOSE AN AUGMENT";
        public string subtitle = "Choose one Augment for the next performance.";
        public int ownedAugmentCount;
        public List<AugmentChoiceViewModel> choices = new List<AugmentChoiceViewModel>();
        public List<AugmentOwnedItemViewModel> ownedAugments =
            new List<AugmentOwnedItemViewModel>();
    }
}
