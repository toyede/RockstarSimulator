using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    public enum CardAugmentVisual { None, Encore, StageControl, Draw, Reroll }

    /// <summary>증강 카드의 이동 픽셀·카드 조각·링·플러스를 하나의 재사용 메시로 그린다.</summary>
    public sealed class AugmentCardVFX : MaskableGraphic
    {
        enum Shape { Pixel, Card, Ring, SquareRing, Plus, Swirl, HandWave }

        struct Mote
        {
            public Vector2 From, To;
            public Color Tint;
            public float Start, Lifetime, Size, Curve;
            public Shape Shape;
        }

        readonly List<Mote> _motes = new List<Mote>(256);
        Canvas _rootCanvas;
        public static readonly Color Gold = new Color32(255, 217, 83, 255);
        public int LiveMoteCount => _motes.Count;

        public static CardAugmentVisual Resolve(CardDefinition card, CardUpgradeModifiers upgrade)
        {
            if (card == null) return CardAugmentVisual.None;
            if (card.UtilityEffect == UtilityCardEffect.ExtendPerformanceTime)
                return CardAugmentVisual.Encore;
            if (IsStageControl(card.Id)) return CardAugmentVisual.StageControl;
            if (upgrade.ExtraCardsAfterUse <= 0) return CardAugmentVisual.None;
            if (card.UtilityEffect == UtilityCardEffect.Draw) return CardAugmentVisual.Draw;
            if (card.UtilityEffect == UtilityCardEffect.Reroll) return CardAugmentVisual.Reroll;
            return CardAugmentVisual.None;
        }

        public static bool IsStageControl(string id) =>
            id != null && id.StartsWith("stage_control_", StringComparison.Ordinal);

        public static AugmentCardVFX Create(Transform owner)
        {
            Canvas canvas = owner == null ? null : owner.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            var go = new GameObject("AugmentCardPixels", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas overlay = go.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = canvas.sortingLayerID;
            overlay.sortingOrder = canvas.sortingOrder + 205;
            var graphic = go.AddComponent<AugmentCardVFX>();
            graphic._rootCanvas = canvas;
            graphic.raycastTarget = false;
            graphic.maskable = false;
            return graphic;
        }

        public static Vector2 ScreenCenter(RectTransform source)
        {
            Canvas canvas = source.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(camera, source.TransformPoint(source.rect.center));
        }

        Vector2 Local(Vector2 screen)
        {
            Camera camera = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out var p);
            return p;
        }

        void Add(Vector2 from, Vector2 to, Shape shape, float size, float lifetime,
            float delay = 0f, float curve = 0f, bool white = false)
        {
            _motes.Add(new Mote { From = from, To = to, Shape = shape, Size = size,
                Lifetime = lifetime, Start = Time.unscaledTime + delay, Curve = curve,
                Tint = white ? Color.white : Gold });
            SetVerticesDirty();
        }

        public void FlyToTimer(Vector2 source, RectTransform timer)
        {
            if (timer == null) return;
            Vector2 from = Local(source), to = Local(ScreenCenter(timer));
            for (int i = 0; i < 24; i++)
                Add(from + new Vector2(UnityEngine.Random.Range(-55f, 55f), UnityEngine.Random.Range(-75f, 75f)),
                    to, Shape.Pixel, UnityEngine.Random.Range(4f, 9f), 0.5f,
                    i * 0.005f, (i % 2 == 0 ? 1f : -1f) * (25f + i * 2f), i % 3 == 0);
        }

        public void SplitToCard(Vector2 source, RectTransform target, bool bonus, int index)
        {
            if (target == null) return;
            Vector2 from = Local(source), to = Local(ScreenCenter(target));
            float delay = 0.06f + Mathf.Min(index, 8) * 0.015f;
            for (int i = 0; i < 5; i++)
                Add(from + new Vector2((i - 2) * 12f, i % 2 * 18f), to,
                    i == 0 ? Shape.Card : Shape.Pixel, i == 0 ? 17f : 6f,
                    0.42f, delay + i * 0.018f, (to.x - from.x) * 0.18f, i % 2 == 0);
            if (bonus) RingAt(to, 62f, 0.5f, delay + 0.42f, false);
        }

        public void PlayCollapse(Vector2 source, CardAugmentVisual style)
        {
            Vector2 origin = Local(source);
            if (style == CardAugmentVisual.Reroll)
            {
                for (int i = 0; i < 22; i++)
                {
                    float angle = i * Mathf.PI * 2f / 22f;
                    Add(origin + new Vector2(Mathf.Cos(angle) * 80f, Mathf.Sin(angle) * 110f),
                        origin, Shape.Swirl, 6f, 0.38f, 0f, angle, i % 3 == 0);
                }
            }
            else if (style == CardAugmentVisual.StageControl)
            {
                RingAt(origin, 145f, 0.36f, 0.16f, true);
                for (int i = 0; i < 18; i++)
                {
                    Vector2 ray = new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f));
                    Add(origin, origin + ray * 150f, Shape.Pixel, 9f, 0.35f, 0.16f, 0f, i % 2 == 0);
                }
            }
        }

        public void SweepHand(RectTransform hand)
        {
            if (hand == null) return;
            Vector2 center = Local(ScreenCenter(hand));
            Add(center, center, Shape.HandWave, Mathf.Clamp(hand.rect.width * 0.55f, 80f, 600f), 0.55f, 0.08f);
        }

        public void FloatPluses(RectTransform timer, int count, float delay)
        {
            if (timer == null) return;
            Vector2 center = Local(ScreenCenter(timer));
            // 문자 대신 메시로 +를 그려 폰트/로컬라이제이션 참조 없이 정확한 개수를 표시한다.
            for (int i = 0; i < count; i++)
            {
                float offset = (Mathf.Repeat(i * 0.618034f, 1f) - 0.5f) * 100f;
                Vector2 start = center + new Vector2(offset, 3f);
                Vector2 end = start + new Vector2(Mathf.Sin(i * 2.4f) * 18f, 65f + i % 3 * 14f);
                end.y = Mathf.Min(end.y, rectTransform.rect.yMax - 14f);
                Add(start, end,
                    Shape.Plus, 13f + i % 3 * 2f, 0.95f,
                    delay + i * 0.055f, 5f, i % 2 == 0);
            }
            for (int i = 0; i < 8; i++)
                Add(center + new Vector2((i - 3.5f) * 15f, 0f),
                    center + new Vector2((i - 3.5f) * 20f, 28f),
                    Shape.Pixel, 4f, 0.45f, delay, 0f, i % 2 == 0);
        }

        void RingAt(Vector2 center, float radius, float duration, float delay, bool square)
        {
            Add(center, center, square ? Shape.SquareRing : Shape.Ring,
                Mathf.Clamp(radius, 20f, 600f), duration, delay);
        }

        public void Clear()
        {
            _motes.Clear();
            SetVerticesDirty();
        }

        protected override void OnDisable() { Clear(); base.OnDisable(); }

        void Update()
        {
            if (_motes.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = _motes.Count - 1; i >= 0; i--)
                if (now >= _motes[i].Start + _motes[i].Lifetime) _motes.RemoveAt(i);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float now = Time.unscaledTime;
            for (int i = 0; i < _motes.Count; i++)
            {
                Mote m = _motes[i];
                if (now < m.Start) continue;
                float t = Mathf.Clamp01((now - m.Start) / m.Lifetime);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                Color tint = m.Tint;
                tint.a *= 1f - t * t;
                Vector2 point = Vector2.Lerp(m.From, m.To, ease) +
                    Vector2.right * (Mathf.Sin(t * Mathf.PI) * m.Curve);
                if (m.Shape == Shape.Swirl)
                {
                    Vector2 delta = m.From - m.To;
                    float angle = m.Curve + t * Mathf.PI * 2.5f;
                    point = m.To + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * delta.magnitude * (1f - ease);
                }
                float size = m.Size * (1f - t * 0.45f);
                if (m.Shape == Shape.Ring || m.Shape == Shape.SquareRing || m.Shape == Shape.HandWave)
                {
                    float radius = m.Size * Mathf.Lerp(0.15f, 1f, ease);
                    for (int j = 0; j < 32; j++)
                    {
                        if (m.Shape == Shape.HandWave && j % 8 >= 5) continue;
                        float a = j * Mathf.PI / 16f + (m.Shape == Shape.HandWave ? t * 5f : 0f);
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        if (m.Shape == Shape.HandWave) d.y *= 0.28f;
                        if (m.Shape == Shape.SquareRing) d /= Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                        Quad(vh, m.From + d * radius, new Vector2(5f, 5f), tint);
                    }
                }
                else if (m.Shape == Shape.Plus)
                {
                    Quad(vh, point, new Vector2(size, size / 3f), tint);
                    Quad(vh, point, new Vector2(size / 3f, size), tint);
                }
                else if (m.Shape == Shape.Card)
                {
                    Quad(vh, point + Vector2.left * size * 0.5f, new Vector2(3f, size * 1.4f), tint);
                    Quad(vh, point + Vector2.right * size * 0.5f, new Vector2(3f, size * 1.4f), tint);
                    Quad(vh, point + Vector2.up * size * 0.7f, new Vector2(size, 3f), tint);
                    Quad(vh, point + Vector2.down * size * 0.7f, new Vector2(size, 3f), tint);
                }
                else Quad(vh, point, Vector2.one * size, tint);
            }
        }

        static void Quad(VertexHelper vh, Vector2 center, Vector2 size, Color tint)
        {
            int n = vh.currentVertCount;
            Vector2 half = size * 0.5f;
            vh.AddVert(center + new Vector2(-half.x, -half.y), tint, Vector2.zero);
            vh.AddVert(center + new Vector2(-half.x, half.y), tint, Vector2.up);
            vh.AddVert(center + half, tint, Vector2.one);
            vh.AddVert(center + new Vector2(half.x, -half.y), tint, Vector2.right);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
