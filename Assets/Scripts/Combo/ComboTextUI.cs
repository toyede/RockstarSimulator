using GameJamKit;
using TMPro;
using UnityEngine;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class ComboTextUI : MonoBehaviour
    {
        [Header("Text (assign either one)")]
        [SerializeField] TMP_Text comboText;
        [SerializeField] LegacyText legacyComboText;

        [Header("Presentation")]
        [SerializeField, Min(0.1f)] float lostMessageDuration = 0.7f;
        [SerializeField] Color normalColor = new Color(1f, 0.88f, 0.25f, 1f);
        [SerializeField] Color lostColor = new Color(1f, 0.25f, 0.2f, 1f);

        int _currentCombo;
        float _currentMultiplier = 1f;
        float _restoreAt;
        bool _showingLost;

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            _currentCombo =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            _currentMultiplier =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentMultiplier : 1f;
            RefreshCombo();
        }

        void OnDisable() =>
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);

        void Update()
        {
            if (!_showingLost || Time.unscaledTime < _restoreAt) return;
            _showingLost = false;
            RefreshCombo();
        }

        void OnComboChanged(ComboChanged e)
        {
            _currentCombo = e.CurrentCombo;
            _currentMultiplier = e.Multiplier;
            if (!e.WasLost)
            {
                _showingLost = false;
                RefreshCombo();
                return;
            }

            _showingLost = true;
            _restoreAt = Time.unscaledTime + lostMessageDuration;
            SetColor(lostColor);
            SetText("COMBO LOST");
        }

        void RefreshCombo()
        {
            SetColor(normalColor);
            SetText(_currentMultiplier > 1f
                ? $"COMBO {_currentCombo}   x{_currentMultiplier:0.0}"
                : $"COMBO {_currentCombo}");
        }

        void SetText(string value)
        {
            if (comboText != null) comboText.text = value;
            if (legacyComboText != null) legacyComboText.text = value;
        }

        void SetColor(Color value)
        {
            if (comboText != null) comboText.color = value;
            if (legacyComboText != null) legacyComboText.color = value;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            lostMessageDuration = Mathf.Max(0.1f, lostMessageDuration);
        }
#endif
    }
}
