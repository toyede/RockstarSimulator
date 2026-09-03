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

        [Header("Fever Visual")]
        [SerializeField] Color feverBackgroundColor =
            new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [SerializeField] Color feverArtworkTint =
            new Color32(0xFF, 0xE7, 0x8A, 0xFF);
        [SerializeField] Color feverTextColor =
            new Color32(0x2E, 0x22, 0x2F, 0xFF);

        CardDefinition _boundCard;
        bool _feverVisual;
        bool _capturedBaseTextColors;
        Color _titleBaseColor = Color.white;
        Color _descriptionBaseColor = Color.white;
        Color _roleBaseColor = Color.white;
        SpecialCardIdleVFX _specialIdleVfx;

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
            CaptureBaseTextColors();
            EnsureSpecialIdleVfx();
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
            // Configure can run after Awake for runtime-created slots. Capture
            // the real prefab colours instead of retaining the white fallbacks.
            _capturedBaseTextColors = false;
            CaptureBaseTextColors();
        }

        public void Bind(CardDefinition card)
        {
            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _boundCard = card;
            CardRuntimePresentation presentation = CardRuntimePresentation.Resolve(card);
            if (artwork != null)
            {
                artwork.sprite = presentation.Artwork;
                artwork.enabled = presentation.Artwork != null;
                artwork.preserveAspect = true;
            }
            if (titleText != null) titleText.text = presentation.DisplayName;
            if (descriptionText != null) descriptionText.text = presentation.Description;
            EnsureSpecialIdleVfx();
            _specialIdleVfx.Bind(background, card.Role == CardRole.Special);
            ApplyFeverVisual();
        }

        public void SetFeverVisual(bool active)
        {
            _feverVisual = active;
            ApplyFeverVisual();
        }

        void OnDisable()
        {
            _feverVisual = false;
            _boundCard = null;
            if (_specialIdleVfx != null) _specialIdleVfx.SetActive(false);
        }

        void ApplyFeverVisual()
        {
            if (_boundCard == null) return;
            CaptureBaseTextColors();

            if (background != null)
                background.color = _feverVisual
                    ? feverBackgroundColor
                    : _boundCard.CardColor;
            if (artwork != null)
                artwork.color = _feverVisual
                    ? feverArtworkTint
                    : Color.white;

            if (titleText != null)
                titleText.color = _feverVisual ? feverTextColor : _titleBaseColor;
            if (descriptionText != null)
                descriptionText.color = _feverVisual ? feverTextColor : _descriptionBaseColor;
            if (roleText != null)
            {
                roleText.color = _feverVisual ? feverTextColor : _roleBaseColor;
                roleText.text = _feverVisual
                    ? $"FEVER | {GetRoleLabel(_boundCard)}"
                    : GetRoleLabel(_boundCard);
            }
        }

        void CaptureBaseTextColors()
        {
            if (_capturedBaseTextColors) return;
            if (titleText != null) _titleBaseColor = titleText.color;
            if (descriptionText != null) _descriptionBaseColor = descriptionText.color;
            if (roleText != null) _roleBaseColor = roleText.color;
            _capturedBaseTextColors = true;
        }

        void EnsureSpecialIdleVfx()
        {
            if (_specialIdleVfx != null) return;
            _specialIdleVfx = GetComponent<SpecialCardIdleVFX>();
            if (_specialIdleVfx == null)
                _specialIdleVfx = gameObject.AddComponent<SpecialCardIdleVFX>();
        }

        static string GetRoleLabel(CardDefinition card)
        {
            if (card.Role == CardRole.Utility)
            {
                switch (card.UtilityEffect)
                {
                    case UtilityCardEffect.Draw:
                        return "UTILITY · DRAW";
                    case UtilityCardEffect.Reroll:
                        return "UTILITY · REROLL";
                    case UtilityCardEffect.ExtendPerformanceTime:
                        return "UTILITY · ENCORE";
                    default:
                        return "UTILITY";
                }
            }

            AudienceReactionProfile profile = card.AudienceReaction;
            if (profile == null || !profile.AppliesToAudience)
                return "AUDIENCE DATA MISSING";

            if (profile.ReactionMode == AudienceReactionMode.FixedAllAudience)
                return $"ALL {profile.FixedReactionValue:+0;-0;0}";

            return
                $"PREF C{profile.ChillScore} " +
                $"S{profile.SingalongScore} M{profile.MoshScore}  " +
                $"STAGE {profile.CalmScore}/{profile.MiddleScore}/{profile.ExcitedScore}";
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
