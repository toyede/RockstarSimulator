using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 최종 아트 프리팹이 준비되기 전 TourHub에서 증강 선택 View를 검증하기 위한 빌더다.
    /// 최종 프리팹도 AugmentSelectionPopup의 동일한 공개 계약을 사용하면 된다.
    /// </summary>
    internal static class PrototypeAugmentSelectionUIFactory
    {
        public static AugmentSelectionPopup Create(Transform parent)
        {
            var rootObject = new GameObject(
                "AugmentSelectionPopup",
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
            TourPrototypeUIFactory.CreateFullscreenImage(
                root,
                new Color32(0x0B, 0x08, 0x12, 0xDC));

            RectTransform panel = TourPrototypeUIFactory.CreatePanel(
                root,
                "AugmentPanel",
                new Vector2(1540f, 900f),
                Vector2.zero,
                new Color32(0x2E, 0x22, 0x2F, 0xFF));

            RectTransform topAccent = TourPrototypeUIFactory.CreatePanel(
                panel,
                "TopAccent",
                new Vector2(1540f, 12f),
                new Vector2(0f, 444f),
                new Color32(0xF9, 0xC2, 0x2B, 0xFF));
            topAccent.SetAsLastSibling();

            Text title = TourPrototypeUIFactory.CreateText(
                panel,
                "Title",
                52,
                TextAnchor.MiddleCenter,
                new Color32(0xF9, 0xC2, 0x2B, 0xFF));
            TourPrototypeUIFactory.SetRect(title.rectTransform, new Vector2(0f, 382f), new Vector2(1100f, 72f));

            Text subtitle = TourPrototypeUIFactory.CreateText(
                panel,
                "Subtitle",
                25,
                TextAnchor.MiddleCenter,
                new Color32(0xC7, 0xDC, 0xD0, 0xFF));
            TourPrototypeUIFactory.SetRect(subtitle.rectTransform, new Vector2(0f, 330f), new Vector2(1100f, 46f));

            Text ownedCount = TourPrototypeUIFactory.CreateText(
                panel,
                "OwnedAugmentCount",
                22,
                TextAnchor.MiddleLeft,
                new Color32(0x9B, 0xAB, 0xB2, 0xFF));
            TourPrototypeUIFactory.SetRect(ownedCount.rectTransform, new Vector2(-570f, 374f), new Vector2(320f, 46f));

            var choices = new AugmentChoiceView[AugmentSelectionPopup.VisibleSlotCount];
            for (int i = 0; i < choices.Length; i++)
            {
                float x = (i - 1) * 465f;
                choices[i] = CreateChoice(panel, i, new Vector2(x, -15f));
            }

            Text feedback = TourPrototypeUIFactory.CreateText(
                panel,
                "Feedback",
                21,
                TextAnchor.MiddleCenter,
                new Color32(0x9B, 0xAB, 0xB2, 0xFF));
            TourPrototypeUIFactory.SetRect(feedback.rectTransform, new Vector2(0f, -406f), new Vector2(1150f, 42f));

            AugmentSelectionPopup popup = rootObject.AddComponent<AugmentSelectionPopup>();
            popup.Configure(canvasGroup, title, subtitle, ownedCount, feedback, choices);
            popup.Hide();
            return popup;
        }

        static AugmentChoiceView CreateChoice(Transform parent, int index, Vector2 position)
        {
            RectTransform card = TourPrototypeUIFactory.CreatePanel(
                parent,
                $"AugmentChoice_{index + 1}",
                new Vector2(410f, 650f),
                position,
                new Color32(0x3E, 0x35, 0x46, 0xFF));

            Image background = card.GetComponent<Image>();
            RectTransform tierStripRect = TourPrototypeUIFactory.CreatePanel(
                card,
                "TierStrip",
                new Vector2(410f, 14f),
                new Vector2(0f, 318f),
                new Color32(0x0E, 0xAF, 0x9B, 0xFF));
            Image tierStrip = tierStripRect.GetComponent<Image>();

            Text tierText = TourPrototypeUIFactory.CreateText(
                card,
                "Tier",
                20,
                TextAnchor.MiddleCenter,
                Color.white);
            TourPrototypeUIFactory.SetRect(tierText.rectTransform, new Vector2(0f, 278f), new Vector2(340f, 36f));

            RectTransform iconBackRect = TourPrototypeUIFactory.CreatePanel(
                card,
                "IconBackground",
                new Vector2(142f, 142f),
                new Vector2(0f, 182f),
                new Color32(0x0E, 0xAF, 0x9B, 0x44));
            Image iconBackground = iconBackRect.GetComponent<Image>();

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(iconBackRect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(16f, 16f);
            iconRect.offsetMax = new Vector2(-16f, -16f);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;

            Text iconPlaceholder = TourPrototypeUIFactory.CreateText(
                iconBackRect,
                "IconPlaceholder",
                58,
                TextAnchor.MiddleCenter,
                Color.white);
            iconPlaceholder.rectTransform.anchorMin = Vector2.zero;
            iconPlaceholder.rectTransform.anchorMax = Vector2.one;
            iconPlaceholder.rectTransform.offsetMin = Vector2.zero;
            iconPlaceholder.rectTransform.offsetMax = Vector2.zero;

            Text nameText = TourPrototypeUIFactory.CreateText(
                card,
                "Name",
                31,
                TextAnchor.MiddleCenter,
                Color.white);
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            TourPrototypeUIFactory.SetRect(nameText.rectTransform, new Vector2(0f, 75f), new Vector2(350f, 70f));

            Text descriptionText = TourPrototypeUIFactory.CreateText(
                card,
                "Description",
                22,
                TextAnchor.UpperCenter,
                new Color32(0xC7, 0xDC, 0xD0, 0xFF));
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            TourPrototypeUIFactory.SetRect(descriptionText.rectTransform, new Vector2(0f, -42f), new Vector2(340f, 135f));

            Button selectButton = TourPrototypeUIFactory.CreateButton(
                card,
                "SELECT",
                null,
                new Color32(0x0B, 0x8A, 0x8F, 0xFF));
            selectButton.name = "SelectButton";
            TourPrototypeUIFactory.SetRect(selectButton.GetComponent<RectTransform>(), new Vector2(0f, -165f), new Vector2(320f, 62f));
            Text selectLabel = selectButton.GetComponentInChildren<Text>();

            Button rerollButton = TourPrototypeUIFactory.CreateButton(
                card,
                "REROLL  (1)",
                null,
                new Color32(0x69, 0x4F, 0x62, 0xFF));
            rerollButton.name = "RerollButton";
            TourPrototypeUIFactory.SetRect(rerollButton.GetComponent<RectTransform>(), new Vector2(0f, -244f), new Vector2(320f, 54f));
            Text rerollLabel = rerollButton.GetComponentInChildren<Text>();
            rerollLabel.fontSize = 21;

            AugmentChoiceView view = card.gameObject.AddComponent<AugmentChoiceView>();
            view.Configure(
                background,
                tierStrip,
                iconBackground,
                icon,
                iconPlaceholder,
                tierText,
                nameText,
                descriptionText,
                selectButton,
                selectLabel,
                rerollButton,
                rerollLabel);
            return view;
        }
    }
}
