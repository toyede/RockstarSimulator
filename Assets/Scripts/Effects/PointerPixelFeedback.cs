using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>Optional, non-raycasting pointer accent. Disabled by default for comparison testing.</summary>
    [DisallowMultipleComponent]
    public sealed class PointerPixelFeedback : MonoBehaviour
    {
        [SerializeField] bool showPointerPixels = false;
        Canvas _canvas;
        RectTransform _anchor;
        UIPixelBurstEmitter _emitter;
        Vector2 _previous;
        bool _hasPrevious;
        float _nextEmission;

        void OnEnable()
        {
            _canvas = GetComponentInParent<Canvas>();
            _hasPrevious = false;
            if (_emitter != null) _emitter.enabled = true;
        }

        void OnDisable()
        {
            _hasPrevious = false;
            if (_emitter != null) _emitter.enabled = false;
        }

        void OnDestroy()
        {
            if (_anchor != null) Destroy(_anchor.gameObject);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!showPointerPixels || _canvas == null) { _hasPrevious = false; return; }
            Vector2 screen;
            bool touch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
            if (touch)
            {
                if (!Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return;
                screen = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else
            {
                if (Mouse.current == null) return;
                screen = Mouse.current.position.ReadValue();
            }
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
            { _hasPrevious = false; return; }
            float travel = _hasPrevious ? Vector2.Distance(screen, _previous) : 0f;
            _previous = screen;
            bool hadPrevious = _hasPrevious;
            _hasPrevious = true;
            // No long streak when returning from outside or teleporting across the display.
            if (!touch && (!hadPrevious || travel < 8f || travel > 180f || Time.unscaledTime < _nextEmission)) return;
            if (_anchor == null)
            {
                var go = new GameObject("PointerPixelAnchor", typeof(RectTransform));
                go.transform.SetParent(_canvas.transform, false);
                _anchor = go.GetComponent<RectTransform>();
                _anchor.anchorMin = _anchor.anchorMax = Vector2.one * 0.5f;
                _anchor.sizeDelta = Vector2.one;
                _emitter = go.AddComponent<UIPixelBurstEmitter>();
            }
            var root = _canvas.transform as RectTransform;
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, camera, out Vector2 local)) return;
            _anchor.anchoredPosition = local;
            _emitter.EmitBurst(_anchor, new Color32(0xC7,0xDC,0xD0,110), touch ? 4 : 1,
                8f, 24f, 2f, 4f, 0.13f, 0.22f);
            _nextEmission = Time.unscaledTime + 0.06f;
#endif
        }
    }
}
