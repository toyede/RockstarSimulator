using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>HUD 숫자의 반동과 풀링된 증감 표시. 라벨과 독립적으로 움직인다.</summary>
    [DisallowMultipleComponent]
    public sealed class HudNumberFeedback : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text valueText;
        [SerializeField] private UnityEngine.UI.Text popupTemplate;
        [Header("Number motion")]
        [SerializeField, Min(0.1f)] private float pulseDuration = 0.52f;
        [SerializeField, Range(1f, 1.6f)] private float peakScale = 1.22f;
        [SerializeField, Min(0f)] private float bounceHeight = 10f;
        [SerializeField, Min(0f)] private float lossShakeWidth = 6f;
        [Header("Floating change")]
        [SerializeField, Min(0.1f)] private float popupDuration = 0.85f;
        [SerializeField, Min(0f)] private float popupRise = 28f;
        [SerializeField] private Color gainColor = Color.white;
        [SerializeField] private Color lossColor = new Color32(255, 86, 86, 255);

        struct Popup
        {
            public UnityEngine.UI.Text Text;
            public Vector2 Origin;
            public Color Color;
            public float Age;
        }

        readonly List<Popup> _popups = new List<Popup>(8);
        Vector2 _basePosition, _startOffset;
        Vector3 _baseScale, _startScale;
        float _pulseAge = -1f;
        bool _loss, _initialized;
        bool _anchorRight;

        public float PeakScale => peakScale;
        public float LossShakeWidth => lossShakeWidth;

        void Awake() => Initialize();

        /// <summary>숫자 반동을 오른쪽 끝 기준으로 바꾼다 (오른쪽 끝을 고정하고 왼쪽 위로 커진다).</summary>
        public void AnchorPulseToRight() => _anchorRight = true;

        void Initialize()
        {
            if (_initialized || valueText == null) return;
            _basePosition = valueText.rectTransform.anchoredPosition;
            _baseScale = valueText.rectTransform.localScale;
            if (popupTemplate != null) popupTemplate.gameObject.SetActive(false);
            _initialized = true;
        }

        public Color GainColor => gainColor;

        public void Play(int delta) => Play(delta, gainColor);

        /// <summary>증가 표시 색만 바꿔 재생한다. 감소는 항상 lossColor 를 쓴다.</summary>
        public void Play(int delta, Color positiveColor) => Play(delta, positiveColor, 1f);

        /// <summary>증감 표시 글자를 sizeScale 배로 키워 재생한다. 1 미만은 템플릿 크기로 고정한다.</summary>
        public void Play(int delta, Color positiveColor, float sizeScale)
        {
            if (delta == 0 || !isActiveAndEnabled) return;
            if (!BeginPulse(delta < 0)) return;
            ShowPopup(delta > 0 ? $"+{delta}" : delta.ToString(),
                delta < 0 ? lossColor : positiveColor, false, sizeScale);
        }

        public void PlayMessage(string message, Color color)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(message) || !BeginPulse(false)) return;
            ShowPopup(message, color, true);
        }

        bool BeginPulse(bool loss)
        {
            Initialize();
            if (!_initialized) return false;
            var rect = valueText.rectTransform;
            // 진행 중이던 반동의 오른쪽 기준 보정은 빼고 순수 오프셋만 이어받는다.
            _startOffset = rect.anchoredPosition - _basePosition - RightAnchorShift(rect.localScale.x);
            _startScale = rect.localScale;
            _pulseAge = 0f;
            _loss = loss;
            return true;
        }

        // 오른쪽 끝을 고정하기 위해 배율만큼 왼쪽으로 옮길 거리.
        Vector2 RightAnchorShift(float scaleX)
        {
            if (!_anchorRight) return Vector2.zero;
            var rect = valueText.rectTransform;
            return Vector2.left * (rect.rect.width * (1f - rect.pivot.x) * (scaleX - _baseScale.x));
        }

        void ShowPopup(string message, Color color, bool compact, float sizeScale = 1f)
        {
            if (popupTemplate == null) return;

            int baseSize = popupTemplate.fontSize;
            bool scaled = !compact && sizeScale > 1f;
            int fontSize = compact ? Mathf.Max(12, Mathf.RoundToInt(baseSize * 0.65f))
                : scaled ? Mathf.RoundToInt(baseSize * sizeScale)
                : baseSize;

            // 연속 변화는 이전 표시를 지우지 않고 새 표시 높이만큼 위로 밀어 구분한다.
            float stackOffset = Mathf.Max(baseSize, fontSize) * 1.1f;
            for (int i = 0; i < _popups.Count; i++)
            {
                var previous = _popups[i];
                previous.Origin.y += stackOffset;
                _popups[i] = previous;
            }
            var popup = PoolManager.Spawn(popupTemplate, Vector3.zero, Quaternion.identity,
                popupTemplate.transform.parent);
            popup.gameObject.SetActive(true);
            // 커진 글자는 아래쪽 숫자를 가리지 않도록 늘어난 높이의 절반만큼 올린다.
            float lift = scaled ? (fontSize - baseSize) * 0.5f : 0f;
            popup.rectTransform.anchoredPosition = popupTemplate.rectTransform.anchoredPosition + Vector2.up * lift;
            popup.rectTransform.localRotation = Quaternion.identity;
            popup.transform.SetAsLastSibling();
            popup.text = message;
            popup.color = color;
            popup.fontSize = fontSize;
            // 템플릿 칸 폭을 넘는 큰 숫자가 줄바꿈되지 않게 한다.
            popup.horizontalOverflow = compact || scaled
                ? HorizontalWrapMode.Overflow : popupTemplate.horizontalOverflow;
            _popups.Add(new Popup { Text = popup, Origin = popup.rectTransform.anchoredPosition,
                Color = popup.color });
        }

        void Update()
        {
            if (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused) return;
            float dt = Time.unscaledDeltaTime;
            if (_pulseAge >= 0f && valueText != null)
            {
                _pulseAge += dt;
                float t = Mathf.Clamp01(_pulseAge / Mathf.Max(0.1f, pulseDuration));
                var rect = valueText.rectTransform;
                const float peakAt = 0.2f;
                Vector2 peakOffset = _loss ? Vector2.zero : Vector2.up * bounceHeight;
                if (t < peakAt)
                {
                    float rise = Mathf.SmoothStep(0f, 1f, t / peakAt);
                    rect.localScale = Vector3.Lerp(_startScale, _baseScale * peakScale, rise);
                    rect.anchoredPosition = _basePosition + Vector2.Lerp(_startOffset, peakOffset, rise);
                }
                else
                {
                    float settle = (t - peakAt) / (1f - peakAt);
                    float spring = Mathf.Cos(settle * Mathf.PI * 2.5f) * Mathf.Pow(1f - settle, 2f);
                    rect.localScale = _baseScale * (1f + (peakScale - 1f) * spring);
                    rect.anchoredPosition = _basePosition + peakOffset * spring;
                }
                rect.anchoredPosition += RightAnchorShift(rect.localScale.x);
                if (_loss)
                    rect.anchoredPosition += Vector2.right *
                        (Mathf.Sin(t * Mathf.PI * 8f) * lossShakeWidth * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f) RestoreNumber();
            }
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];
                popup.Age += dt;
                if (popup.Text == null || !popup.Text.gameObject.activeSelf || popup.Age >= popupDuration)
                {
                    if (popup.Text != null && popup.Text.gameObject.activeSelf) PoolManager.Despawn(popup.Text);
                    _popups.RemoveAt(i);
                    continue;
                }
                float t = popup.Age / Mathf.Max(0.1f, popupDuration);
                popup.Text.rectTransform.anchoredPosition = popup.Origin + Vector2.up * (popupRise * t);
                var color = popup.Color;
                color.a *= 1f - Mathf.SmoothStep(0f, 1f, t);
                popup.Text.color = color;
                _popups[i] = popup;
            }
        }

        void RestoreNumber()
        {
            _pulseAge = -1f;
            if (!_initialized || valueText == null) return;
            valueText.rectTransform.anchoredPosition = _basePosition;
            valueText.rectTransform.localScale = _baseScale;
        }

        public void Clear()
        {
            RestoreNumber();
            foreach (var popup in _popups)
            {
                if (popup.Text == null || !popup.Text.gameObject.activeSelf) continue;
                // 하위 OnDisable에서는 부모의 activeInHierarchy가 아직 true일 수 있다.
                // 즉시 숨기되 SetParent가 필요한 풀 반납은 항상 콜백 이후로 미룬다.
                popup.Text.gameObject.SetActive(false);
                if (Application.isPlaying && PoolManager.HasInstance)
                    PoolManager.Despawn(popup.Text.gameObject, 0.01f);
            }
            _popups.Clear();
        }

        void OnDisable() => Clear();
    }
}
