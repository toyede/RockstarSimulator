using System;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public sealed class AugmentBadgeArt
    {
        [SerializeField] private Sprite bronze;
        [SerializeField] private Sprite silver;
        [SerializeField] private Sprite gold;

        public AugmentBadgeArt() { }

        public AugmentBadgeArt(Sprite bronze, Sprite silver, Sprite gold)
        {
            this.bronze = bronze;
            this.silver = silver;
            this.gold = gold;
        }

        public Sprite Resolve(AugmentTier tier) => tier == AugmentTier.Gold
            ? gold : tier == AugmentTier.Silver ? silver : bronze;

        public void Apply(
            UnityEngine.UI.Image background,
            UnityEngine.UI.Image icon,
            AugmentTier tier,
            Sprite category)
        {
            Sprite badge = Resolve(tier);
            if (background != null && badge != null)
            {
                background.sprite = badge;
                background.color = Color.white;
                background.preserveAspect = true;
                background.raycastTarget = false;
            }

            if (icon == null) return;
            icon.sprite = category;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = category != null;

            if (background == null || badge == null || category == null) return;
            // 배경 40px, 심볼 36px의 원본 비율을 유지한다.
            float size = Mathf.Min(
                background.rectTransform.rect.width,
                background.rectTransform.rect.height) * 0.9f;
            icon.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, size);
            icon.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, size);
        }
    }
}
