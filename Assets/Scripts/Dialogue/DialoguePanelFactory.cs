using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 대화창 계층을 코드로 만든다. (피그마 "다이얼로그" 프레임 레이아웃)
    ///
    /// - 런타임: Resources/Dialogue/DialoguePanel 프리팹이 없을 때의 폴백
    /// - 에디터: Tools/Dialogue/Create Dialogue Panel Prefab 이 같은 계층을 프리팹으로 저장한다
    ///
    /// 계층 (앞→뒤 그리기 순서):
    ///   BlurBackdrop(이전 화면 블러) → Backdrop(스테이지 그림, 블러 끌 때) → Dim → AdvanceButton(전체 클릭)
    ///   → Portraits → Venue → DialogueBox(상자 아트 · 이름 · 본문 · 화살표) → RuleCard → Skip(F 키 + 스킵하기)
    /// 크기·위치·색은 DialogueStyle 이 ApplyStyle 로 덮어쓴다.
    /// </summary>
    public static class DialoguePanelFactory
    {
        public const string PrefabResourcesPath = "Dialogue/DialoguePanel";
        public const int CanvasSortingOrder = 300;
        public const string BlurShaderName = "ContextStage/UI/Kawase Blur";

        public static DialoguePanel Create(Transform parent, DialogueStyle style)
        {
            var canvasObject = new GameObject(
                "DialogueCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            if (parent != null) canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasObject.transform;
            TMP_FontAsset font = style != null ? style.Font : null;
            var parts = new DialoguePanel.Parts();

            // 0) 이전 화면 블러 (일시정지 메뉴와 같은 UIBlurBackdrop)
            var blurObject = new GameObject("BlurBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform blurRect = blurObject.GetComponent<RectTransform>();
            blurRect.SetParent(root, false);
            StretchAll(blurRect, Vector2.zero, Vector2.zero);
            RawImage blurImage = blurObject.GetComponent<RawImage>();
            blurImage.color = style != null ? style.BlurTint : new Color(0.5f, 0.5f, 0.58f, 1f);
            blurImage.raycastTarget = false;
            parts.blurBackdrop = blurObject.AddComponent<UIBlurBackdrop>();
            parts.blurBackdrop.EditorAssignShader(Shader.Find(BlurShaderName));

            // 1) 스테이지 그림 배경 + 딤 (블러를 끌 때 쓴다)
            parts.backdrop = CreateStretchedImage(root, "Backdrop", new Color(0.4f, 0.4f, 0.45f, 1f));
            parts.backdrop.preserveAspect = false;
            parts.backdrop.raycastTarget = false;
            parts.dim = CreateStretchedImage(root, "Dim", new Color(0f, 0f, 0f, 0.35f));
            parts.dim.raycastTarget = false;

            // 2) 화면 전체 진행 버튼 (다른 버튼보다 앞 순서 = 뒤에 깔림)
            Image advanceImage = CreateStretchedImage(root, "AdvanceButton", new Color(0f, 0f, 0f, 0f));
            advanceImage.raycastTarget = true;
            parts.advanceButton = advanceImage.gameObject.AddComponent<Button>();
            parts.advanceButton.transition = Selectable.Transition.None;

            // 3) 초상화 (좌/우 하단, 아트가 오면 DialogueLine.portrait 로 뜬다)
            parts.portraitLeft = CreatePortrait(root, "PortraitLeft", new Vector2(0f, 0f), new Vector2(340f, 470f));
            parts.portraitRight = CreatePortrait(root, "PortraitRight", new Vector2(1f, 0f), new Vector2(-340f, 470f));

            // 4) 공연장 표시 (좌상단, 기본 숨김)
            RectTransform venue = CreateRect(root, "Venue",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -36f), new Vector2(700f, 72f));
            parts.venueRoot = venue.gameObject;
            var venueIconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform venueIconRect = venueIconObject.GetComponent<RectTransform>();
            venueIconRect.SetParent(venue, false);
            SetRect(venueIconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));
            parts.venueIcon = venueIconObject.GetComponent<Image>();
            parts.venueIcon.preserveAspect = true;
            parts.venueIcon.raycastTarget = false;
            parts.venueText = CreateText(venue, "VenueText", font, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(parts.venueText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                new Vector2(78f, 0f), new Vector2(-78f, 0f));

            // 5) 대사 상자 (하단 중앙) — 상자 아트(1_dialogue)를 통째로 늘려 쓴다
            RectTransform box = CreateRect(root, "DialogueBox",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(1520f, 300f));
            parts.boxRoot = box.gameObject;
            parts.boxBackground = box.gameObject.AddComponent<Image>();
            parts.boxBackground.color = Color.white;
            parts.boxBackground.raycastTarget = false;
            parts.boxOutline = box.gameObject.AddComponent<Outline>();
            parts.boxOutline.effectDistance = new Vector2(4f, -4f);
            parts.boxOutline.effectColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
            parts.boxOutline.useGraphicAlpha = false;
            parts.boxOutline.enabled = false; // 상자 아트가 있으면 테두리는 그림에 포함돼 있다

            RectTransform nameTag = CreateRect(box, "NameTag",
                new Vector2(0.10f, 0.66f), new Vector2(0.50f, 0.95f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            parts.nameTag = nameTag.gameObject.AddComponent<Image>();
            parts.nameTag.color = new Color32(0x0B, 0x8A, 0x8F, 0xFF);
            parts.nameTag.raycastTarget = false;
            parts.nameTag.enabled = false; // 시안의 그라데이션 띠는 상자 아트에 포함
            parts.nameText = CreateText(nameTag, "NameText", font, 30f, TextAlignmentOptions.Center, Color.white);
            StretchAll(parts.nameText.rectTransform, Vector2.zero, Vector2.zero);

            parts.bodyText = CreateText(box, "BodyText", font, 32f, TextAlignmentOptions.Center, Color.white);
            SetRect(parts.bodyText.rectTransform, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.62f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            parts.bodyText.textWrappingMode = TextWrappingModes.Normal;
            parts.bodyText.overflowMode = TextOverflowModes.Overflow;
            parts.bodyText.lineSpacing = 12f;

            var arrowObject = new GameObject("NextArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.SetParent(box, false);
            SetRect(arrowRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-34f, 28f), new Vector2(20f, 20f));
            parts.arrow = arrowObject.GetComponent<Image>();
            parts.arrow.raycastTarget = false;
            parts.arrow.color = Color.white;

            // 6) 룰 카드 (중앙)
            RectTransform ruleCard = CreateRect(root, "RuleCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(980f, 500f));
            parts.ruleCardRoot = ruleCard.gameObject;
            parts.ruleCardBackground = ruleCard.gameObject.AddComponent<Image>();
            parts.ruleCardBackground.color = new Color32(0x10, 0x0E, 0x14, 0xF6);
            parts.ruleCardBackground.raycastTarget = true; // 카드 뒤 진행 버튼으로 클릭이 새지 않게
            parts.ruleCardOutline = ruleCard.gameObject.AddComponent<Outline>();
            parts.ruleCardOutline.effectDistance = new Vector2(4f, -4f);
            parts.ruleCardOutline.effectColor = Color.white;
            parts.ruleCardOutline.useGraphicAlpha = false;

            var ruleIconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform ruleIconRect = ruleIconObject.GetComponent<RectTransform>();
            ruleIconRect.SetParent(ruleCard, false);
            SetRect(ruleIconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(128f, 128f));
            parts.ruleIcon = ruleIconObject.GetComponent<Image>();
            parts.ruleIcon.preserveAspect = true;
            parts.ruleIcon.raycastTarget = false;

            parts.ruleTitle = CreateText(ruleCard, "Title", font, 40f, TextAlignmentOptions.Center, Color.white);
            SetRect(parts.ruleTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(-80f, 60f));

            parts.ruleBody = CreateText(ruleCard, "Body", font, 28f, TextAlignmentOptions.Center, Color.white);
            SetRect(parts.ruleBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -250f), new Vector2(-120f, 150f));
            parts.ruleBody.textWrappingMode = TextWrappingModes.Normal;
            parts.ruleBody.lineSpacing = 10f;

            RectTransform confirm = CreateRect(ruleCard, "ConfirmButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(360f, 66f));
            Image confirmImage = confirm.gameObject.AddComponent<Image>();
            confirmImage.color = new Color32(0x0B, 0x8A, 0x8F, 0xFF);
            parts.ruleConfirmButton = confirm.gameObject.AddComponent<Button>();
            parts.ruleConfirmLabel = CreateText(confirm, "Label", font, 28f, TextAlignmentOptions.Center, Color.white);
            StretchAll(parts.ruleConfirmLabel.rectTransform, Vector2.zero, Vector2.zero);

            // 7) 스킵 (우하단: [F] 스킵하기, 가장 앞)
            RectTransform skip = CreateRect(root, "SkipButton",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 24f), new Vector2(260f, 60f));
            Image skipImage = skip.gameObject.AddComponent<Image>();
            skipImage.color = new Color(0f, 0f, 0f, 0f);
            skipImage.raycastTarget = true;
            parts.skipButton = skip.gameObject.AddComponent<Button>();
            parts.skipButton.transition = Selectable.Transition.None;

            var keyObject = new GameObject("KeyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform keyRect = keyObject.GetComponent<RectTransform>();
            keyRect.SetParent(skip, false);
            SetRect(keyRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(48f, 48f));
            parts.skipKeyIcon = keyObject.GetComponent<Image>();
            parts.skipKeyIcon.preserveAspect = true;
            parts.skipKeyIcon.raycastTarget = false;

            parts.skipLabel = CreateText(skip, "Label", font, 26f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(parts.skipLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(40f, 0f), new Vector2(-80f, 0f));
            parts.skipLabel.text = "스킵하기";

            DialoguePanel panel = canvasObject.AddComponent<DialoguePanel>();
            panel.Configure(style);
            panel.Bind(parts);
            panel.ApplyLayoutFromStyle(); // 처음 한 번만 스타일 배치. 이후는 씬/프리팹 값이 기준
            return panel;
        }

        // ---------------- 헬퍼 ----------------

        static Image CreateStretchedImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            StretchAll(rect, Vector2.zero, Vector2.zero);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Image CreatePortrait(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchor, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(560f, 760f));
            Image image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        static TMP_Text CreateText(
            Transform parent,
            string name,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, pivot, position, size);
            return rect;
        }

        static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void StretchAll(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
