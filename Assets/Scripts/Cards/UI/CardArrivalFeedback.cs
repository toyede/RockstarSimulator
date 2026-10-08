using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>카드 아트만 짧게 확대. 입력/레이아웃 루트와 증강 판정은 건드리지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CardArrivalFeedback : MonoBehaviour
    {
        RectTransform _art;
        Vector3 _rest;
        ArrivalFrame _frame;
        float _remaining;
        public void Play(Image art, Color tint)
        {
            Clear();
            if (art == null) return;
            _art = art.rectTransform; _rest = _art.localScale;
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
            _frame.color = tint; _frame.enabled = true;
            _remaining = 0.3f;
        }
        void LateUpdate()
        {
            if (_remaining <= 0 || _art == null) return;
            if (GameJamKit.GameManager.HasInstance && GameJamKit.GameManager.Instance.State == GameJamKit.GameState.Paused) return;
            _remaining = Mathf.Max(0, _remaining - Time.deltaTime);
            float t = 1 - _remaining / 0.3f;
            _art.localScale = _rest * (1 + 0.08f * Mathf.Sin(t * Mathf.PI));
            if (_frame != null) { var color = _frame.color; color.a = (1 - t) * 0.8f; _frame.color = color; }
            if (_remaining <= 0) Clear();
        }
        public void Clear()
        {
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
