using System;
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

        int _slotIndex = -1;
        bool _modelAllowsSelection;
        int _rerollsRemaining;
        bool _wired;

        public int SlotIndex => _slotIndex;
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

        public void Bind(AugmentChoiceViewModel model)
        {
            if (model == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            _slotIndex = model.slotIndex;
            _modelAllowsSelection = model.canSelect;
            _rerollsRemaining = Mathf.Max(0, model.rerollsRemaining);

            Color tierColor = model.tierColor;
            if (tierStrip != null) tierStrip.color = tierColor;
            if (iconBackground != null) iconBackground.color = WithAlpha(tierColor, 0.28f);
            if (cardBackground != null) cardBackground.color = new Color32(0x3E, 0x35, 0x46, 0xFF);

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

            SetInputEnabled(true);
        }

        public void SetInputEnabled(bool enabled)
        {
            if (selectButton != null)
                selectButton.interactable = enabled && _modelAllowsSelection && _slotIndex >= 0;

            if (rerollButton != null)
                rerollButton.interactable = enabled && _rerollsRemaining > 0 && _slotIndex >= 0;
        }

        public void Clear()
        {
            _slotIndex = -1;
            _modelAllowsSelection = false;
            _rerollsRemaining = 0;
            gameObject.SetActive(false);
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

        static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
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
