#if UNITY_EDITOR
using System;
using System.Linq;
using ContextStage;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStageEditor
{
    public static class AugmentSelectionArtSetup
    {
        const string SpriteFolder = "Assets/Sprites/0823_art";
        const string TierSpriteFolder = "Assets/Sprites/0903/0903";
        const string PrefabFolder = "Assets/Resources/UI";
        const string PrefabPath = PrefabFolder + "/AugmentSelectionPopup.prefab";
        const string FontPath = "Assets/Font/DungGeunMo.ttf";
        const string BlurShaderPath = "Assets/Shaders/UIKawaseBlur.shader";
        static bool _previewOwnedModal;
        static bool _previewReroll;

        [MenuItem("Tools/Tour/Setup Augment Selection Art", false, 30)]
        public static void Setup()
        {
            AugmentChoiceView.TierCardArt silver = LoadTierArt("silver_card");
            AugmentChoiceView.TierCardArt gold = LoadTierArt("gold_card");
            EnsureFolder(PrefabFolder);
            ConfigureModalTexture();
            ConfigureCardListTexture();

            Sprite background = LoadSprite("2_background.png");
            Sprite card = LoadSprite("2_bronze_card.png");
            Sprite cardFrame1 = LoadSprite("2_bronze_card_ani_0001.png");
            Sprite cardFrame2 = LoadSprite("2_bronze_card_ani_0002.png");
            Sprite cardFrame3 = LoadSprite("2_bronze_card_ani_0003.png");
            Sprite reroll = LoadSprite("2_card_reroll_button.png");
            Sprite rerollShadow = LoadSprite("2_card_reroll_button_shadow.png");
            Sprite cardList = LoadSprite("2_card_list.png");
            Sprite close = LoadSprite("2_x.png");
            Sprite modalBackground = LoadSprite("2_modal_background.png");
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Shader blurShader = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);

            if (new[]
                {
                    background, card, cardFrame1, cardFrame2, cardFrame3,
                    reroll, rerollShadow, cardList, close, modalBackground
                }.Any(sprite => sprite == null) ||
                font == null ||
                blurShader == null)
            {
                throw new MissingReferenceException(
                    "증강 선택 프리팹에 필요한 0823_art 스프라이트, 폰트 또는 블러 셰이더가 없습니다.");
            }

            GameObject root = CreateRectObject(
                "AugmentSelectionPopup",
                null,
                new Vector2(1920f, 1080f),
                Vector2.zero,
                typeof(CanvasGroup));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();

                Image backgroundImage = CreateImage(
                    "Background",
                    root.transform,
                    background,
                    new Vector2(1920f, 1080f),
                    Vector2.zero);
                Stretch(backgroundImage.rectTransform);

                AugmentChoiceView[] choices = new AugmentChoiceView[3];
                float[] choiceX = { -478f, 0f, 478f };
                for (int i = 0; i < choices.Length; i++)
                {
                    choices[i] = CreateChoice(
                        root.transform,
                        i,
                        new Vector2(choiceX[i], -27.5f),
                        font,
                        card,
                        cardFrame1,
                        cardFrame2,
                        cardFrame3,
                        reroll,
                        rerollShadow);
                    choices[i].ConfigureTierArt(silver, gold);
                }

                Button ownedListButton = CreateOwnedListButton(
                    root.transform,
                    font,
                    cardList);
                GameObject ownedModalBlocker = CreateOwnedModalBlocker(root.transform);

                CreateOwnedModal(
                    root.transform,
                    font,
                    modalBackground,
                    close,
                    blurShader,
                    out GameObject modal,
                    out Button closeButton,
                    out GameObject[] rows,
                    out Image[] icons,
                    out Text[] names,
                    out Text[] descriptions);

                AugmentSelectionPopup popup = root.AddComponent<AugmentSelectionPopup>();
                popup.Configure(rootGroup, null, null, null, null, choices);
                popup.ConfigureOwnedModal(
                    ownedListButton,
                    ownedModalBlocker,
                    modal,
                    closeButton,
                    rows,
                    icons,
                    names,
                    descriptions);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("증강 선택 프리팹 저장에 실패했습니다.");

                AssetDatabase.SaveAssets();
                Debug.Log($"[AugmentSelectionArtSetup] Figma 증강 선택 UI 생성 완료: {PrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/Tour/Update Augment Tier Art", false, 34)]
        public static void UpdateTierArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("증강 등급 아트 연결은 Edit Mode에서 실행하세요.");

            AugmentChoiceView.TierCardArt silver = LoadTierArt("silver_card");
            AugmentChoiceView.TierCardArt gold = LoadTierArt("gold_card");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AugmentChoiceView[] choices = root.GetComponentsInChildren<AugmentChoiceView>(true);
                if (choices.Length != AugmentSelectionPopup.VisibleSlotCount)
                    throw new InvalidOperationException("증강 프리팹의 후보 슬롯 수가 예상과 다릅니다.");

                // 기존 배치와 참조를 유지하고 등급별 아트 필드만 갱신한다.
                foreach (AugmentChoiceView choice in choices)
                {
                    var serialized = new SerializedObject(choice);
                    AssignTierArt(serialized.FindProperty("silverCardArt"), silver);
                    AssignTierArt(serialized.FindProperty("goldCardArt"), gold);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("증강 등급 아트 프리팹 저장에 실패했습니다.");
                Debug.Log($"[AugmentSelectionArtSetup] {choices.Length}개 슬롯에 실버·골드 아트를 연결했습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AssignTierArt(SerializedProperty property, AugmentChoiceView.TierCardArt art)
        {
            property.FindPropertyRelative("front").objectReferenceValue = art.front;
            property.FindPropertyRelative("angled").objectReferenceValue = art.angled;
            property.FindPropertyRelative("edge").objectReferenceValue = art.edge;
        }

        static AugmentChoiceView.TierCardArt LoadTierArt(string folder)
        {
            Sprite Load(string file) => AssetDatabase.LoadAllAssetsAtPath($"{TierSpriteFolder}/{folder}/{file}")
                .OfType<Sprite>().FirstOrDefault();
            var art = new AugmentChoiceView.TierCardArt
            {
                front = Load("gold_card_flip_0000.png"),
                angled = Load("gold_card_ani_0001.png"),
                edge = Load("gold_card_ani_0002.png")
            };
            if (!art.IsComplete)
                throw new MissingReferenceException($"증강 {folder}의 정면 또는 회전 스프라이트가 없습니다.");
            return art;
        }

        [MenuItem("Tools/Tour/Preview Augment Selection Art", false, 31)]
        public static void Preview()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[AugmentSelectionArtSetup] Play Mode에서만 미리보기를 실행할 수 있습니다.");
                return;
            }

            GameObject existing = GameObject.Find("CodexAugmentPreview");
            if (existing != null) UnityEngine.Object.Destroy(existing);

            Canvas[] existingCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < existingCanvases.Length; i++)
                existingCanvases[i].enabled = false;

            var canvasObject = new GameObject(
                "CodexAugmentPreview",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            AugmentSelectionPopup prefab =
                Resources.Load<AugmentSelectionPopup>("UI/AugmentSelectionPopup");
            if (prefab == null)
            {
                Debug.LogError("[AugmentSelectionArtSetup] 미리보기 프리팹을 찾지 못했습니다.");
                UnityEngine.Object.Destroy(canvasObject);
                return;
            }

            AugmentSelectionPopup popup =
                UnityEngine.Object.Instantiate(prefab, canvasObject.transform, false);
            CardDefinition previewGrantedCard = LoadCardDefinition(
                "Assets/Card_Prefab/Card_StageControl_Bronze.prefab");
            var model = new AugmentSelectionScreenModel
            {
                ownedAugmentCount = 2
            };
            model.choices.Add(CreatePreviewChoice(
                "무대 장악",
                "모든 관객에게 고정 +20 호응을 적용한다.",
                previewGrantedCard));
            model.choices.Add(CreatePreviewChoice(
                "사전 홍보",
                "공연 시작 시 일반 관객이 1명 추가된다.", tier: AugmentTier.Silver));
            model.choices.Add(CreatePreviewChoice(
                "동료 커버",
                "공연마다 콤보 끊김을 1회 방지한다.", tier: AugmentTier.Gold));
            model.ownedAugments.Add(new AugmentOwnedItemViewModel
            {
                displayName = "앙코르",
                description = "매 덱 묶음에 공연 시간을 연장하는 앙코르 카드를 추가한다."
            });
            model.ownedAugments.Add(new AugmentOwnedItemViewModel
            {
                displayName = "무대 장악",
                description = "매 덱 묶음에 무대 장악 카드를 추가한다."
            });
            popup.Show(model);
            popup.RerollRequested += slotIndex =>
            {
                if (slotIndex < 0 || slotIndex >= model.choices.Count) return;
                AugmentChoiceViewModel rerolled = model.choices[slotIndex].Clone();
                rerolled.slotIndex = slotIndex;
                rerolled.tier = (AugmentTier)(((int)rerolled.tier + 1) % 3);
                rerolled.tierLabel = rerolled.tier.ToString();
                rerolled.displayName += " · 리롤";
                rerolled.rerollsRemaining = 0;
                popup.PlayReroll(rerolled);
            };
            popup.SelectRequested += slotIndex =>
                popup.PlaySelection(slotIndex, _ => { });
            if (_previewReroll)
            {
                AugmentChoiceViewModel rerolled = model.choices[0].Clone();
                rerolled.slotIndex = 0;
                rerolled.displayName += " · 리롤";
                rerolled.rerollsRemaining = 0;
                popup.SetBusy(true);
                popup.PlayReroll(rerolled);
            }
            if (_previewOwnedModal)
            {
                Transform modal = popup.transform.Find("OwnedAugmentModal");
                if (modal != null)
                {
                    modal.SetAsLastSibling();
                    modal.gameObject.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                }
            }
        }

        [MenuItem("Tools/Tour/Preview Owned Augment Modal", false, 32)]
        public static void PreviewOwnedModal()
        {
            _previewOwnedModal = true;
            try
            {
                Preview();
            }
            finally
            {
                _previewOwnedModal = false;
            }
        }

        [MenuItem("Tools/Tour/Preview Augment Reroll", false, 33)]
        public static void PreviewReroll()
        {
            _previewReroll = true;
            try
            {
                Preview();
            }
            finally
            {
                _previewReroll = false;
            }
        }

        static AugmentChoiceViewModel CreatePreviewChoice(
            string displayName,
            string description,
            CardDefinition grantedCard = null,
            AugmentTier tier = AugmentTier.Bronze)
        {
            return new AugmentChoiceViewModel
            {
                displayName = displayName,
                description = description,
                grantedCard = grantedCard == null
                    ? null
                    : new AugmentCardPreviewViewModel
                    {
                        artwork = grantedCard.Artwork,
                        displayName = grantedCard.DisplayName,
                        description = grantedCard.Description
                    },
                tier = tier,
                tierLabel = tier.ToString(),
                tierColor = tier == AugmentTier.Silver ? new Color32(0xC0, 0xC0, 0xC0, 0xFF)
                    : tier == AugmentTier.Gold ? new Color32(0xFF, 0xD7, 0x00, 0xFF)
                    : new Color32(0xCD, 0x7F, 0x32, 0xFF),
                rerollsRemaining = 1,
                canSelect = true
            };
        }

        static AugmentChoiceView CreateChoice(
            Transform parent,
            int index,
            Vector2 position,
            Font font,
            Sprite card,
            Sprite frame1,
            Sprite frame2,
            Sprite frame3,
            Sprite reroll,
            Sprite rerollShadow)
        {
            GameObject choiceObject = CreateRectObject(
                $"AugmentChoice_{index + 1}",
                parent,
                new Vector2(378f, 667f),
                position,
                typeof(CanvasGroup));
            CanvasGroup choiceGroup = choiceObject.GetComponent<CanvasGroup>();

            GameObject cardHitObject = CreateRectObject(
                "Card",
                choiceObject.transform,
                new Vector2(378f, 513f),
                new Vector2(0f, 77f),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(AugmentHoverMotion));
            Image cardHitImage = cardHitObject.GetComponent<Image>();
            cardHitImage.color = new Color(1f, 1f, 1f, 0f);
            Button selectButton = cardHitObject.GetComponent<Button>();
            selectButton.transition = Selectable.Transition.None;

            GameObject visualObject = CreateRectObject(
                "CardVisual",
                cardHitObject.transform,
                new Vector2(378f, 513f),
                Vector2.zero);
            RectTransform visualRect = visualObject.GetComponent<RectTransform>();

            Image frameImage = CreateImage(
                "Frame",
                visualObject.transform,
                card,
                new Vector2(289f, 433f),
                new Vector2(0.5f, 0f));

            GameObject detailsObject = CreateRectObject(
                "Details",
                visualObject.transform,
                new Vector2(378f, 513f),
                Vector2.zero);

            Image iconBackground = CreateImage(
                "IconBackground",
                detailsObject.transform,
                GetBuiltinCircleSprite(),
                new Vector2(58f, 58f),
                new Vector2(1f, 119.5f));
            iconBackground.color = Color.white;

            Image iconImage = CreateImage(
                "Icon",
                iconBackground.transform,
                null,
                new Vector2(46f, 46f),
                Vector2.zero);
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            Text nameText = CreateText(
                "Name",
                detailsObject.transform,
                font,
                28,
                TextAnchor.MiddleCenter,
                Color.white,
                new Vector2(233f, 39f),
                new Vector2(0.5f, 55f));
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text descriptionText = CreateText(
                "Description",
                detailsObject.transform,
                font,
                20,
                TextAnchor.MiddleCenter,
                new Color32(0xE0, 0xE0, 0xE0, 0xFF),
                new Vector2(233f, 139f),
                new Vector2(0.5f, -34f));
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;

            GameObject grantedDetailsObject = CreateRectObject(
                "GrantedCardDetails",
                visualObject.transform,
                new Vector2(378f, 513f),
                Vector2.zero);

            Image grantedIconBackground = CreateImage(
                "IconBackground",
                grantedDetailsObject.transform,
                GetBuiltinCircleSprite(),
                new Vector2(58f, 58f),
                new Vector2(1f, 119.5f));
            grantedIconBackground.color = Color.white;

            Image grantedIconImage = CreateImage(
                "Icon",
                grantedIconBackground.transform,
                null,
                new Vector2(46f, 46f),
                Vector2.zero);
            grantedIconImage.preserveAspect = true;
            grantedIconImage.enabled = false;

            Image grantedCardArtwork = CreateImage(
                "GrantedCardArtwork",
                grantedDetailsObject.transform,
                null,
                new Vector2(112f, 168f),
                new Vector2(-70f, -92f));
            grantedCardArtwork.preserveAspect = true;
            grantedCardArtwork.enabled = false;

            Text grantedAugmentName = CreateText(
                "AugmentName",
                grantedDetailsObject.transform,
                font,
                28,
                TextAnchor.MiddleCenter,
                Color.white,
                new Vector2(233f, 39f),
                new Vector2(0.5f, 55f));
            grantedAugmentName.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text grantedEffectDescription = CreateText(
                "EffectDescription",
                grantedDetailsObject.transform,
                font,
                20,
                TextAnchor.UpperLeft,
                new Color32(0xE0, 0xE0, 0xE0, 0xFF),
                new Vector2(118f, 154f),
                new Vector2(68f, -92f));
            grantedEffectDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            grantedEffectDescription.verticalOverflow = VerticalWrapMode.Truncate;
            grantedDetailsObject.SetActive(false);

            AugmentHoverMotion cardHover = cardHitObject.GetComponent<AugmentHoverMotion>();
            cardHover.Configure(visualRect, new Vector2(-8f, 8f));

            GameObject rerollObject = CreateRectObject(
                "Reroll",
                choiceObject.transform,
                new Vector2(170f, 170f),
                new Vector2(-2f, -248.5f),
                typeof(CanvasGroup),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(AugmentHoverMotion));
            CanvasGroup rerollGroup = rerollObject.GetComponent<CanvasGroup>();
            Image rerollHitImage = rerollObject.GetComponent<Image>();
            rerollHitImage.color = new Color(1f, 1f, 1f, 0f);
            Button rerollButton = rerollObject.GetComponent<Button>();
            rerollButton.transition = Selectable.Transition.None;

            Image shadowImage = CreateImage(
                "Shadow",
                rerollObject.transform,
                rerollShadow,
                new Vector2(114.645f, 114.645f),
                new Vector2(1.9795f, -3.4585f));
            Image rerollImage = CreateImage(
                "Button",
                rerollObject.transform,
                reroll,
                new Vector2(114.645f, 114.645f),
                new Vector2(-0.9795f, 2.4585f));

            AugmentHoverMotion rerollHover = rerollObject.GetComponent<AugmentHoverMotion>();
            rerollHover.Configure(
                rerollImage.rectTransform,
                new Vector2(-3.698f, 3.219f),
                shadowImage.rectTransform,
                new Vector2(3.698f, -2.219f));

            AugmentChoiceView view = choiceObject.AddComponent<AugmentChoiceView>();
            view.Configure(
                frameImage,
                null,
                iconBackground,
                iconImage,
                null,
                null,
                nameText,
                descriptionText,
                selectButton,
                null,
                rerollButton,
                null);
            view.ConfigureDesign(
                visualRect,
                detailsObject,
                frameImage,
                choiceGroup,
                rerollGroup,
                cardHover,
                rerollHover,
                rerollImage,
                shadowImage,
                card,
                frame1,
                frame2,
                frame3);
            view.ConfigureGrantedCardDesign(
                grantedDetailsObject,
                grantedIconBackground,
                grantedIconImage,
                grantedCardArtwork,
                grantedAugmentName,
                grantedEffectDescription);
            return view;
        }

        static Button CreateOwnedListButton(Transform parent, Font font, Sprite cardList)
        {
            GameObject buttonObject = CreateTopLeftObject(
                "OwnedAugmentListButton",
                parent,
                new Vector2(390f, 78f),
                new Vector2(49f, -51f),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Image hitImage = buttonObject.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            Image icon = CreateImage(
                "Icon",
                buttonObject.transform,
                cardList,
                new Vector2(78f, 78f),
                new Vector2(-156f, 0f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            Text label = CreateText(
                "Label",
                buttonObject.transform,
                font,
                24,
                TextAnchor.MiddleLeft,
                Color.white,
                new Vector2(308f, 78f),
                new Vector2(41f, 0f));
            label.text = "보유 증강 확인하기";
            return button;
        }

        static void CreateOwnedModal(
            Transform parent,
            Font font,
            Sprite modalBackground,
            Sprite close,
            Shader blurShader,
            out GameObject modal,
            out Button closeButton,
            out GameObject[] rows,
            out Image[] icons,
            out Text[] names,
            out Text[] descriptions)
        {
            modal = CreateRectObject(
                "OwnedAugmentModal",
                parent,
                new Vector2(1430f, 780f),
                new Vector2(-10f, -4f),
                typeof(CanvasGroup),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));

            Image modalMaskImage = modal.GetComponent<Image>();
            modalMaskImage.sprite = modalBackground;
            modalMaskImage.color = Color.white;
            modalMaskImage.raycastTarget = false;
            modal.GetComponent<Mask>().showMaskGraphic = false;

            GameObject blurObject = CreateRectObject(
                "BlurredBackdrop",
                modal.transform,
                new Vector2(1430f, 780f),
                Vector2.zero,
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(UIRegionBlurBackdrop));
            RawImage blurImage = blurObject.GetComponent<RawImage>();
            blurImage.color = Color.white;
            blurImage.raycastTarget = false;
            blurImage.enabled = false;
            blurObject.GetComponent<UIRegionBlurBackdrop>().Configure(
                blurImage,
                modal.GetComponent<RectTransform>(),
                modal.GetComponent<CanvasGroup>(),
                blurShader);
            blurObject.transform.SetAsFirstSibling();

            Image backdropVeil = CreateImage(
                "BackdropVeil",
                modal.transform,
                null,
                new Vector2(1430f, 780f),
                Vector2.zero);
            backdropVeil.color = new Color(0.06f, 0.035f, 0.08f, 0.32f);
            backdropVeil.raycastTarget = false;

            Image background = CreateImage(
                "ModalBackground",
                modal.transform,
                modalBackground,
                new Vector2(1433f, 782f),
                new Vector2(0.5f, 0f));
            background.raycastTarget = true;
            background.color = new Color(1f, 1f, 1f, 0.48f);

            Text title = CreateTopLeftText(
                "Title",
                modal.transform,
                font,
                40,
                TextAnchor.MiddleLeft,
                Color.white,
                new Vector2(300f, 56f),
                new Vector2(64f, -42f));
            title.text = "보유 증강";

            Image headerLine = CreateTopLeftImage(
                "HeaderLine",
                modal.transform,
                null,
                new Vector2(1305f, 1f),
                new Vector2(64f, -122f));
            headerLine.color = new Color32(0xA8, 0xA8, 0xA8, 0xA0);

            rows = new GameObject[3];
            icons = new Image[3];
            names = new Text[3];
            descriptions = new Text[3];
            for (int i = 0; i < rows.Length; i++)
            {
                float top = 154f + i * 157f;
                GameObject row = CreateTopLeftObject(
                    $"OwnedAugment_{i + 1}",
                    modal.transform,
                    new Vector2(1305f, 133f),
                    new Vector2(64f, -top));
                rows[i] = row;

                Image circle = CreateTopLeftImage(
                    "IconBackground",
                    row.transform,
                    GetBuiltinCircleSprite(),
                    new Vector2(133f, 133f),
                    Vector2.zero);
                circle.color = Color.white;

                Image itemIcon = CreateImage(
                    "Icon",
                    circle.transform,
                    null,
                    new Vector2(104f, 104f),
                    Vector2.zero);
                itemIcon.preserveAspect = true;
                itemIcon.enabled = false;
                icons[i] = itemIcon;

                Text itemName = CreateTopLeftText(
                    "Name",
                    row.transform,
                    font,
                    28,
                    TextAnchor.MiddleLeft,
                    Color.white,
                    new Vector2(1132f, 40f),
                    new Vector2(173f, 0f));
                names[i] = itemName;

                Text itemDescription = CreateTopLeftText(
                    "Description",
                    row.transform,
                    font,
                    20,
                    TextAnchor.UpperLeft,
                    new Color32(0xE0, 0xE0, 0xE0, 0xFF),
                    new Vector2(1132f, 80f),
                    new Vector2(173f, -53f));
                itemDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
                itemDescription.verticalOverflow = VerticalWrapMode.Truncate;
                descriptions[i] = itemDescription;

                if (i < rows.Length - 1)
                {
                    Image line = CreateTopLeftImage(
                        "Divider",
                        row.transform,
                        null,
                        new Vector2(1305f, 1f),
                        new Vector2(0f, -157f));
                    line.color = new Color32(0x80, 0x80, 0x80, 0x78);
                }
            }

            GameObject closeObject = CreateTopLeftObject(
                "CloseButton",
                modal.transform,
                new Vector2(48f, 48f),
                new Vector2(1321f, -42f),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Image closeImage = closeObject.GetComponent<Image>();
            closeImage.sprite = close;
            closeImage.color = Color.white;
            closeImage.preserveAspect = false;
            closeButton = closeObject.GetComponent<Button>();
            closeButton.transition = Selectable.Transition.None;

            modal.SetActive(false);
        }

        static GameObject CreateOwnedModalBlocker(Transform parent)
        {
            GameObject blocker = CreateRectObject(
                "OwnedModalBlocker",
                parent,
                new Vector2(1920f, 1080f),
                Vector2.zero,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Image blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(1f, 1f, 1f, 0f);
            blockerImage.raycastTarget = true;
            Button blockerButton = blocker.GetComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blocker.SetActive(false);
            return blocker;
        }

        static GameObject CreateRectObject(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            params Type[] additionalComponents)
        {
            Type[] components = new[] { typeof(RectTransform) }
                .Concat(additionalComponents ?? Array.Empty<Type>())
                .ToArray();
            var gameObject = new GameObject(name, components);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return gameObject;
        }

        static GameObject CreateTopLeftObject(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            params Type[] additionalComponents)
        {
            GameObject gameObject = CreateRectObject(
                name,
                parent,
                size,
                Vector2.zero,
                additionalComponents);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            return gameObject;
        }

        static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = CreateRectObject(
                name,
                parent,
                size,
                position,
                typeof(CanvasRenderer),
                typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateTopLeftImage(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = CreateTopLeftObject(
                name,
                parent,
                size,
                position,
                typeof(CanvasRenderer),
                typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = CreateRectObject(
                name,
                parent,
                size,
                position,
                typeof(CanvasRenderer),
                typeof(Text));
            Text text = gameObject.GetComponent<Text>();
            ConfigureText(text, font, fontSize, alignment, color);
            return text;
        }

        static Text CreateTopLeftText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = CreateTopLeftObject(
                name,
                parent,
                size,
                position,
                typeof(CanvasRenderer),
                typeof(Text));
            Text text = gameObject.GetComponent<Text>();
            ConfigureText(text, font, fontSize, alignment, color);
            return text;
        }

        static void ConfigureText(
            Text text,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.lineSpacing = 1.15f;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite LoadSprite(string fileName)
        {
            string path = $"{SpriteFolder}/{fileName}";
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault();
        }

        static CardDefinition LoadCardDefinition(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab == null ? null : prefab.GetComponent<CardDefinition>();
        }

        static Sprite GetBuiltinCircleSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        static void ConfigureModalTexture()
        {
            string path = $"{SpriteFolder}/2_modal_background.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) return;

            bool needsReimport =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency;
            if (!needsReimport) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void ConfigureCardListTexture()
        {
            string path = $"{SpriteFolder}/2_card_list.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) return;

            bool needsReimport =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!needsReimport) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
