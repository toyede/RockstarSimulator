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
            if (background != null) background.color = card.PrototypeColor;
            if (numberText != null) numberText.text = (handIndex + 1).ToString();
            if (titleText != null) titleText.text = card.DisplayName;
            if (deltaText != null)
                deltaText.text = $"적정 {card.FavorableHypeMin:0}-{card.FavorableHypeMax:0}";
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
