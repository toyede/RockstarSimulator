using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>카드 프리팹의 외형을 CardDefinition 데이터로 갱신한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CardSlotUI : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image artwork;
        [SerializeField] Text titleText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text roleText;

        void Awake()
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform != null) rectTransform.sizeDelta = new Vector2(160f, 240f);

            var layout = GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = 160f;
                layout.preferredHeight = 240f;
            }

            ConfigureText(titleText, 18, 26);
            ConfigureText(descriptionText, 13, 18);
            ConfigureText(roleText, 12, 16);
        }

        public void Configure(
            Image backgroundImage,
            Image artworkImage,
            Text titleLabel,
            Text descriptionLabel,
            Text roleLabel)
        {
            background = backgroundImage;
            artwork = artworkImage;
            titleText = titleLabel;
            descriptionText = descriptionLabel;
            roleText = roleLabel;
        }

        public void Bind(CardDefinition card)
        {
            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            if (background != null) background.color = card.CardColor;
            if (artwork != null)
            {
                artwork.sprite = card.Artwork;
                artwork.enabled = card.Artwork != null;
                artwork.color = Color.white;
                artwork.preserveAspect = true;
            }
            if (titleText != null) titleText.text = card.DisplayName;
            if (descriptionText != null) descriptionText.text = card.Description;
            if (roleText != null) roleText.text = GetRoleLabel(card);
        }

        static string GetRoleLabel(CardDefinition card)
        {
            if (card.Role == CardRole.Utility)
                return card.UtilityEffect == UtilityCardEffect.Draw ? "UTILITY · DRAW" : "UTILITY · REROLL";

            string stage = card.TargetStage.ToString().ToUpperInvariant();
            return card.Role == CardRole.Special ? $"SPECIAL · {stage}" : stage;
        }

        static void ConfigureText(Text text, int minSize, int maxSize)
        {
            if (text == null) return;

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;

            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
