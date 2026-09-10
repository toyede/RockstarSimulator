using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>증강 등급 색으로 카드 둘레를 시계 방향으로 도는 강화 테두리.</summary>
    [DisallowMultipleComponent]
    public sealed class CardUpgradeVFX : MaskableGraphic
    {
        [SerializeField] Color bronzeColor = new Color32(0xCD, 0x80, 0x42, 0xFF);
        [SerializeField] Color silverColor = new Color32(0xC4, 0xCF, 0xDE, 0xFF);
        [SerializeField] Color goldColor = new Color32(0xFF, 0xC5, 0x38, 0xFF);
        [SerializeField, Min(1f)] float borderWidth = 4f;
        [SerializeField, Range(1f, 3f)] float movingWidthMultiplier = 2f;
        [SerializeField, Min(0.1f)] float rotationPeriod = 2.5f;
        [SerializeField, Range(0.05f, 0.5f)] float trailFraction = 0.24f;
        [SerializeField, Min(0.01f)] float useFlashDuration = 0.1f;

        bool _active;
        bool _using;
        float _phaseOffset;
        float _useStarted;
        float _clock;
        Color _tierColor;

        public void Bind(bool active, int handIndex, AugmentTier tier)
        {
            raycastTarget = false;
            _active = active;
            _using = false;
            _tierColor = tier == AugmentTier.Gold ? goldColor :
                tier == AugmentTier.Silver ? silverColor : bronzeColor;
            // 풀에서 다시 꺼내도 공통 시간축을 사용해 연출이 재시작되지 않는다.
            _phaseOffset = handIndex * 0.73f;
            _clock = Time.unscaledTime + _phaseOffset;
            color = Color.white;
            enabled = active;
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
            if (_using && Time.unscaledTime - _useStarted >= useFlashDuration)
            {
                StopEffect();
                return;
            }
            _clock = Time.unscaledTime + _phaseOffset;
            SetVerticesDirty();
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
            float horizontal = r.width - width;
            float vertical = r.height - width;
            float perimeter = 2f * (horizontal + vertical);
            float head = Mathf.Repeat(_clock / Mathf.Max(0.1f, rotationPeriod), 1f) * perimeter;
            Vector2 tl = new Vector2(r.xMin, r.yMax);
            Vector2 tr = new Vector2(r.xMax, r.yMax);
            Vector2 br = new Vector2(r.xMax, r.yMin);
            Vector2 bl = new Vector2(r.xMin, r.yMin);
            Vector2 itl = tl + new Vector2(width, -width);
            Vector2 itr = tr + new Vector2(-width, -width);
            Vector2 ibr = br + new Vector2(-width, width);
            Vector2 ibl = bl + new Vector2(width, width);
            // 안쪽·바깥쪽 모서리를 함께 보간해 모서리에 빈틈이나 중첩이 생기지 않는다.
            Side(vh, tl, tr, itl, itr, horizontal, 0f, perimeter, head);
            Side(vh, tr, br, itr, ibr, vertical, horizontal, perimeter, head);
            Side(vh, br, bl, ibr, ibl, horizontal, horizontal + vertical, perimeter, head);
            Side(vh, bl, tl, ibl, itl, vertical, 2f * horizontal + vertical, perimeter, head);
            if (_using) return;

            Rect path = new Rect(r.xMin + width * 0.5f, r.yMin + width * 0.5f, horizontal, vertical);
            Spark(vh, PointOnBorder(path, head), 0.55f + 0.35f * Mathf.Sin(_clock * 11f));
            float corner = head >= 2f * horizontal + vertical ? 2f * horizontal + vertical :
                head >= horizontal + vertical ? horizontal + vertical : head >= horizontal ? horizontal : 0f;
            float age = (head - corner) / perimeter * Mathf.Max(0.1f, rotationPeriod);
            if (age < 0.32f)
                Spark(vh, PointOnBorder(path, corner), (1f - age / 0.32f) * 0.8f);
        }

        void Side(VertexHelper vh, Vector2 outerA, Vector2 outerB, Vector2 innerA,
            Vector2 innerB, float length, float start, float perimeter, float head)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 4f));
            for (int i = 0; i < steps; i++)
            {
                float a = (float)i / steps;
                float b = (float)(i + 1) / steps;
                Color tint = BorderColor(start + (a + b) * 0.5f * length, perimeter, head);
                Vector2 outerStart = Vector2.Lerp(outerA, outerB, a);
                Vector2 outerEnd = Vector2.Lerp(outerA, outerB, b);
                Vector2 innerStart = Vector2.Lerp(innerA, innerB, a);
                Vector2 innerEnd = Vector2.Lerp(innerA, innerB, b);
                // 회전 구간만 안쪽으로 넓히고 꼬리 끝은 기본 테두리 두께로 잇는다.
                innerStart = Vector2.LerpUnclamped(outerStart, innerStart,
                    MovingWidth(start + a * length, perimeter, head));
                innerEnd = Vector2.LerpUnclamped(outerEnd, innerEnd,
                    MovingWidth(start + b * length, perimeter, head));
                int index = vh.currentVertCount;
                vh.AddVert(outerStart, tint, Vector2.zero);
                vh.AddVert(outerEnd, tint, Vector2.zero);
                vh.AddVert(innerEnd, tint, Vector2.zero);
                vh.AddVert(innerStart, tint, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
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

        static Vector2 PointOnBorder(Rect r, float distance)
        {
            if (distance < r.width) return new Vector2(r.xMin + distance, r.yMax);
            distance -= r.width;
            if (distance < r.height) return new Vector2(r.xMax, r.yMax - distance);
            distance -= r.height;
            if (distance < r.width) return new Vector2(r.xMax - distance, r.yMin);
            return new Vector2(r.xMin, r.yMin + distance - r.width);
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
