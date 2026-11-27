using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>Scales the entire title button, including its label, without replacing its sprite swap.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class TitleMenuButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        const float HoverScale = 1.045f;
        const float PressedScale = 0.96f;
        const float Speed = 1.8f;

        Button _button;
        Vector3 _baseScale;
        float _scale = 1f;
        bool _hovered;
        bool _pressed;

        void Awake()
        {
            _button = GetComponent<Button>();
            _baseScale = transform.localScale;
        }

        void Update()
        {
            float target = 1f;
            if (_button.IsInteractable())
                target = _pressed ? PressedScale : _hovered ? HoverScale : 1f;
            if (_scale == target) return;
            _scale = Mathf.MoveTowards(_scale, target, Speed * Time.unscaledDeltaTime);
            transform.localScale = _baseScale * _scale;
        }

        void OnDisable()
        {
            _hovered = _pressed = false;
            _scale = 1f;
            transform.localScale = _baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _button.IsInteractable())
                _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _pressed = false;
        }
    }
}
