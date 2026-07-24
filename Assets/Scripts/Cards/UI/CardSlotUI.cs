using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>카드 한 장을 단색 네모와 텍스트로 표시하는 프로토타입 슬롯.</summary>
    public sealed class CardSlotUI : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Text numberText;
        [SerializeField] Text titleText;
        [SerializeField] Text deltaText;

        void Awake()
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform != null) rectTransform.sizeDelta = new Vector2(150f, 225f);

            var layout = GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = 150f;
                layout.preferredHeight = 225f;
            }

            ConfigureText(numberText, 16, 22);
            ConfigureText(titleText, 16, 22);
            ConfigureText(deltaText, 14, 20);
        }

        public void Configure(Image backgroundImage, Text numberLabel, Text titleLabel, Text deltaLabel)
        {
            background = backgroundImage;
            numberText = numberLabel;
            titleText = titleLabel;
            deltaText = deltaLabel;
        }

        public void Bind(CardData card, int handIndex)
        {
            if (card == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            if (background != null)
            {
                bool hasArtwork = card.Artwork != null;
                background.sprite = card.Artwork;
                background.color = hasArtwork ? Color.white : card.PrototypeColor;
                background.type = Image.Type.Simple;
                background.preserveAspect = hasArtwork;
            }
            if (numberText != null) numberText.text = (handIndex + 1).ToString();
            if (titleText != null) titleText.text = card.DisplayName;
            if (deltaText != null)
                deltaText.text = $"적정 {card.FavorableHypeMin:0}-{card.FavorableHypeMax:0}";
        }

        public void Hide() => gameObject.SetActive(false);

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
