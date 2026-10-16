using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ContextStage
{
    /// <summary>카드 아트만 짧게 확대. 입력/레이아웃 루트와 증강 판정은 건드리지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CardArrivalFeedback : MonoBehaviour, IBeginDragHandler
    {
        RectTransform _art;
        Vector3 _rest;
        ArrivalFrame _frame;
        float _remaining;
        float _pulseDuration = 0.3f, _pulseScale = 0.08f;
        public bool IsBreathing { get; private set; }

        public void PlayBreathing(Image art)
        {
            Clear();
            if (art == null) return;
            _art = art.rectTransform;
            _rest = _art.localScale;
            IsBreathing = true;
        }

        public void ArriveBreathing(float duration, float scale)
        {
            if (!IsBreathing || _art == null) return;
            EnsureFrame();
            _frame.color = Color.white;
            _frame.enabled = true;
            _pulseDuration = Mathf.Max(0.1f, duration);
            _pulseScale = Mathf.Max(0f, scale);
            _remaining = _pulseDuration;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Clear();
        }
        public void Play(Image art, Color tint)
        {
            Clear();
            if (art == null) return;
            _art = art.rectTransform; _rest = _art.localScale;
            EnsureFrame();
            _frame.color = tint; _frame.enabled = true;
            _pulseDuration = 0.3f;
            _pulseScale = 0.08f;
            _remaining = _pulseDuration;
        }

        void EnsureFrame()
        {
            if (_frame == null)
            {
                var go = new GameObject("ArrivalBorder", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(_art, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                _frame = go.AddComponent<ArrivalFrame>();
                _frame.raycastTarget = false;
            }
        }
        void LateUpdate()
        {
            if (_remaining <= 0 || _art == null) return;
            if (GameJamKit.GameManager.HasInstance && GameJamKit.GameManager.Instance.State == GameJamKit.GameState.Paused) return;
            _remaining = Mathf.Max(0, _remaining - (IsBreathing ? Time.unscaledDeltaTime : Time.deltaTime));
            float t = 1 - _remaining / _pulseDuration;
            _art.localScale = _rest * (1 + _pulseScale * Mathf.Sin(t * Mathf.PI));
            if (_frame != null) { var color = _frame.color; color.a = (1 - t) * 0.8f; _frame.color = color; }
            if (_remaining <= 0) Clear();
        }
        public void Clear()
        {
            IsBreathing = false;
            _remaining = 0;
            if (_art != null) _art.localScale = _rest;
            if (_frame != null) _frame.enabled = false;
        }
        void OnDisable() => Clear();

    }

    // 재질·텍스처 추가 없이 도트 두께의 테두리만 그린다.
    sealed class ArrivalFrame : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Add(vh, new Rect(r.xMin, r.yMin, r.width, 3));
            Add(vh, new Rect(r.xMin, r.yMax - 3, r.width, 3));
            Add(vh, new Rect(r.xMin, r.yMin + 3, 3, r.height - 6));
            Add(vh, new Rect(r.xMax - 3, r.yMin + 3, 3, r.height - 6));
        }
        void Add(VertexHelper vh, Rect r)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
