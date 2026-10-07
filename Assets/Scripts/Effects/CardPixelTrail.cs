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
        [SerializeField, Min(1f)] float sampleDistance = 7f;
        [SerializeField, Min(0.01f)] float lifetime = 0.6f;
        [SerializeField, Min(1f)] float headSize = 16f;
        [SerializeField, Min(1f)] float tailSize = 5f;
        [SerializeField, Range(4, 96)] int maximumPixels = 56;

        PixelTrailGraphic _graphic;
        RectTransform _dragLayer;
        Color _color = Color.white;
        bool _useGoldAccent;
        CardAugmentVisual _style;

        public int PixelCount =>
            _graphic != null ? _graphic.PixelCount : 0;

        public void Bind(
            RectTransform dragLayer,
            CardRole role,
            HeatStage stage,
            Color cardColor,
            CardAugmentVisual style = CardAugmentVisual.None)
        {
            _style = style;
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
                maximumPixels,
                _style);
        }

        public void Begin(RectTransform source)
        {
            EnsureGraphic();
            if (_graphic == null ||
                !TryResolveTrailPosition(source, out Vector2 localPosition))
                return;
            _graphic.Begin(localPosition);
        }

        public void AddPoint(RectTransform source)
        {
            if (_graphic != null &&
                TryResolveTrailPosition(source, out Vector2 localPosition))
                _graphic.AddPoint(localPosition);
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
                maximumPixels,
                _style);
        }

        bool TryResolveTrailPosition(
            RectTransform source,
            out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            if (source == null || _graphic == null) return false;

            Vector3 worldCenter = source.TransformPoint(source.rect.center);
            Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
            Camera sourceCamera =
                sourceCanvas != null &&
                sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? sourceCanvas.worldCamera
                    : null;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                sourceCamera,
                worldCenter);

            RectTransform trailRect = _graphic.rectTransform;
            Canvas trailCanvas = trailRect.GetComponentInParent<Canvas>();
            Camera trailCamera =
                trailCanvas != null &&
                trailCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? trailCanvas.worldCamera
                    : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                trailRect,
                screenPosition,
                trailCamera,
                out localPosition);
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
            public Vector2 Direction;
            public float Phase;
            public float Distance;
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
        CardAugmentVisual _style;
        Vector2 _lastPosition;
        Vector2 _direction = Vector2.up;
        float _distance;

        bool IsAugmentTrail =>
            _style == CardAugmentVisual.Encore || _style == CardAugmentVisual.StageControl;

        public int PixelCount => _pixels.Count;

        public void Configure(
            Color trailColor,
            Color accentColor,
            bool useAccent,
            float sampleDistance,
            float lifetime,
            float headSize,
            float tailSize,
            int maximumPixels,
            CardAugmentVisual style = CardAugmentVisual.None)
        {
            _style = style;
            color = trailColor;
            _accentColor = accentColor;
            _useAccent = useAccent;
            _sampleDistance = Mathf.Max(1f, sampleDistance);
            _lifetime = Mathf.Max(0.01f, lifetime);
            if (IsAugmentTrail)
            {
                _sampleDistance *= 3f;
                _lifetime *= 1.8f;
            }
            _headSize = Mathf.Max(1f, headSize);
            _tailSize = Mathf.Max(1f, tailSize);
            _maximumPixels = Mathf.Clamp(maximumPixels, 4, 64);
            SetVerticesDirty();
        }

        public void Begin(Vector2 position)
        {
            _pixels.Clear();
            _lastPosition = position;
            _distance = 0f;
            _direction = Vector2.up;
            _emitting = true;
            gameObject.SetActive(true);
            AddPixel(position);
        }

        public void AddPoint(Vector2 position)
        {
            if (!_emitting) return;
            if (!IsAugmentTrail)
            {
                if (_pixels.Count == 0 || Vector2.Distance(_pixels[_pixels.Count - 1].Position, position) >= _sampleDistance)
                    AddPixel(position);
                return;
            }
            Vector2 delta = position - _lastPosition;
            float distance = delta.magnitude;
            // 느린 이동에서도 같은 위치에 불씨가 겹치지 않도록 이동 간격을 유지한다.
            if (distance < _sampleDistance) return;
            _direction = delta / distance;
            // 빠른 드래그에서도 나선이 끊기지 않도록 경로를 보간한다.
            int steps = Mathf.Min(24, Mathf.FloorToInt(distance / _sampleDistance));
            Vector2 start = _lastPosition;
            for (int i = 1; i <= steps; i++)
            {
                _distance += distance / steps;
                AddPixel(Vector2.Lerp(start, position, i / (float)steps));
            }
            _lastPosition = position;
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
                float fade = normalizedAge;
                if (IsAugmentTrail)
                {
                    // 빠른 이동으로 개수 제한에 닿아도 꼬리가 진한 채 잘리지 않게 한다.
                    float distanceFade = Mathf.Clamp01(
                        (_distance - pixel.Distance) / (_sampleDistance * (_maximumPixels - 1)));
                    fade = Mathf.Max(fade, distanceFade);
                }
                vertexColor.a = (byte)Mathf.RoundToInt(
                    255f * Mathf.Pow(1f - fade, IsAugmentTrail ? 2f : 1.4f));
                if (_style == CardAugmentVisual.Encore)
                {
                    Vector2 normal = new Vector2(-pixel.Direction.y, pixel.Direction.x);
                    float phase = pixel.Phase + Time.unscaledTime * 8f;
                    Vector2 offset = normal * (Mathf.Sin(phase) * 19f) +
                        pixel.Direction * (Mathf.Cos(phase) * 5f);
                    for (int strand = 0; strand < 2; strand++)
                    {
                        float sign = strand == 0 ? 1f : -1f;
                        Color flame = Color.Lerp(new Color(1f, 0.65f, 0.12f), Color.white,
                            0.5f + 0.5f * Mathf.Cos(phase + strand * Mathf.PI));
                        flame.a = vertexColor.a / 255f;
                        Vector2 center = pixel.Position + offset * sign + Vector2.up * normalizedAge * 14f;
                        AddQuad(vh, center, size * 0.7f, 0f, flame);
                        if (i % 3 == 0)
                            AddQuad(vh, center + normal * sign * normalizedAge * 12f,
                                size * 0.24f, 0f, flame);
                    }
                }
                else if (_style == CardAugmentVisual.StageControl)
                {
                    AddQuad(vh, pixel.Position, size * 0.945f, 0f, vertexColor);
                    Color spark = Color.Lerp(new Color(1f, 0.72f, 0.1f), Color.white, 0.8f);
                    spark.a = vertexColor.a / 255f;
                    Vector2 normal = new Vector2(-pixel.Direction.y, pixel.Direction.x);
                    float zigzag = Mathf.Sin(pixel.Phase * 3f + Time.unscaledTime * 23f);
                    AddQuad(vh, pixel.Position + normal * zigzag * 25f, size * 0.39f, 45f, spark);
                }
                else AddQuad(vh, pixel.Position, size, pixel.Rotation, vertexColor);
            }
        }

        void AddPixel(Vector2 position)
        {
            if (_pixels.Count >= _maximumPixels)
                _pixels.RemoveAt(0);

            _pixels.Add(new Pixel
            {
                Position = position,
                Direction = _direction,
                Phase = _distance * 0.04f,
                Distance = _distance,
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
