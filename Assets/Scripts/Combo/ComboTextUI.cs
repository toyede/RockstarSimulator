using System.Collections;
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
        [SerializeField] Color feverColor = new Color(0.25f, 1f, 0.95f, 1f);
        [SerializeField, Min(0f)] float cardPresentationDelay = 0.085f;

        int _currentCombo;
        float _currentMultiplier = 1f;
        float _restoreAt;
        bool _showingLost;
        bool _showingFever;
        Coroutine _comboPresentationRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            _currentCombo =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            _currentMultiplier =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentMultiplier : 1f;
            _showingFever =
                FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
            if (_showingFever)
                RefreshFever();
            else
                RefreshCombo();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            _comboPresentationRoutine = null;
        }

        void Update()
        {
            if (_showingFever)
            {
                if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive)
                {
                    RefreshFever();
                    return;
                }

                _showingFever = false;
                RefreshCombo();
            }

            if (!_showingLost || Time.unscaledTime < _restoreAt) return;
            _showingLost = false;
            RefreshCombo();
        }

        void OnComboChanged(ComboChanged e)
        {
            if (_comboPresentationRoutine != null)
                StopCoroutine(_comboPresentationRoutine);
            _comboPresentationRoutine = StartCoroutine(
                ShowComboAfterDelay(e));
        }

        IEnumerator ShowComboAfterDelay(ComboChanged change)
        {
            if (cardPresentationDelay > 0f)
                yield return new WaitForSecondsRealtime(
                    cardPresentationDelay);
            _comboPresentationRoutine = null;
            ApplyComboChanged(change);
        }

        void ApplyComboChanged(ComboChanged e)
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

        void OnFeverStateChanged(FeverStateChanged e)
        {
            if (_comboPresentationRoutine != null)
            {
                StopCoroutine(_comboPresentationRoutine);
                _comboPresentationRoutine = null;
            }
            _showingFever = e.IsActive;
            _showingLost = false;
            if (_showingFever)
                RefreshFever();
            else
                RefreshCombo();
        }

        void RefreshFever()
        {
            float remaining = FeverSystem.HasInstance
                ? FeverSystem.Instance.Remaining
                : 0f;
            SetColor(feverColor);
            SetText(
                $"FEVER {remaining:0.0}s   COMBO {_currentCombo} " +
                $"x{_currentMultiplier:0.0}");
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
            cardPresentationDelay = Mathf.Max(0f, cardPresentationDelay);
        }
#endif
    }
}
