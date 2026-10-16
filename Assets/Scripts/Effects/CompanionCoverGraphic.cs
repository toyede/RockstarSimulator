using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>하나의 재사용 메시로 동료의 별빛 궤적과 콤보 보호 테두리를 그린다.</summary>
    public sealed class CompanionCoverGraphic : MaskableGraphic
    {
        Canvas _canvas;
        Text _targetText;
        Vector2 _from, _secondFrom, _to;
        Vector2 _halfTarget;
        float _flight, _impact = -1f;
        bool _visible, _twoSources;
        Color _tint;
        float _borderGlow;
        const float MergeAt = 0.65f;

        public static CompanionCoverGraphic Create(RectTransform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            var go = new GameObject("CompanionCoverPixels", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var overlay = go.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = canvas.sortingLayerID;
            overlay.sortingOrder = canvas.sortingOrder + 205;
            var graphic = go.AddComponent<CompanionCoverGraphic>();
            graphic._canvas = canvas;
            graphic._targetText = target.GetComponent<Text>();
            graphic.raycastTarget = false;
            graphic.maskable = false;
            return graphic;
        }

        Vector2 Local(Vector2 screen)
        {
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out var p);
            return p;
        }

        public void Show(Vector2 from, Vector2 secondFrom, bool twoSources, RectTransform target,
            float flight, float impact, Color tint, float borderGlow)
        {
            _from = Local(from);
            _secondFrom = Local(secondFrom);
            _twoSources = twoSources;
            TargetBounds(target);
            _flight = flight;
            _impact = impact;
            _tint = tint;
            _borderGlow = borderGlow;
            _visible = true;
            SetVerticesDirty();
        }

        void TargetBounds(RectTransform target)
        {
            _to = Local(AugmentCardVFX.ScreenCenter(target));
            _halfTarget = new Vector2(16f, 16f);
            if (_targetText == null) return;
            // 오른쪽 정렬된 숫자는 RectTransform 중앙과 실제 글자 중앙이 다르다.
            var vertices = _targetText.cachedTextGenerator.verts;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool found = false;
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                Vector2 a = vertices[i].position, b = vertices[i + 2].position;
                if (Mathf.Abs(a.x - b.x) < 0.01f || Mathf.Abs(a.y - b.y) < 0.01f) continue;
                min = Vector2.Min(min, Vector2.Min(a, b));
                max = Vector2.Max(max, Vector2.Max(a, b));
                found = true;
            }
            if (!found) return;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            float units = _targetText.pixelsPerUnit;
            Vector2 aLocal = Local(RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(min / units)));
            Vector2 bLocal = Local(RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(max / units)));
            _to = (aLocal + bLocal) * 0.5f;
            _halfTarget = new Vector2(Mathf.Abs(bLocal.x - aLocal.x), Mathf.Abs(bLocal.y - aLocal.y)) * 0.5f;
        }

        public void Clear() { _visible = false; SetVerticesDirty(); }

        Vector2 MergePoint => _to + new Vector2(-95f, -110f);

        Vector2 Path(Vector2 from, float t)
        {
            if (t >= MergeAt) return Vector2.Lerp(MergePoint, _to, (t - MergeAt) / (1f - MergeAt));
            t /= MergeAt;
            Vector2 control = (from + MergePoint) * 0.5f + Vector2.up * 140f;
            return (1f - t) * (1f - t) * from + 2f * (1f - t) * t * control + t * t * MergePoint;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_visible) return;
            Border(vh);
            if (_flight < 0f) return;
            if (_impact < 0f)
            {
                Trail(vh, _from, false);
                if (_twoSources) Trail(vh, _secondFrom, true);
                if (_flight >= MergeAt && _flight < MergeAt + 0.16f)
                    Star(vh, MergePoint, 40f * (1f - (_flight - MergeAt) / 0.16f), Color.white);
                return;
            }
            float fade = 1f - Mathf.Clamp01(_impact);
            Color cyan = _tint; cyan.a *= fade;
            Color white = Color.white; white.a = fade;
            float expand = 1f - Mathf.Pow(1f - Mathf.Clamp01(_impact), 3f);
            Vector2 radius = _halfTarget + Vector2.one * Mathf.Lerp(10f, 68f, expand);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12f;
                Vector2 ray = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Quad(vh, _to + Vector2.Scale(ray, radius),
                    Vector2.one * (i % 2 == 0 ? 7f : 4f) * Mathf.Lerp(1f, 0.4f, _impact), i % 2 == 0 ? cyan : white);
                if (i % 2 == 0)
                    Quad(vh, _to + Vector2.Scale(ray, radius + Vector2.one * 10f), Vector2.one * 3f, cyan);
            }
            // 숫자 중앙을 가리지 않는 짧은 보호 테두리.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 p = _to + Vector2.right * (side * (_halfTarget.x + 9f));
                float height = _halfTarget.y + 7f;
                Quad(vh, p, new Vector2(4f, height * 2f), cyan);
                Quad(vh, p + new Vector2(-side * 4f, height), new Vector2(10f, 3f), white);
                Quad(vh, p + new Vector2(-side * 4f, -height), new Vector2(10f, 3f), white);
            }
        }

        void Trail(VertexHelper vh, Vector2 from, bool second)
        {
            for (int i = 11; i >= 0; i--)
            {
                float t = _flight - i * 0.018f;
                if (t < 0f || (second && t >= MergeAt)) continue;
                Color tint = _tint;
                tint.a *= 1f - i / 12f;
                Quad(vh, Path(from, t), Vector2.one * Mathf.Lerp(10f, 3f, i / 11f), tint);
            }
            if (!second || _flight < MergeAt)
            {
                Vector2 head = Path(from, _flight);
                Star(vh, head, _flight >= MergeAt ? 32f : 25f, _tint);
                Quad(vh, head, Vector2.one * 7f, Color.white);
            }
            if (_flight < 0.25f)
                Star(vh, from, 42f * (1f - _flight / 0.25f), Color.white);
        }

        void Border(VertexHelper vh)
        {
            Rect r = rectTransform.rect;
            const int bands = 12;
            const float width = 3f;
            for (int i = 0; i < bands; i++)
            {
                float inset = i * width;
                Color tint = _tint;
                tint.a *= _borderGlow * Mathf.Pow(1f - i / (float)bands, 2f) * (i == 0 ? 1f : 0.55f);
                Quad(vh, new Vector2(r.xMin + inset + width * 0.5f, r.center.y), new Vector2(width, r.height - inset * 2f), tint);
                Quad(vh, new Vector2(r.xMax - inset - width * 0.5f, r.center.y), new Vector2(width, r.height - inset * 2f), tint);
                Quad(vh, new Vector2(r.center.x, r.yMin + inset + width * 0.5f), new Vector2(r.width - inset * 2f, width), tint);
                Quad(vh, new Vector2(r.center.x, r.yMax - inset - width * 0.5f), new Vector2(r.width - inset * 2f, width), tint);
            }
        }

        static void Star(VertexHelper vh, Vector2 p, float size, Color tint)
        {
            Quad(vh, p, new Vector2(size, size * 0.25f), tint);
            Quad(vh, p, new Vector2(size * 0.25f, size), tint);
        }

        static void Quad(VertexHelper vh, Vector2 p, Vector2 size, Color tint)
        {
            int n = vh.currentVertCount;
            Vector2 half = size * 0.5f;
            vh.AddVert(p + new Vector2(-half.x, -half.y), tint, Vector2.zero);
            vh.AddVert(p + new Vector2(-half.x, half.y), tint, Vector2.up);
            vh.AddVert(p + half, tint, Vector2.one);
            vh.AddVert(p + new Vector2(half.x, -half.y), tint, Vector2.right);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
