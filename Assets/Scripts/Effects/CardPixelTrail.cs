using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// Screen Space Canvas에서 카드 뒤를 따라오는 네모 픽셀 트레일.
    /// 카드 인스턴스마다 한 번 만든 Graphic을 풀 수명 동안 재사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardPixelTrail : MonoBehaviour
    {
        [SerializeField, Min(1f)] float sampleDistance = 9f;
        [SerializeField, Min(0.01f)] float lifetime = 0.28f;
        [SerializeField, Min(1f)] float headSize = 10f;
        [SerializeField, Min(1f)] float tailSize = 3f;
        [SerializeField, Range(4, 64)] int maximumPixels = 28;

        PixelTrailGraphic _graphic;
        RectTransform _dragLayer;
        Color _color = Color.white;
        bool _useGoldAccent;

        public int PixelCount =>
            _graphic != null ? _graphic.PixelCount : 0;

        public void Bind(
            RectTransform dragLayer,
            CardRole role,
            HeatStage stage,
            Color cardColor)
        {
            _dragLayer = dragLayer;
            _color = CardVFXPalette.ResolveTrail(role, stage, cardColor);
            _useGoldAccent = role == CardRole.Special;
            EnsureGraphic();
            _graphic.Configure(
                _color,
                CardVFXPalette.Utility,
                _useGoldAccent,
                sampleDistance,
                lifetime,
                headSize,
                tailSize,
                maximumPixels);
        }

        public void Begin(Vector2 localPosition)
        {
            EnsureGraphic();
            if (_graphic == null) return;
            _graphic.Begin(localPosition);
        }

        public void AddPoint(Vector2 localPosition)
        {
            if (_graphic != null) _graphic.AddPoint(localPosition);
        }

        public void End()
        {
            if (_graphic != null) _graphic.End();
        }

        void OnDestroy()
        {
            if (_graphic != null)
                Destroy(_graphic.gameObject);
        }

        void EnsureGraphic()
        {
            if (_graphic != null || _dragLayer == null) return;

            var visual = new GameObject(
                $"{name}_PixelTrail",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(PixelTrailGraphic));
            var rect = (RectTransform)visual.transform;
            rect.SetParent(_dragLayer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            _graphic = visual.GetComponent<PixelTrailGraphic>();
            _graphic.raycastTarget = false;
            _graphic.Configure(
                _color,
                CardVFXPalette.Utility,
                _useGoldAccent,
                sampleDistance,
                lifetime,
                headSize,
                tailSize,
                maximumPixels);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PixelTrailGraphic : MaskableGraphic
    {
        struct Pixel
        {
            public Vector2 Position;
            public float Age;
            public float Rotation;
        }

        readonly List<Pixel> _pixels = new List<Pixel>(32);
        float _sampleDistance = 9f;
        float _lifetime = 0.28f;
        float _headSize = 10f;
        float _tailSize = 3f;
        int _maximumPixels = 28;
        bool _emitting;
        Color _accentColor = Color.white;
        bool _useAccent;

        public int PixelCount => _pixels.Count;

        public void Configure(
            Color trailColor,
            Color accentColor,
            bool useAccent,
            float sampleDistance,
            float lifetime,
            float headSize,
            float tailSize,
            int maximumPixels)
        {
            color = trailColor;
            _accentColor = accentColor;
            _useAccent = useAccent;
            _sampleDistance = Mathf.Max(1f, sampleDistance);
            _lifetime = Mathf.Max(0.01f, lifetime);
            _headSize = Mathf.Max(1f, headSize);
            _tailSize = Mathf.Max(1f, tailSize);
            _maximumPixels = Mathf.Clamp(maximumPixels, 4, 64);
            SetVerticesDirty();
        }

        public void Begin(Vector2 position)
        {
            _pixels.Clear();
            _emitting = true;
            gameObject.SetActive(true);
            AddPixel(position);
        }

        public void AddPoint(Vector2 position)
        {
            if (!_emitting) return;
            if (_pixels.Count > 0 &&
                Vector2.Distance(_pixels[_pixels.Count - 1].Position, position) <
                _sampleDistance)
                return;

            AddPixel(position);
        }

        public void End() => _emitting = false;

        void Update()
        {
            if (_pixels.Count == 0)
            {
                if (!_emitting) gameObject.SetActive(false);
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            for (int i = _pixels.Count - 1; i >= 0; i--)
            {
                Pixel pixel = _pixels[i];
                pixel.Age += deltaTime;
                if (pixel.Age >= _lifetime)
                    _pixels.RemoveAt(i);
                else
                    _pixels[i] = pixel;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < _pixels.Count; i++)
            {
                Pixel pixel = _pixels[i];
                float normalizedAge = Mathf.Clamp01(pixel.Age / _lifetime);
                float size = Mathf.Lerp(_headSize, _tailSize, normalizedAge);
                Color32 vertexColor = _useAccent && i % 4 == 0
                    ? _accentColor
                    : color;
                vertexColor.a = (byte)Mathf.RoundToInt(
                    255f * Mathf.Pow(1f - normalizedAge, 1.4f));
                AddQuad(vh, pixel.Position, size, pixel.Rotation, vertexColor);
            }
        }

        void AddPixel(Vector2 position)
        {
            if (_pixels.Count >= _maximumPixels)
                _pixels.RemoveAt(0);

            _pixels.Add(new Pixel
            {
                Position = position,
                Age = 0f,
                Rotation = Random.Range(0, 4) * 90f
            });
            SetVerticesDirty();
        }

        static void AddQuad(
            VertexHelper vh,
            Vector2 center,
            float size,
            float rotation,
            Color32 color)
        {
            int start = vh.currentVertCount;
            float half = size * 0.5f;
            Quaternion turn = Quaternion.Euler(0f, 0f, rotation);
            Vector2 a = center + (Vector2)(turn * new Vector3(-half, -half));
            Vector2 b = center + (Vector2)(turn * new Vector3(-half, half));
            Vector2 c = center + (Vector2)(turn * new Vector3(half, half));
            Vector2 d = center + (Vector2)(turn * new Vector3(half, -half));

            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.up);
            vh.AddVert(c, color, Vector2.one);
            vh.AddVert(d, color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
