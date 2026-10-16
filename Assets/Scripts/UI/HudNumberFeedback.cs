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

        void Awake() => Initialize();

        void Initialize()
        {
            if (_initialized || valueText == null) return;
            _basePosition = valueText.rectTransform.anchoredPosition;
            _baseScale = valueText.rectTransform.localScale;
            if (popupTemplate != null) popupTemplate.gameObject.SetActive(false);
            _initialized = true;
        }

        public void Play(int delta)
        {
            if (delta == 0 || !isActiveAndEnabled) return;
            Initialize();
            if (!_initialized) return;
            var rect = valueText.rectTransform;
            _startOffset = rect.anchoredPosition - _basePosition;
            _startScale = rect.localScale;
            _pulseAge = 0f;
            _loss = delta < 0;
            if (popupTemplate == null) return;

            // 연속 변화는 이전 표시를 지우지 않고 위로 밀어 구분한다.
            for (int i = 0; i < _popups.Count; i++)
            {
                var previous = _popups[i];
                previous.Origin.y += popupTemplate.fontSize * 1.1f;
                _popups[i] = previous;
            }
            var popup = PoolManager.Spawn(popupTemplate, Vector3.zero, Quaternion.identity,
                popupTemplate.transform.parent);
            popup.gameObject.SetActive(true);
            popup.rectTransform.anchoredPosition = popupTemplate.rectTransform.anchoredPosition;
            popup.rectTransform.localRotation = Quaternion.identity;
            popup.transform.SetAsLastSibling();
            popup.text = delta > 0 ? $"+{delta}" : delta.ToString();
            popup.color = _loss ? lossColor : gainColor;
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
