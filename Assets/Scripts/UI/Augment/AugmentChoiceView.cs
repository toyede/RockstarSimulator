using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 증강 후보 한 칸의 표시와 입력만 담당한다.
    /// 리롤 가능 여부나 선택 결과를 스스로 변경하지 않는다.
    /// </summary>
    public sealed class AugmentChoiceView : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] Image cardBackground;
        [SerializeField] Image tierStrip;
        [SerializeField] Image iconBackground;
        [SerializeField] Image iconImage;
        [SerializeField] Text iconPlaceholderText;
        [SerializeField] Text tierText;
        [SerializeField] Text nameText;
        [SerializeField] Text descriptionText;

        [Header("Input")]
        [SerializeField] Button selectButton;
        [SerializeField] Text selectButtonText;
        [SerializeField] Button rerollButton;
        [SerializeField] Text rerollButtonText;

        [Header("Figma Presentation")]
        [SerializeField] RectTransform cardVisualRoot;
        [SerializeField] GameObject cardDetailsRoot;
        [SerializeField] GameObject grantedCardDetailsRoot;
        [SerializeField] Image flipImage;
        [SerializeField] Image grantedCardIconBackground;
        [SerializeField] Image grantedCardIconImage;
        [SerializeField] Image grantedCardArtworkImage;
        [SerializeField] Text grantedCardAugmentNameText;
        [SerializeField] Text grantedCardEffectText;
        [SerializeField] CanvasGroup choiceCanvasGroup;
        [SerializeField] CanvasGroup rerollCanvasGroup;
        [SerializeField] AugmentHoverMotion cardHoverMotion;
        [SerializeField] AugmentHoverMotion rerollHoverMotion;
        [SerializeField] Image rerollButtonGraphic;
        [SerializeField] Image rerollShadowGraphic;
        [SerializeField] Sprite defaultCardSprite;
        [SerializeField] Sprite rerollFrame1;
        [SerializeField] Sprite rerollFrame2;
        [SerializeField] Sprite rerollFrame3;

        [Header("Tier Presentation")]
        [SerializeField] TierCardArt silverCardArt = new TierCardArt();
        [SerializeField] TierCardArt goldCardArt = new TierCardArt();
        [SerializeField, Min(0.1f)] float rerollDurationMultiplier = 1.05f;

        [Serializable]
        public sealed class TierCardArt
        {
            public Sprite front;
            public Sprite angled;
            public Sprite edge;

            public bool IsComplete => front != null && angled != null && edge != null;
        }

        int _slotIndex = -1;
        AugmentTier _tier;
        bool _modelAllowsSelection;
        int _rerollsRemaining;
        bool _isGrantCard;
        bool _wired;
        Coroutine _rerollRoutine;

        public int SlotIndex => _slotIndex;
        public RectTransform RectTransform => (RectTransform)transform;
        public CanvasGroup ChoiceCanvasGroup => choiceCanvasGroup;
        public event Action<int> SelectRequested;
        public event Action<int> RerollRequested;

        void Awake()
        {
            WireButtons();
        }

        void OnDestroy()
        {
            UnwireButtons();
        }

        void OnDisable()
        {
            if (_rerollRoutine != null)
            {
                StopCoroutine(_rerollRoutine);
                _rerollRoutine = null;
            }

            ResetPresentation();
        }

        public void Configure(
            Image background,
            Image strip,
            Image iconBack,
            Image icon,
            Text iconPlaceholder,
            Text tier,
            Text title,
            Text description,
            Button select,
            Text selectLabel,
            Button reroll,
            Text rerollLabel)
        {
            cardBackground = background;
            tierStrip = strip;
            iconBackground = iconBack;
            iconImage = icon;
            iconPlaceholderText = iconPlaceholder;
            tierText = tier;
            nameText = title;
            descriptionText = description;
            selectButton = select;
            selectButtonText = selectLabel;
            rerollButton = reroll;
            rerollButtonText = rerollLabel;
            WireButtons();
        }

        public void ConfigureDesign(
            RectTransform visualRoot,
            GameObject detailsRoot,
            Image cardFlipImage,
            CanvasGroup group,
            CanvasGroup rerollGroup,
            AugmentHoverMotion cardHover,
            AugmentHoverMotion rerollHover,
            Image rerollGraphic,
            Image rerollShadow,
            Sprite cardSprite,
            Sprite frame1,
            Sprite frame2,
            Sprite frame3)
        {
            cardVisualRoot = visualRoot;
            cardDetailsRoot = detailsRoot;
            flipImage = cardFlipImage;
            choiceCanvasGroup = group;
            rerollCanvasGroup = rerollGroup;
            cardHoverMotion = cardHover;
            rerollHoverMotion = rerollHover;
            rerollButtonGraphic = rerollGraphic;
            rerollShadowGraphic = rerollShadow;
            defaultCardSprite = cardSprite;
            rerollFrame1 = frame1;
            rerollFrame2 = frame2;
            rerollFrame3 = frame3;
            ResetPresentation();
        }

        public void ConfigureGrantedCardDesign(
            GameObject detailsRoot,
            Image iconBackground,
            Image icon,
            Image artwork,
            Text augmentName,
            Text effectDescription)
        {
            grantedCardDetailsRoot = detailsRoot;
            grantedCardIconBackground = iconBackground;
            grantedCardIconImage = icon;
            grantedCardArtworkImage = artwork;
            grantedCardAugmentNameText = augmentName;
            grantedCardEffectText = effectDescription;
            ResetPresentation();
        }

        public void ConfigureTierArt(TierCardArt silver, TierCardArt gold)
        {
            silverCardArt = silver;
            goldCardArt = gold;
            ResetPresentation();
        }

        TierCardArt ResolveTierArt(AugmentTier tier)
        {
            TierCardArt art = tier == AugmentTier.Silver ? silverCardArt
                : tier == AugmentTier.Gold ? goldCardArt : null;
            return art != null && art.IsComplete ? art : null;
        }

        Sprite GetFront(AugmentTier tier) => ResolveTierArt(tier)?.front ?? defaultCardSprite;

        Sprite GetRerollFrame(AugmentTier tier, int pose)
        {
            TierCardArt art = ResolveTierArt(tier);
            if (art != null) return pose == 2 ? art.edge : art.angled;
            Sprite frame = pose == 1 ? rerollFrame1 : pose == 2 ? rerollFrame2 : rerollFrame3;
            return frame != null ? frame : defaultCardSprite;
        }

        public void Bind(AugmentChoiceViewModel model)
        {
            if (model == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            _slotIndex = model.slotIndex;
            _tier = model.tier;
            _modelAllowsSelection = model.canSelect;
            _rerollsRemaining = Mathf.Max(0, model.rerollsRemaining);
            _isGrantCard = model.grantedCard != null;

            Color tierColor = model.tierColor;
            if (tierStrip != null) tierStrip.color = tierColor;
            if (iconBackground != null)
                iconBackground.color = defaultCardSprite != null
                    ? Color.white
                    : WithAlpha(tierColor, 0.28f);
            if (cardBackground != null) cardBackground.color = Color.white;

            if (flipImage != null)
            {
                Sprite front = GetFront(_tier);
                flipImage.sprite = front != null
                    ? front
                    : flipImage.sprite;
                flipImage.enabled = flipImage.sprite != null;
            }
            SetDetailsVisible(true);

            SetText(tierText, string.IsNullOrWhiteSpace(model.tierLabel) ? "AUGMENT" : model.tierLabel.ToUpperInvariant());
            SetText(nameText, model.displayName ?? "");
            SetText(descriptionText, model.description ?? "");
            SetText(selectButtonText, "SELECT");
            SetText(rerollButtonText, $"REROLL  ({_rerollsRemaining})");

            if (iconImage != null)
            {
                iconImage.sprite = model.icon;
                iconImage.color = Color.white;
                iconImage.enabled = model.icon != null;
            }

            if (iconPlaceholderText != null)
            {
                iconPlaceholderText.gameObject.SetActive(model.icon == null);
                iconPlaceholderText.text = GetInitial(model.displayName);
            }

            if (grantedCardIconBackground != null)
                grantedCardIconBackground.color = Color.white;
            if (grantedCardIconImage != null)
            {
                grantedCardIconImage.sprite = model.icon;
                grantedCardIconImage.color = Color.white;
                grantedCardIconImage.enabled = _isGrantCard && model.icon != null;
            }
            if (grantedCardArtworkImage != null)
            {
                grantedCardArtworkImage.sprite = _isGrantCard
                    ? model.grantedCard.artwork
                    : null;
                grantedCardArtworkImage.color = Color.white;
                grantedCardArtworkImage.enabled =
                    _isGrantCard && model.grantedCard.artwork != null;
            }
            SetText(grantedCardAugmentNameText, _isGrantCard ? model.displayName : "");
            SetText(grantedCardEffectText, _isGrantCard ? model.description : "");

            SetInputEnabled(true);
            ApplyRerollAvailability();
        }

        public void SetInputEnabled(bool enabled)
        {
            if (selectButton != null)
                selectButton.interactable = enabled && _modelAllowsSelection && _slotIndex >= 0;

            if (rerollButton != null)
                rerollButton.interactable = enabled && _rerollsRemaining > 0 && _slotIndex >= 0;

            cardHoverMotion?.SetInteractionEnabled(
                enabled && _modelAllowsSelection && _slotIndex >= 0);
            rerollHoverMotion?.SetInteractionEnabled(
                enabled && _rerollsRemaining > 0 && _slotIndex >= 0);
        }

        public void Clear()
        {
            _slotIndex = -1;
            _modelAllowsSelection = false;
            _rerollsRemaining = 0;
            _isGrantCard = false;
            gameObject.SetActive(false);
        }

        public void ResetPresentation()
        {
            cardHoverMotion?.ResetState();
            rerollHoverMotion?.ResetState();

            if (choiceCanvasGroup != null) choiceCanvasGroup.alpha = 1f;
            if (rerollCanvasGroup != null) rerollCanvasGroup.alpha = 1f;
            _isGrantCard = false;
            SetDetailsVisible(true);
            if (grantedCardDetailsRoot != null) grantedCardDetailsRoot.SetActive(false);
            if (grantedCardArtworkImage != null)
            {
                grantedCardArtworkImage.sprite = null;
                grantedCardArtworkImage.enabled = false;
            }
            if (grantedCardIconImage != null)
            {
                grantedCardIconImage.sprite = null;
                grantedCardIconImage.enabled = false;
            }
            SetText(grantedCardAugmentNameText, "");
            SetText(grantedCardEffectText, "");
            if (flipImage != null)
            {
                Sprite front = GetFront(_tier);
                flipImage.sprite = front != null
                    ? front
                    : cardBackground == null ? null : cardBackground.sprite;
                flipImage.enabled = flipImage.sprite != null;
            }
        }

        public bool PlayReroll(
            AugmentChoiceViewModel model,
            Action<bool> completed)
        {
            if (model == null || _rerollRoutine != null || !isActiveAndEnabled)
                return false;

            _rerollRoutine = StartCoroutine(PlayRerollRoutine(model, completed));
            return true;
        }

        public void ShowSelectedTransitionFrame()
        {
            cardHoverMotion?.ResetState();
            rerollHoverMotion?.ResetState();
            SetInputEnabled(false);
        }

        public void SetVisualAlpha(float alpha)
        {
            if (choiceCanvasGroup != null)
                choiceCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetRerollAlpha(float alpha)
        {
            if (rerollCanvasGroup != null)
                rerollCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        void WireButtons()
        {
            if (_wired || selectButton == null || rerollButton == null) return;
            selectButton.onClick.AddListener(OnSelectClicked);
            rerollButton.onClick.AddListener(OnRerollClicked);
            _wired = true;
        }

        void UnwireButtons()
        {
            if (!_wired) return;
            if (selectButton != null) selectButton.onClick.RemoveListener(OnSelectClicked);
            if (rerollButton != null) rerollButton.onClick.RemoveListener(OnRerollClicked);
            _wired = false;
        }

        void OnSelectClicked()
        {
            if (_slotIndex >= 0) SelectRequested?.Invoke(_slotIndex);
        }

        void OnRerollClicked()
        {
            if (_slotIndex >= 0 && _rerollsRemaining > 0) RerollRequested?.Invoke(_slotIndex);
        }

        IEnumerator PlayRerollRoutine(
            AugmentChoiceViewModel model,
            Action<bool> completed)
        {
            SetInputEnabled(false);
            cardHoverMotion?.ResetState();
            rerollHoverMotion?.ResetState();
            SetDetailsVisible(false);

            Sprite[] frames =
            {
                GetRerollFrame(_tier, 1),
                GetRerollFrame(_tier, 2),
                GetRerollFrame(_tier, 3),
                GetFront(_tier),
                GetRerollFrame(_tier, 3),
                GetRerollFrame(_tier, 2),
                GetRerollFrame(_tier, 1)
            };
            float[] durations = { 0.02f, 0.02f, 0.02f, 0.02f, 0.02f, 0.02f, 0.05f };

            for (int i = 0; i < frames.Length; i++)
            {
                if (flipImage != null)
                {
                    flipImage.sprite = frames[i] != null ? frames[i] : defaultCardSprite;
                    flipImage.enabled = flipImage.sprite != null;
                }
                float duration = durations[i] * Mathf.Max(0.1f, rerollDurationMultiplier);
                // 마지막 대기 안에 등급 공개 프레임을 포함해 총 시간을 늘리지 않는다.
                if (i == frames.Length - 1)
                    duration = Mathf.Max(0f, duration - Time.unscaledDeltaTime);
                yield return WaitUnscaled(duration);
            }

            // 새 등급은 내용이 공개되기 직전 한 프레임에만 보여준다.
            if (flipImage != null)
            {
                flipImage.sprite = GetRerollFrame(model.tier, 1);
                flipImage.enabled = flipImage.sprite != null;
            }
            yield return null;

            _rerollRoutine = null;
            Bind(model);
            SetInputEnabled(false);
            completed?.Invoke(true);
        }

        IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void ApplyRerollAvailability()
        {
            bool available = _rerollsRemaining > 0;
            if (rerollButtonGraphic != null)
                rerollButtonGraphic.color = available
                    ? Color.white
                    : new Color32(0x45, 0x45, 0x45, 0xFF);
            if (rerollShadowGraphic != null)
                rerollShadowGraphic.color = available
                    ? Color.white
                    : new Color32(0x45, 0x45, 0x45, 0xA0);
        }

        static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
        }

        void SetDetailsVisible(bool visible)
        {
            if (cardDetailsRoot != null)
                cardDetailsRoot.SetActive(visible && !_isGrantCard);
            if (grantedCardDetailsRoot != null)
                grantedCardDetailsRoot.SetActive(visible && _isGrantCard);
        }

        static string GetInitial(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";
            return value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
