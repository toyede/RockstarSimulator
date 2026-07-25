using System.Globalization;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    public enum AudienceReactionDisplayMode
    {
        ReactionScore,
        EngagementDelta
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class AudienceReactionPopup : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] TextMeshPro valueText;
        [SerializeField] AudienceReactionDisplayMode displayMode =
            AudienceReactionDisplayMode.ReactionScore;
        [SerializeField] bool showZero;
        [SerializeField, Min(1)] int strongReactionThreshold = 15;
        [SerializeField] int strongNegativeThreshold = -10;
        [SerializeField] Color positiveColor = new Color(1f, 0.92f, 0.28f, 1f);
        [SerializeField] Color strongColor = new Color(0.35f, 1f, 0.5f, 1f);
        [SerializeField] Color negativeColor = new Color(1f, 0.55f, 0.2f, 1f);
        [SerializeField] Color strongNegativeColor = new Color(1f, 0.2f, 0.18f, 1f);
        [SerializeField] Color departedColor = new Color(0.8f, 0.82f, 0.88f, 1f);
        [SerializeField] Color zeroColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [Header("Motion")]
        [SerializeField, Min(0.05f)] float duration = 0.75f;
        [SerializeField, Min(0f)] float riseDistance = 0.55f;
        [SerializeField, Min(0.01f)] float startScale = 0.65f;
        [SerializeField, Min(0.01f)] float peakScale = 1.2f;
        [SerializeField, Range(0f, 1f)] float fadeStart = 0.55f;

        Vector3 _baseLocalPosition;
        Vector3 _baseLocalScale;
        float _elapsed;
        bool _initialized;
        bool _showing;
        int _lastReactionValue;
        float _lastEngagementDelta;

        public TextMeshPro ValueText => valueText;
        public AudienceReactionDisplayMode DisplayMode => displayMode;
        public bool IsConfigured => valueText != null;
        public bool IsShowing => _showing;
        public int LastReactionValue => _lastReactionValue;
        public float LastEngagementDelta => _lastEngagementDelta;
        public string DisplayedText =>
            valueText != null ? valueText.text : string.Empty;

        void Awake()
        {
            CaptureBaseTransform();
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[AudienceReactionPopup] TextMeshPro reference is required.",
                    this);
                enabled = false;
                return;
            }

            ResetVisual();
        }

        void Update()
        {
            if (!_showing) return;

            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, duration));
            float easedRise = 1f - (1f - progress) * (1f - progress);
            transform.localPosition =
                _baseLocalPosition + Vector3.up * (riseDistance * easedRise);

            float scale = progress < 0.25f
                ? Mathf.Lerp(startScale, peakScale, progress / 0.25f)
                : Mathf.Lerp(peakScale, 1f, (progress - 0.25f) / 0.75f);
            transform.localScale = _baseLocalScale * scale;

            float alpha = progress <= fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
            Color color = valueText.color;
            color.a = alpha;
            valueText.color = color;

            if (progress >= 1f) ResetVisual();
        }

        public void Show(
            int reactionValue,
            float engagementDelta,
            int sortingOrder)
        {
            if (!enabled || !IsConfigured) return;
            if (!_initialized) CaptureBaseTransform();

            _lastReactionValue = reactionValue;
            _lastEngagementDelta = engagementDelta;
            float displayedValue =
                displayMode == AudienceReactionDisplayMode.ReactionScore
                    ? reactionValue
                    : engagementDelta;
            if (!showZero && Mathf.Approximately(displayedValue, 0f))
            {
                ResetVisual();
                return;
            }

            ShowText(
                FormatValue(reactionValue, engagementDelta),
                ResolveColor(reactionValue),
                sortingOrder);
        }

        public void ShowDeparture(int sortingOrder)
        {
            if (!enabled || !IsConfigured) return;
            if (!_initialized) CaptureBaseTransform();

            ShowText("LEFT THE SHOW", departedColor, sortingOrder);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (valueText != null) valueText.renderer.sortingOrder = sortingOrder;
        }

        public void ResetVisual()
        {
            if (!_initialized) CaptureBaseTransform();

            _elapsed = 0f;
            _showing = false;
            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale;
            if (valueText == null) return;

            valueText.text = string.Empty;
            valueText.enabled = false;
        }

        string FormatValue(int reactionValue, float engagementDelta)
        {
            if (displayMode == AudienceReactionDisplayMode.ReactionScore)
            {
                string score =
                    reactionValue.ToString(CultureInfo.InvariantCulture);
                return $"{ResolveLabel(reactionValue)} {FormatSigned(score, reactionValue)}";
            }

            string delta =
                engagementDelta.ToString("0.#", CultureInfo.InvariantCulture);
            return $"{ResolveLabel(reactionValue)} {FormatSigned(delta, engagementDelta)}";
        }

        static string FormatSigned(string value, float numericValue) =>
            numericValue > 0f ? "+" + value : value;

        Color ResolveColor(int reactionValue)
        {
            if (reactionValue <= strongNegativeThreshold) return strongNegativeColor;
            if (reactionValue < 0) return negativeColor;
            if (reactionValue == 0) return zeroColor;
            return reactionValue >= strongReactionThreshold
                ? strongColor
                : positiveColor;
        }

        string ResolveLabel(int reactionValue)
        {
            if (reactionValue >= strongReactionThreshold) return "LOVE IT!";
            if (reactionValue > 0) return "INTERESTED";
            if (reactionValue <= strongNegativeThreshold) return "BORED";
            if (reactionValue < 0) return "NOT FOR ME";
            return "NO REACTION";
        }

        void ShowText(string text, Color color, int sortingOrder)
        {
            valueText.text = text;
            valueText.color = color;
            valueText.renderer.sortingOrder = sortingOrder;
            valueText.enabled = true;

            _elapsed = 0f;
            _showing = true;
            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale * startScale;
        }

        void CaptureBaseTransform()
        {
            if (_initialized) return;
            _baseLocalPosition = transform.localPosition;
            _baseLocalScale = transform.localScale;
            _initialized = true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            strongReactionThreshold = Mathf.Max(1, strongReactionThreshold);
            strongNegativeThreshold = Mathf.Min(-1, strongNegativeThreshold);
            duration = Mathf.Max(0.05f, duration);
            riseDistance = Mathf.Max(0f, riseDistance);
            startScale = Mathf.Max(0.01f, startScale);
            peakScale = Mathf.Max(0.01f, peakScale);
        }
#endif
    }
}
