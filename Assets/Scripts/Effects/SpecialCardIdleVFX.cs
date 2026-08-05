using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>손패에 있는 SPECIAL 카드를 드래그 전부터 알아볼 수 있게 하는 얇은 금색 맥동.</summary>
    [DisallowMultipleComponent]
    public sealed class SpecialCardIdleVFX : MonoBehaviour
    {
        static readonly Color Gold = new Color32(0xF9, 0xC2, 0x2B, 0xFF);

        Image _background;
        Outline _outlineA;
        Outline _outlineB;
        RectTransform _sparkRoot;
        Image[] _cornerPixels;
        bool _active;
        float _phase;

        public void Bind(Image background, bool active)
        {
            _background = background;
            if (active) EnsureVisuals();
            SetActive(active);
        }

        public void SetActive(bool active)
        {
            _active = active;
            enabled = active;

            if (_outlineA != null) _outlineA.enabled = active;
            if (_outlineB != null) _outlineB.enabled = active;
            if (_sparkRoot != null) _sparkRoot.gameObject.SetActive(active);
            if (!active) _phase = 0f;
        }

        void Update()
        {
            if (!_active) return;
            _phase += Time.unscaledDeltaTime;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_phase * Mathf.PI * 2f / 0.8f);
            Color outlineColor = Gold;
            outlineColor.a = Mathf.Lerp(0.35f, 0.95f, pulse);
            if (_outlineA != null) _outlineA.effectColor = outlineColor;
            if (_outlineB != null) _outlineB.effectColor = outlineColor;

            if (_cornerPixels == null) return;
            for (int i = 0; i < _cornerPixels.Length; i++)
            {
                Image pixel = _cornerPixels[i];
                float localPulse = 0.5f + 0.5f * Mathf.Sin(
                    _phase * 7f + i * Mathf.PI * 0.5f);
                Color color = Gold;
                color.a = Mathf.Lerp(0.2f, 1f, localPulse);
                pixel.color = color;
                pixel.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.25f, localPulse);
            }
        }

        void OnDisable()
        {
            if (_active) return;
            if (_outlineA != null) _outlineA.enabled = false;
            if (_outlineB != null) _outlineB.enabled = false;
            if (_sparkRoot != null) _sparkRoot.gameObject.SetActive(false);
        }

        void EnsureVisuals()
        {
            if (_background != null && _outlineA == null)
            {
                _outlineA = _background.gameObject.AddComponent<Outline>();
                _outlineA.effectDistance = new Vector2(3f, -3f);
                _outlineA.useGraphicAlpha = true;

                _outlineB = _background.gameObject.AddComponent<Outline>();
                _outlineB.effectDistance = new Vector2(-3f, 3f);
                _outlineB.useGraphicAlpha = true;
            }

            if (_sparkRoot != null) return;

            var root = new GameObject("SpecialCardSparkles", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _sparkRoot = (RectTransform)root.transform;
            _sparkRoot.anchorMin = Vector2.zero;
            _sparkRoot.anchorMax = Vector2.one;
            _sparkRoot.offsetMin = Vector2.zero;
            _sparkRoot.offsetMax = Vector2.zero;
            _sparkRoot.SetAsLastSibling();

            Vector2[] anchors =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            Vector2[] offsets =
            {
                new Vector2(7f, 7f), new Vector2(-7f, 7f),
                new Vector2(7f, -7f), new Vector2(-7f, -7f),
            };

            _cornerPixels = new Image[4];
            for (int i = 0; i < _cornerPixels.Length; i++)
            {
                var pixelObject = new GameObject(
                    $"SpecialPixel_{i}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                pixelObject.transform.SetParent(_sparkRoot, false);
                var rect = (RectTransform)pixelObject.transform;
                rect.anchorMin = anchors[i];
                rect.anchorMax = anchors[i];
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = offsets[i];
                rect.sizeDelta = new Vector2(7f, 7f);

                Image image = pixelObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = Gold;
                _cornerPixels[i] = image;
            }
        }
    }
}
