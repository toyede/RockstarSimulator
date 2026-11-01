using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>배경 사각형 대신 입장 도장의 잉크·밑줄로 클릭과 키보드 포커스를 알린다.</summary>
    public sealed class StagePosterEntryFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        TMP_Text _label;
        Image _top, _bottom;
        Image _arrowUpper, _arrowLower;
        Button _button;
        Color _ink, _accent;
        bool _hover, _selected, _pressed;
        float _blend;

        public void Configure(Button button, TMP_Text label, Image top, Image bottom, Image arrowUpper, Image arrowLower, Color ink, Color accent)
        {
            _button=button; _label=label; _top=top; _bottom=bottom; _ink=ink; _accent=accent;
            _arrowUpper=arrowUpper; _arrowLower=arrowLower;
            _hover=_selected=_pressed=false; _blend=0f;
            Apply();
        }
        void Update()
        {
            bool focused = _button != null && _button.IsInteractable() && (_hover || _selected);
            _blend = Mathf.MoveTowards(_blend, focused ? 1f : 0f, Time.unscaledDeltaTime * 10f);
            Apply();
        }
        void Apply()
        {
            Color ink = Color.Lerp(_ink, _accent, _blend);
            if (_label != null) _label.color = ink;
            if (_top != null) _top.color = ink;
            if (_arrowUpper != null) _arrowUpper.color=ink;
            if (_arrowLower != null) _arrowLower.color=ink;
            if (_bottom != null)
            {
                _bottom.color = ink;
                _bottom.rectTransform.localScale = new Vector3(_pressed ? .92f : 1f + .04f * _blend, 1f, 1f);
            }
        }
        void OnDisable() { _hover=_selected=_pressed=false; _blend=0f; Apply(); }
        public void OnPointerEnter(PointerEventData e) => _hover=true;
        public void OnPointerExit(PointerEventData e) { _hover=false; _pressed=false; }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) _pressed=true; }
        public void OnPointerUp(PointerEventData e) => _pressed=false;
        public void OnSelect(BaseEventData e) => _selected=true;
        public void OnDeselect(BaseEventData e) { _selected=false; _pressed=false; }
    }
}
