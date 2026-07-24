using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>HandChanged 이벤트를 받아 화면 하단 카드 슬롯을 갱신한다.</summary>
    public sealed class CardHandUI : MonoBehaviour
    {
        [SerializeField] CardSlotUI[] slots;
        [SerializeField] Text hintText;

        public void Configure(CardSlotUI[] cardSlots, Text hintLabel)
        {
            slots = cardSlots;
            hintText = hintLabel;
        }

        void OnEnable()
        {
            EventBus.Subscribe<HandChanged>(OnHandChanged);
            Refresh();
        }

        void OnDisable() => EventBus.Unsubscribe<HandChanged>(OnHandChanged);

        void OnHandChanged(HandChanged _) => Refresh();

        void Refresh()
        {
            if (slots == null) return;

            if (!CardSystem.HasInstance)
            {
                HideAll();
                return;
            }

            var system = CardSystem.Instance;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                var card = system.GetCard(i);
                if (card == null)
                {
                    slots[i].Hide();
                    continue;
                }

                slots[i].Bind(card, i);
            }

            if (hintText != null)
            {
                hintText.text = system.HandCount > 0
                    ? $"숫자키 1~{system.HandCount}로 카드 선택"
                    : "호응도 100 달성 시 카드 추가";
            }
        }

        void HideAll()
        {
            for (int i = 0; i < slots.Length; i++) slots[i]?.Hide();
            if (hintText != null) hintText.text = "카드 시스템을 찾을 수 없음";
        }
    }
}
