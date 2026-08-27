using UnityEngine;
using UnityEngine.EventSystems;

namespace ContextStage
{
    /// <summary>
    /// Figma의 즉시 전환 호버 상태를 RectTransform 위치 변화로 재현한다.
    /// </summary>
    public sealed class AugmentHoverMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] RectTransform primaryTarget;
        [SerializeField] Vector2 primaryHoverOffset;
        [SerializeField] RectTransform secondaryTarget;
        [SerializeField] Vector2 secondaryHoverOffset;

        Vector2 _primaryBasePosition;
        Vector2 _secondaryBasePosition;
        bool _interactionEnabled = true;
        bool _hovered;

        void Awake()
        {
            CaptureBasePositions();
        }

        void OnDisable()
        {
            ResetState();
        }

        public void Configure(
            RectTransform primary,
            Vector2 primaryOffset,
            RectTransform secondary = null,
            Vector2 secondaryOffset = default)
        {
            primaryTarget = primary;
            primaryHoverOffset = primaryOffset;
            secondaryTarget = secondary;
            secondaryHoverOffset = secondaryOffset;
            CaptureBasePositions();
            ApplyState(false);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            if (!enabled) ResetState();
        }

        public void ResetState()
        {
            _hovered = false;
            ApplyState(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactionEnabled) return;
            _hovered = true;
            ApplyState(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyState(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_interactionEnabled) ApplyState(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ApplyState(_interactionEnabled && _hovered);
        }

        void CaptureBasePositions()
        {
            if (primaryTarget != null)
                _primaryBasePosition = primaryTarget.anchoredPosition;
            if (secondaryTarget != null)
                _secondaryBasePosition = secondaryTarget.anchoredPosition;
        }

        void ApplyState(bool hovered)
        {
            if (primaryTarget != null)
                primaryTarget.anchoredPosition =
                    _primaryBasePosition + (hovered ? primaryHoverOffset : Vector2.zero);
            if (secondaryTarget != null)
                secondaryTarget.anchoredPosition =
                    _secondaryBasePosition + (hovered ? secondaryHoverOffset : Vector2.zero);
        }
    }
}
