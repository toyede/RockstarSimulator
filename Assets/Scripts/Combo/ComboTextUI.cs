using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage
{
    /// <summary>
    /// 우측 상단의 단일 종합 콤보 HUD. 개별 관객 반응은 AudienceReactionPopup이,
    /// 이 UI는 카드 한 장의 성공/실패와 현재 콤보/Fever 상태를 보여준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboTextUI : MonoBehaviour
    {
        [Header("Text (assign either one)")]
        [SerializeField] TMP_Text comboText;
        [SerializeField] LegacyText legacyComboText;
        [SerializeField, Tooltip("방금 얻은 콤보와 점수 전용. 비어 있으면 런타임에 자식 Text를 만든다")]
        TMP_Text recentGainText;
        [SerializeField] LegacyText legacyRecentGainText;

        [Header("Presentation")]
        [FormerlySerializedAs("lostMessageDuration")]
        [SerializeField, Min(0.1f)] float resultMessageDuration = 0.9f;
        [SerializeField] Color normalColor = new Color32(0xFB, 0xB9, 0x54, 0xFF);
        [SerializeField] Color successColor = new Color32(0x91, 0xDB, 0x69, 0xFF);
        [SerializeField] Color mixedColor = new Color32(0x9B, 0xAB, 0xB2, 0xFF);
        [SerializeField] Color lostColor = new Color32(0xEA, 0x4F, 0x36, 0xFF);
        [SerializeField] Color specialColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color feverColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [SerializeField, Min(0f)] float cardPresentationDelay = 0.085f;
        [SerializeField, Min(0.1f)] float recentGainDuration = 1.05f;
        [SerializeField, Min(0.01f)] float punchInDuration = 0.1f;
        [SerializeField, Min(0.01f)] float settleDuration = 0.12f;
        [SerializeField, Min(1f)] float punchScale = 1.2f;

        int _currentCombo;
        float _currentMultiplier = 1f;
        float _restoreAt;
        bool _showingResult;
        bool _showingFever;
        Coroutine _presentationRoutine;
        Coroutine _recentGainRoutine;
        RectTransform _recentGainRect;

        void Awake()
        {
            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta.y < 90f)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 90f);

            if (legacyComboText != null)
            {
                legacyComboText.resizeTextForBestFit = true;
                legacyComboText.resizeTextMinSize = 18;
                legacyComboText.resizeTextMaxSize = 36;
            }

            var outline = GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            EnsureRecentGainText();
            SetRecentGainVisible(false);
        }

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Subscribe<CardResolved>(OnCardResolved);

            _currentCombo =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            _currentMultiplier =
                ComboSystem.HasInstance ? ComboSystem.Instance.CurrentMultiplier : 1f;
            _showingFever =
                FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
            _showingResult = false;

            if (_showingFever) RefreshFever();
            else RefreshCombo();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            CancelPresentation();
            if (_recentGainRoutine != null)
            {
                StopCoroutine(_recentGainRoutine);
                _recentGainRoutine = null;
            }
            if (_recentGainRect != null) _recentGainRect.localScale = Vector3.one;
            SetRecentGainVisible(false);
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
                _showingResult = false;
                RefreshCombo();
            }

            if (!_showingResult || Time.unscaledTime < _restoreAt) return;
            _showingResult = false;
            RefreshCombo();
        }

        void OnComboChanged(ComboChanged e)
        {
            _currentCombo = e.CurrentCombo;
            _currentMultiplier = e.Multiplier;
            if (_showingFever) return;

            ApplyComboEmphasis();

            // 카드 결과가 같은 프레임에 뒤이어 오면 그쪽이 이 예약을 취소한다.
            // ResetCombo처럼 CardResolved가 없는 변경만 기본 표시로 돌아온다.
            StartPresentation(RefreshComboAfterDelay());
        }

        void OnCardResolved(CardResolved e)
        {
            _currentCombo = e.ComboCount;
            _currentMultiplier = e.ComboMultiplier;

            if (_showingFever ||
                e.FeverBonusScore > 0 ||
                (FeverSystem.HasInstance && FeverSystem.Instance.IsActive))
            {
                CancelPresentation();
                _showingFever = true;
                _showingResult = false;
                RefreshFever();
                ShowRecentGain($"FEVER BONUS  {FormatSignedScore(e.GainedScore)}", feverColor);
                return;
            }

            StartPresentation(ShowResultAfterDelay(e));
        }

        IEnumerator RefreshComboAfterDelay()
        {
            if (cardPresentationDelay > 0f)
                yield return new WaitForSecondsRealtime(cardPresentationDelay);
            _presentationRoutine = null;
            if (!_showingFever && !_showingResult)
                RefreshCombo();
        }

        IEnumerator ShowResultAfterDelay(CardResolved result)
        {
            if (cardPresentationDelay > 0f)
                yield return new WaitForSecondsRealtime(cardPresentationDelay);
            _presentationRoutine = null;
            ShowResult(result);
        }

        void ShowResult(CardResolved result)
        {
            _showingResult = true;
            _restoreAt = Time.unscaledTime + resultMessageDuration;
            ApplyComboEmphasis();

            if (result.Role == CardRole.Utility)
            {
                RefreshCombo();
                ShowRecentGain("UTILITY · COMBO 유지", mixedColor);
                return;
            }

            if (result.IsSpecialHit)
            {
                RefreshCombo();
                ShowRecentGain(
                    $"SPECIAL HIT!  +1 COMBO  ·  {FormatSignedScore(result.GainedScore)}",
                    specialColor);
                return;
            }

            // 최종 점수에는 Fever나 기타 배율이 섞일 수 있으므로, 콤보 시스템과
            // 동일하게 카드 자체의 RawScore를 기준으로 성공/유지/실패를 설명한다.
            if (result.RawScore > 0)
            {
                RefreshCombo();
                ShowRecentGain(
                    $"+1 COMBO  ·  {FormatSignedScore(result.GainedScore)}",
                    successColor);
            }
            else if (result.RawScore < 0)
            {
                RefreshCombo();
                ShowRecentGain(
                    $"COMBO BREAK  ·  {FormatSignedScore(result.GainedScore)}",
                    lostColor);
            }
            else
            {
                RefreshCombo();
                ShowRecentGain(
                    $"COMBO 유지  ·  {FormatSignedScore(result.GainedScore)}",
                    mixedColor);
            }
        }

        void OnFeverStateChanged(FeverStateChanged e)
        {
            CancelPresentation();
            _showingFever = e.IsActive;
            _showingResult = false;
            if (_showingFever) RefreshFever();
            else RefreshCombo();
        }

        void RefreshFever()
        {
            float remaining = FeverSystem.HasInstance
                ? FeverSystem.Instance.Remaining
                : 0f;
            int scorePerAudience =
                FeverSystem.HasInstance && FeverSystem.Instance.Config != null
                    ? FeverSystem.Instance.Config.FeverScorePerAudience
                    : 0;

            SetColor(feverColor);
            SetText(
                $"FEVER TIME!  {remaining:0.0}s  ·  COMBO {_currentCombo:00}\n" +
                $"ALL CARDS GOLD · 관객당 +{scorePerAudience}");
        }

        void RefreshCombo()
        {
            int interval =
                FeverSystem.HasInstance && FeverSystem.Instance.Config != null
                    ? FeverSystem.Instance.Config.FeverComboInterval
                    : 5;
            int remainder = Mathf.Max(0, _currentCombo) % Mathf.Max(1, interval);
            int untilFever = Mathf.Max(1, interval - remainder);

            SetColor(normalColor);
            SetText(
                $"CONTEXT COMBO  ·  FEVER까지 {untilFever}\n" +
                BuildComboLine(_currentCombo, _currentMultiplier));
            ApplyComboEmphasis();
        }

        static string BuildComboLine(int combo, float multiplier)
        {
            return multiplier > 1f
                ? $"{Mathf.Max(0, combo):00} COMBO · x{multiplier:0.00}"
                : $"{Mathf.Max(0, combo):00} COMBO";
        }

        static string FormatSignedScore(int score)
        {
            if (score > 0) return $"+{score:N0} SCORE";
            return $"{score:N0} SCORE";
        }

        void ApplyComboEmphasis()
        {
            int size = _currentCombo >= 6 ? 36 :
                       _currentCombo >= 4 ? 34 :
                       _currentCombo >= 2 ? 32 : 28;

            if (comboText != null)
            {
                comboText.enableAutoSizing = true;
                comboText.fontSizeMin = 22f;
                comboText.fontSizeMax = size;
            }

            if (legacyComboText != null)
            {
                legacyComboText.fontSize = size;
                legacyComboText.resizeTextForBestFit = true;
                legacyComboText.resizeTextMinSize = 20;
                legacyComboText.resizeTextMaxSize = size;
            }
        }

        void EnsureRecentGainText()
        {
            if (recentGainText != null)
            {
                _recentGainRect = recentGainText.rectTransform;
                return;
            }

            if (legacyRecentGainText != null)
            {
                _recentGainRect = legacyRecentGainText.rectTransform;
                return;
            }

            var canvasObject = new GameObject(
                "ComboResultOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 125;

            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject(
                "RecentComboGainText",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            go.transform.SetParent(canvasObject.transform, false);

            _recentGainRect = (RectTransform)go.transform;
            _recentGainRect.anchorMin = new Vector2(0.5f, 1f);
            _recentGainRect.anchorMax = new Vector2(0.5f, 1f);
            _recentGainRect.pivot = new Vector2(0.5f, 1f);
            _recentGainRect.anchoredPosition = new Vector2(0f, -96f);
            _recentGainRect.sizeDelta = new Vector2(760f, 74f);

            if (comboText != null)
            {
                recentGainText = go.AddComponent<TextMeshProUGUI>();
                recentGainText.font = comboText.font;
                recentGainText.fontSharedMaterial = comboText.fontSharedMaterial;
                recentGainText.alignment = TextAlignmentOptions.Top;
                recentGainText.enableAutoSizing = true;
                recentGainText.fontSizeMin = 28f;
                recentGainText.fontSizeMax = 40f;
                recentGainText.raycastTarget = false;
            }
            else
            {
                legacyRecentGainText = go.AddComponent<LegacyText>();
                if (legacyComboText != null)
                {
                    legacyRecentGainText.font = legacyComboText.font;
                    legacyRecentGainText.fontStyle = FontStyle.Bold;
                }
                legacyRecentGainText.alignment = TextAnchor.UpperCenter;
                legacyRecentGainText.resizeTextForBestFit = true;
                legacyRecentGainText.resizeTextMinSize = 28;
                legacyRecentGainText.resizeTextMaxSize = 40;
                legacyRecentGainText.horizontalOverflow = HorizontalWrapMode.Wrap;
                legacyRecentGainText.verticalOverflow = VerticalWrapMode.Overflow;
                legacyRecentGainText.raycastTarget = false;
            }

            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        void ShowRecentGain(string message, Color color)
        {
            if (_recentGainRect == null) EnsureRecentGainText();
            if (_recentGainRect == null) return;

            if (_recentGainRoutine != null) StopCoroutine(_recentGainRoutine);
            _recentGainRoutine = StartCoroutine(AnimateRecentGain(message, color));
        }

        IEnumerator AnimateRecentGain(string message, Color color)
        {
            SetRecentGainText(message);
            SetRecentGainColor(color);
            SetRecentGainVisible(true);

            float elapsed = 0f;
            while (elapsed < punchInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, punchInDuration));
                _recentGainRect.localScale = Vector3.one * Mathf.Lerp(0.75f, punchScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, settleDuration));
                _recentGainRect.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, t);
                yield return null;
            }

            float hold = Mathf.Max(0f, recentGainDuration - punchInDuration - settleDuration - 0.2f);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            elapsed = 0f;
            const float fadeDuration = 0.2f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color faded = color;
                faded.a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                SetRecentGainColor(faded);
                yield return null;
            }

            _recentGainRect.localScale = Vector3.one;
            SetRecentGainVisible(false);
            _recentGainRoutine = null;
        }

        void SetRecentGainText(string value)
        {
            if (recentGainText != null) recentGainText.text = value;
            if (legacyRecentGainText != null) legacyRecentGainText.text = value;
        }

        void SetRecentGainColor(Color value)
        {
            if (recentGainText != null) recentGainText.color = value;
            if (legacyRecentGainText != null) legacyRecentGainText.color = value;
        }

        void SetRecentGainVisible(bool visible)
        {
            if (recentGainText != null) recentGainText.gameObject.SetActive(visible);
            if (legacyRecentGainText != null) legacyRecentGainText.gameObject.SetActive(visible);
        }

        void StartPresentation(IEnumerator routine)
        {
            CancelPresentation();
            _presentationRoutine = StartCoroutine(routine);
        }

        void CancelPresentation()
        {
            if (_presentationRoutine == null) return;
            StopCoroutine(_presentationRoutine);
            _presentationRoutine = null;
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
            resultMessageDuration = Mathf.Max(0.1f, resultMessageDuration);
            cardPresentationDelay = Mathf.Max(0f, cardPresentationDelay);
            recentGainDuration = Mathf.Max(0.1f, recentGainDuration);
            punchInDuration = Mathf.Max(0.01f, punchInDuration);
            settleDuration = Mathf.Max(0.01f, settleDuration);
            punchScale = Mathf.Max(1f, punchScale);
        }
#endif
    }
}
