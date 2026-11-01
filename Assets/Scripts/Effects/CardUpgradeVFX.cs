using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>증강으로 획득한 카드의 등급 색으로 그림 둘레를 시계 방향으로 도는 테두리.</summary>
    [DisallowMultipleComponent]
    public sealed class CardUpgradeVFX : MaskableGraphic
    {
        [SerializeField] Color bronzeColor = new Color32(0xCD, 0x7F, 0x32, 0xFF);
        [SerializeField] Color silverColor = new Color32(0xC0, 0xC0, 0xC0, 0xFF);
        [SerializeField] Color goldColor = new Color32(0xFF, 0xD7, 0x00, 0xFF);
        [SerializeField, Min(1f)] float borderWidth = 4f;
        [SerializeField, Range(1f, 3f)] float movingWidthMultiplier = 2f;
        [SerializeField, Min(0.1f)] float rotationPeriod = 2.5f;
        [SerializeField, Range(0.05f, 0.5f)] float trailFraction = 0.24f;
        [SerializeField, Min(0.01f)] float useFlashDuration = 0.1f;
        [SerializeField, Min(0f), Tooltip("카드 그림 기준 모서리 반경(그림 픽셀). 그림의 둥근 모서리에 맞춘다")]
        float cornerRadiusPixels = 5f;
        [SerializeField, Range(1, 8)] int cornerSegments = 4;

        bool _active;
        bool _using;
        float _phaseOffset;
        float _useStarted;
        float _clock;
        Color _tierColor;
        UnityEngine.UI.Image _artwork;
        float _pixelScale = 1f;
        // 테두리 경로: 바깥·안쪽 점과 중심선 누적 길이. 매 프레임 재사용해 할당하지 않는다.
        readonly List<Vector2> _outer = new List<Vector2>(96);
        readonly List<Vector2> _inner = new List<Vector2>(96);
        readonly List<Vector2> _middle = new List<Vector2>(96);
        readonly List<float> _distance = new List<float>(96);
        readonly float[] _cornerMarks = new float[4];

        public static CardUpgradeVFX Create(UnityEngine.UI.Image artwork)
        {
            if (artwork == null) return null;
            var go = new GameObject("GrantedCardVFX", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(artwork.transform, false);
            var effect = go.AddComponent<CardUpgradeVFX>();
            effect.raycastTarget = false;
            effect.enabled = false;
            effect.rectTransform.anchorMin = effect.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            effect.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return effect;
        }

        public void Bind(UnityEngine.UI.Image artwork, bool active, int handIndex, AugmentTier tier)
        {
            _artwork = artwork;
            raycastTarget = false;
            _active = active && artwork != null && artwork.sprite != null;
            _using = false;
            _tierColor = tier == AugmentTier.Gold ? goldColor :
                tier == AugmentTier.Silver ? silverColor : bronzeColor;
            // 풀에서 다시 꺼내도 공통 시간축을 사용해 연출이 재시작되지 않는다.
            _phaseOffset = handIndex * 0.73f;
            _clock = Time.unscaledTime + _phaseOffset;
            color = Color.white;
            enabled = _active;
            if (_active) FitToArtwork();
            SetVerticesDirty();
        }

        public void PlayUseFlash()
        {
            if (!_active || !isActiveAndEnabled) return;
            _using = true;
            _useStarted = Time.unscaledTime;
            SetVerticesDirty();
        }

        public void StopEffect()
        {
            _active = false;
            _using = false;
            enabled = false;
        }

        void Update()
        {
            if (!_active) return;
            if (_artwork == null || _artwork.sprite == null)
            {
                StopEffect();
                return;
            }
            if (_using && Time.unscaledTime - _useStarted >= useFlashDuration)
            {
                StopEffect();
                return;
            }
            _clock = Time.unscaledTime + _phaseOffset;
            SetVerticesDirty();
        }

        void LateUpdate()
        {
            if (_active) FitToArtwork();
        }

        void FitToArtwork()
        {
            Rect display = _artwork.GetPixelAdjustedRect();
            if (_artwork.preserveAspect)
            {
                Vector2 pixels = _artwork.sprite.rect.size;
                float scale = Mathf.Min(display.width / pixels.x, display.height / pixels.y);
                Vector2 size = pixels * scale;
                Vector2 padding = display.size - size;
                display.position += Vector2.Scale(padding, _artwork.rectTransform.pivot);
                display.size = size;
            }

            // 그림 1픽셀이 화면에서 차지하는 크기. 모서리 반경을 그림 기준으로 맞출 때 쓴다.
            _pixelScale = display.width / Mathf.Max(1f, _artwork.sprite.rect.width);

            // preserveAspect로 생기는 여백을 제외하고 실제 그림 외곽에 맞춘다.
            rectTransform.sizeDelta = display.size;
            rectTransform.anchoredPosition = display.center - _artwork.rectTransform.rect.center;
        }

        protected override void OnDisable()
        {
            _active = false;
            _using = false;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_active) return;
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            float width = Mathf.Min(Mathf.Max(1f, borderWidth), Mathf.Min(r.width, r.height) * 0.25f);
            float radius = Mathf.Clamp(cornerRadiusPixels * _pixelScale, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            BuildPath(r, width, radius);
            float perimeter = _distance[_distance.Count - 1];
            if (perimeter <= 0f) return;
            float head = Mathf.Repeat(_clock / Mathf.Max(0.1f, rotationPeriod), 1f) * perimeter;

            for (int i = 0; i < _outer.Count - 1; i++)
            {
                float a = _distance[i];
                float b = _distance[i + 1];
                Color tint = BorderColor((a + b) * 0.5f, perimeter, head);
                // 회전 구간만 안쪽으로 넓히고 꼬리 끝은 기본 테두리 두께로 잇는다.
                Vector2 innerStart = Vector2.LerpUnclamped(_outer[i], _inner[i], MovingWidth(a, perimeter, head));
                Vector2 innerEnd = Vector2.LerpUnclamped(_outer[i + 1], _inner[i + 1], MovingWidth(b, perimeter, head));
                int index = vh.currentVertCount;
                vh.AddVert(_outer[i], tint, Vector2.zero);
                vh.AddVert(_outer[i + 1], tint, Vector2.zero);
                vh.AddVert(innerEnd, tint, Vector2.zero);
                vh.AddVert(innerStart, tint, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
            if (_using) return;

            Spark(vh, PointAt(head), 0.55f + 0.35f * Mathf.Sin(_clock * 11f));
            // 가장 최근에 지난 모서리에서 잠깐 반짝인다.
            float corner = _cornerMarks[3] - perimeter;
            for (int i = 0; i < _cornerMarks.Length; i++)
                if (_cornerMarks[i] <= head) corner = _cornerMarks[i];
            float age = (head - corner) / perimeter * Mathf.Max(0.1f, rotationPeriod);
            if (age < 0.32f)
                Spark(vh, PointAt(Mathf.Repeat(corner, perimeter)), (1f - age / 0.32f) * 0.8f);
        }

        /// <summary>그림 외곽을 따라 시계 방향으로 도는 둥근 사각형 경로를 만든다. 위쪽 변 왼쪽 끝에서 시작한다.</summary>
        void BuildPath(Rect r, float width, float radius)
        {
            _outer.Clear();
            _inner.Clear();
            _middle.Clear();
            _distance.Clear();
            float innerRadius = Mathf.Max(0f, radius - width);
            float inset = width + innerRadius;
            // 바깥 모서리 중심(반경 radius)과 안쪽 모서리 중심(테두리 두께만큼 들어간 사각형, 반경 innerRadius)
            Vector2 oTL = new Vector2(r.xMin + radius, r.yMax - radius), iTL = new Vector2(r.xMin + inset, r.yMax - inset);
            Vector2 oTR = new Vector2(r.xMax - radius, r.yMax - radius), iTR = new Vector2(r.xMax - inset, r.yMax - inset);
            Vector2 oBR = new Vector2(r.xMax - radius, r.yMin + radius), iBR = new Vector2(r.xMax - inset, r.yMin + inset);
            Vector2 oBL = new Vector2(r.xMin + radius, r.yMin + radius), iBL = new Vector2(r.xMin + inset, r.yMin + inset);

            AddPoint(new Vector2(oTL.x, r.yMax), new Vector2(iTL.x, r.yMax - width));
            AddEdge(new Vector2(oTR.x, r.yMax), new Vector2(iTR.x, r.yMax - width));
            _cornerMarks[0] = AddArc(oTR, radius, iTR, innerRadius, 90f, 0f);
            AddEdge(new Vector2(r.xMax, oBR.y), new Vector2(r.xMax - width, iBR.y));
            _cornerMarks[1] = AddArc(oBR, radius, iBR, innerRadius, 0f, -90f);
            AddEdge(new Vector2(oBL.x, r.yMin), new Vector2(iBL.x, r.yMin + width));
            _cornerMarks[2] = AddArc(oBL, radius, iBL, innerRadius, -90f, -180f);
            AddEdge(new Vector2(r.xMin, oTL.y), new Vector2(r.xMin + width, iTL.y));
            _cornerMarks[3] = AddArc(oTL, radius, iTL, innerRadius, 180f, 90f);
        }

        void AddEdge(Vector2 outerEnd, Vector2 innerEnd)
        {
            Vector2 outerStart = _outer[_outer.Count - 1];
            Vector2 innerStart = _inner[_inner.Count - 1];
            // 색 그라데이션이 매끄럽도록 4단위마다 나눈다.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(outerStart, outerEnd) / 4f));
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                AddPoint(Vector2.Lerp(outerStart, outerEnd, t), Vector2.Lerp(innerStart, innerEnd, t));
            }
        }

        /// <summary>모서리 호를 추가하고 호 가운데의 경로 길이를 돌려준다.</summary>
        float AddArc(Vector2 outerCenter, float outerRadius, Vector2 innerCenter, float innerRadius,
            float fromDegrees, float toDegrees)
        {
            float before = _distance[_distance.Count - 1];
            int steps = Mathf.Max(1, cornerSegments);
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, (float)i / steps) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddPoint(outerCenter + direction * outerRadius, innerCenter + direction * innerRadius);
            }
            return (before + _distance[_distance.Count - 1]) * 0.5f;
        }

        void AddPoint(Vector2 outer, Vector2 inner)
        {
            Vector2 middle = (outer + inner) * 0.5f;
            float distance = _middle.Count == 0
                ? 0f
                : _distance[_distance.Count - 1] + Vector2.Distance(_middle[_middle.Count - 1], middle);
            _outer.Add(outer);
            _inner.Add(inner);
            _middle.Add(middle);
            _distance.Add(distance);
        }

        Vector2 PointAt(float distance)
        {
            for (int i = 1; i < _distance.Count; i++)
            {
                if (_distance[i] < distance) continue;
                float length = _distance[i] - _distance[i - 1];
                float t = length > 0f ? (distance - _distance[i - 1]) / length : 0f;
                return Vector2.Lerp(_middle[i - 1], _middle[i], t);
            }
            return _middle[_middle.Count - 1];
        }

        float MovingWidth(float distance, float perimeter, float head)
        {
            if (_using) return 1f;
            float behind = Mathf.Repeat(head - distance, perimeter) / perimeter;
            float remaining = Mathf.Clamp01(1f - behind / Mathf.Max(0.01f, trailFraction));
            return Mathf.Lerp(1f, movingWidthMultiplier, Mathf.Clamp01(remaining * 4f));
        }

        Color BorderColor(float distance, float perimeter, float head)
        {
            if (_using)
                return WithAlpha(Color.Lerp(_tierColor, Color.white, 0.35f),
                    1f - Mathf.Clamp01((Time.unscaledTime - _useStarted) / Mathf.Max(0.01f, useFlashDuration)));
            float behind = Mathf.Repeat(head - distance, perimeter) / perimeter;
            float strength = Mathf.Pow(Mathf.Clamp01(1f - behind / Mathf.Max(0.01f, trailFraction)), 1.5f);
            Color baseColor = _tierColor * 0.55f;
            Color highlight = Color.Lerp(_tierColor, Color.white, 0.3f);
            return WithAlpha(Color.Lerp(baseColor, highlight, strength), Mathf.Lerp(0.85f, 1f, strength));
        }

        void Spark(VertexHelper vh, Vector2 point, float strength)
        {
            float size = Mathf.Round(Mathf.Lerp(2f, 5f, strength));
            Color tint = WithAlpha(Color.Lerp(_tierColor, Color.white, 0.7f), strength);
            Quad(vh, new Rect(point.x - 1f, point.y - size, 2f, size * 2f), tint);
            Quad(vh, new Rect(point.x - size, point.y - 1f, size * 2f, 2f), tint);
        }

        static Color WithAlpha(Color value, float alpha) { value.a = alpha; return value; }

        static void Quad(VertexHelper vh, Rect r, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), tint, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), tint, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), tint, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
