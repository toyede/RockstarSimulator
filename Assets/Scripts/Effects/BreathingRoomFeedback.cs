using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>숨 고르기 지급 이벤트를 호흡·빛·카드 도착 반동으로 연결한다.</summary>
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class BreathingRoomFeedback : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer raccoon;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private CardHandUI hand;
        [Header("Breathing motion")]
        [SerializeField, Min(0.1f)] private float breathDuration = 0.6f;
        [SerializeField, Range(0f, 0.3f)] private float inhaleStretch = 0.12f;
        [SerializeField, Range(0f, 0.15f)] private float exhaleSquash = 0.035f;
        [SerializeField] private Vector2 mouthOffset = new Vector2(0.22f, 0.28f);
        [Header("Light and card")]
        [SerializeField, Min(0f)] private float exhaleDelay = 0.26f;
        [SerializeField, Min(0.1f)] private float lightTravelDuration = 0.32f;
        [SerializeField, Min(0.1f)] private float arrivalDuration = 0.5f;
        [SerializeField, Range(0f, 0.3f)] private float arrivalScale = 0.08f;

        CardArrivalFeedback _arrival;
        RectTransform _target;
        BreathLightGraphic _light;
        Vector3 _baseScale, _basePosition;
        Vector2 _lightSource;
        float _age = -1f;
        bool _launched, _arrived;

        void OnEnable()
        {
            EventBus.Subscribe<BreathingRoomCardAdded>(OnCardAdded);
            EventBus.Subscribe<GameStateChanged>(OnStateChanged);
        }
        void OnDisable()
        {
            EventBus.Unsubscribe<BreathingRoomCardAdded>(OnCardAdded);
            EventBus.Unsubscribe<GameStateChanged>(OnStateChanged);
            Clear();
        }
        void OnDestroy() { if (_light != null) Destroy(_light.gameObject); }

        void OnCardAdded(BreathingRoomCardAdded e)
        {
            Clear();
            if (hand == null || hand.IsStowed || !hand.isActiveAndEnabled) return;
            var view = hand.GetView(e.HandIndex);
            if (view == null) return;
            _target = (RectTransform)view.transform;
            bool hasSource = raccoon != null && raccoon.gameObject.activeInHierarchy && stageCamera != null;
            _arrival = view.PlayBreathingArrival();
            if (!hasSource) { _arrival.ArriveBreathing(arrivalDuration, arrivalScale); return; }
            _baseScale = raccoon.transform.localScale;
            _basePosition = raccoon.transform.localPosition;
            if (_light == null) _light = BreathLightGraphic.Create(_target);
            _age = 0f;
            _launched = _arrived = false;
        }

        void LateUpdate()
        {
            if (_age < 0f || (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused)) return;
            if (_arrival == null || !_arrival.IsBreathing || _target == null || hand.IsStowed ||
                raccoon == null || !raccoon.gameObject.activeInHierarchy) { Clear(); return; }
            _age += Time.unscaledDeltaTime;
            ApplyBreath();
            if (_age >= exhaleDelay)
            {
                if (!_launched)
                {
                    Bounds b = raccoon.bounds;
                    var origin = b.center + new Vector3(b.size.x * mouthOffset.x, b.size.y * mouthOffset.y, 0f);
                    _lightSource = stageCamera.WorldToScreenPoint(origin);
                    _launched = true;
                }
                float progress = (_age - exhaleDelay) / Mathf.Max(0.1f, lightTravelDuration);
                if (!_arrived && progress >= 1f)
                {
                    _arrival.ArriveBreathing(arrivalDuration, arrivalScale);
                    _arrived = true;
                }
                float impact = progress < 1f ? -1f :
                    (_age - exhaleDelay - lightTravelDuration) / Mathf.Max(0.1f, arrivalDuration);
                if (_light != null) _light.Show(_lightSource, _target, progress, impact);
            }
        }

        void ApplyBreath()
        {
            RestorePose();
            if (_age >= breathDuration || raccoon.sprite == null) return;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.1f, breathDuration));
            float amount = t < 0.45f ? inhaleStretch * Mathf.SmoothStep(0f, 1f, t / 0.45f) :
                t < 0.85f ? Mathf.Lerp(inhaleStretch, -exhaleSquash, Mathf.SmoothStep(0f, 1f, (t - 0.45f) / 0.4f)) :
                Mathf.Lerp(-exhaleSquash, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.85f) / 0.15f));
            var b = raccoon.sprite.bounds;
            var foot = new Vector3(b.center.x, b.min.y, 0f);
            if (raccoon.flipX) foot.x = -foot.x;
            if (raccoon.flipY) foot.y = -foot.y;
            var pose = raccoon.transform;
            Vector3 fixedFoot = pose.TransformPoint(foot);
            pose.localScale = Vector3.Scale(_baseScale, new Vector3(1f - amount * 0.25f, 1f + amount, 1f));
            pose.position += fixedFoot - pose.TransformPoint(foot);
        }
        void RestorePose()
        {
            if (_age < 0f || raccoon == null) return;
            raccoon.transform.localScale = _baseScale;
            raccoon.transform.localPosition = _basePosition;
        }
        void Clear()
        {
            RestorePose();
            _age = -1f;
            if (_arrival != null) _arrival.Clear();
            _arrival = null;
            _target = null;
            if (_light != null) _light.Clear();
        }
        void OnStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
        }
    }

    /// <summary>하나의 메시로 흰 숨결·카드 윤곽·도착 파동을 재사용한다.</summary>
    sealed class BreathLightGraphic : MaskableGraphic
    {
        Canvas _canvas;
        Vector2 _from, _to;
        float _progress = -1f;
        float _impact = -1f;
        public static BreathLightGraphic Create(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            var go = new GameObject("BreathingRoomLight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var overlay = go.GetComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = canvas.sortingLayerID;
            overlay.sortingOrder = canvas.sortingOrder + 205;
            var graphic = go.AddComponent<BreathLightGraphic>();
            graphic._canvas = canvas;
            graphic.raycastTarget = false; graphic.maskable = false;
            return graphic;
        }
        Vector2 Local(Vector2 screen)
        {
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out var point);
            return point;
        }
        public void Show(Vector2 from, RectTransform target, float progress, float impact)
        {
            _from = Local(from);
            _to = Local(AugmentCardVFX.ScreenCenter(target));
            _progress = progress;
            _impact = impact;
            SetVerticesDirty();
        }
        public void Clear() { _progress = -1f; SetVerticesDirty(); }
        Vector2 Path(float t)
        {
            Vector2 control = (_from + _to) * 0.5f + new Vector2(75f, 50f);
            return (1f - t) * (1f - t) * _from + 2f * t * (1f - t) * control + t * t * _to;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_progress < 0f) return;
            if (_impact >= 0f)
            {
                float t = Mathf.Clamp01(_impact);
                float radius = Mathf.Lerp(10f, 65f, 1f - (1f - t) * (1f - t));
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16f;
                    Add(vh, _to + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 4f, 1f - t);
                }
                return;
            }
            float fade = 1f - Mathf.Clamp01((_progress - 1f) / 0.3f);
            for (int i = 0; i < 18; i++)
            {
                float t = _progress - i * 0.018f;
                if (t < 0f || t > 1f) continue;
                float trail = 1f - i / 18f;
                Vector2 point = Path(t) + Vector2.right * Mathf.Sin(t * 7f + i * 0.7f) * (1f - trail) * 10f;
                Add(vh, point, 3f + 7f * trail, trail * fade * 0.85f);
            }
            // 드로우 강화처럼 작은 카드 윤곽이 입자와 함께 목적지로 이동한다.
            Vector2 head = Path(Mathf.Clamp01(_progress));
            Quad(vh, head + Vector2.left * 7f, new Vector2(2f, 20f), 0.9f);
            Quad(vh, head + Vector2.right * 7f, new Vector2(2f, 20f), 0.9f);
            Quad(vh, head + Vector2.up * 10f, new Vector2(14f, 2f), 0.9f);
            Quad(vh, head + Vector2.down * 10f, new Vector2(14f, 2f), 0.9f);
            // 내쉬는 순간 입가에 작게 퍼졌다 사라지는 도트 숨결.
            for (int i = 0; i < 7; i++)
            {
                float puff = Mathf.Clamp01(_progress * 1.8f);
                float angle = i * Mathf.PI * 2f / 7f;
                Vector2 point = _from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (8f + puff * 22f) + Vector2.up * puff * 15f;
                Add(vh, point, 4f + puff * 3f, (1f - puff) * 0.65f);
            }
        }
        static void Add(VertexHelper vh, Vector2 p, float size, float alpha)
            => Quad(vh, p, Vector2.one * size, alpha);

        static void Quad(VertexHelper vh, Vector2 p, Vector2 size, float alpha)
        {
            int n = vh.currentVertCount;
            Vector2 h = size * 0.5f;
            Color tint = new Color(1f, 1f, 1f, alpha);
            vh.AddVert(p + new Vector2(-h.x, -h.y), tint, Vector2.zero);
            vh.AddVert(p + new Vector2(-h.x, h.y), tint, Vector2.up);
            vh.AddVert(p + h, tint, Vector2.one);
            vh.AddVert(p + new Vector2(h.x, -h.y), tint, Vector2.right);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
