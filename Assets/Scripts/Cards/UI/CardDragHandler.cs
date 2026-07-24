using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>카드 호버 확대와 손패 밖으로 드래그해 사용하는 상호작용을 담당한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(LayoutElement))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class CardDragHandler : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        const int HoverSortingOrder = 100;
        const int DragSortingOrder = 200;

        [SerializeField, Min(1f)] float hoverScale = 1.12f;
        [SerializeField, Min(0f)] float hoverDuration = 0.12f;

        RectTransform _rectTransform;
        LayoutElement _layoutElement;
        Canvas _sortingCanvas;
        CanvasGroup _canvasGroup;

        CardInput _cardInput;
        RectTransform _dragLayer;
        RectTransform _handArea;
        Transform _originalParent;
        GameObject _placeholder;
        Coroutine _scaleRoutine;

        int _handIndex = -1;
        int _originalSiblingIndex;
        int _dragPointerId = int.MinValue;
        Vector2 _pointerOffset;
        bool _bound;
        bool _hovered;
        bool _dragging;

        public bool IsDragging => _dragging;
        public int HandIndex => _handIndex;

        void Awake()
        {
            CacheComponents();
            ResetVisualState();
        }

        void OnEnable()
        {
            CacheComponents();
            ResetVisualState();
        }

        void OnDisable()
        {
            StopScaleAnimation();
            RemovePlaceholder();
            ResetVisualState();
            _cardInput = null;
            _dragLayer = null;
            _handArea = null;
            _originalParent = null;
            _handIndex = -1;
            _bound = false;
        }

        public void Bind(
            int handIndex,
            CardInput cardInput,
            RectTransform dragLayer,
            RectTransform handArea)
        {
            CacheComponents();
            StopScaleAnimation();
            RemovePlaceholder();
            ResetVisualState();

            _handIndex = handIndex;
            _cardInput = cardInput;
            _dragLayer = dragLayer;
            _handArea = handArea;
            _bound = handIndex >= 0 &&
                     cardInput != null &&
                     dragLayer != null &&
                     handArea != null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_bound || _dragging) return;

            _hovered = true;
            SetSorting(HoverSortingOrder);
            AnimateScale(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (_dragging) return;

            SetSorting(0);
            AnimateScale(1f);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_bound ||
                _dragging ||
                eventData.button != PointerEventData.InputButton.Left ||
                !_cardInput.CanUseCard(_handIndex))
            {
                return;
            }

            _dragging = true;
            _dragPointerId = eventData.pointerId;
            _originalParent = _rectTransform.parent;
            _originalSiblingIndex = _rectTransform.GetSiblingIndex();

            CreatePlaceholder();
            _layoutElement.ignoreLayout = true;
            _rectTransform.SetParent(_dragLayer, true);
            _rectTransform.SetAsLastSibling();

            _canvasGroup.blocksRaycasts = false;
            SetSorting(DragSortingOrder);
            AnimateScale(hoverScale);

            if (TryGetPointerLocalPosition(eventData, out var pointerPosition))
                _pointerOffset = _rectTransform.anchoredPosition - pointerPosition;
            else
                _pointerOffset = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _dragPointerId) return;
            if (!TryGetPointerLocalPosition(eventData, out var pointerPosition)) return;

            _rectTransform.anchoredPosition = pointerPosition + _pointerOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _dragPointerId) return;

            bool shouldUse = IsAboveHand(eventData);
            var cardInput = _cardInput;
            int handIndex = _handIndex;

            RestoreToHand();
            _hovered = false;
            _dragging = false;
            _dragPointerId = int.MinValue;
            _canvasGroup.blocksRaycasts = true;
            SetSorting(0);

            if (shouldUse && cardInput != null && cardInput.TryUseCard(handIndex))
                return;

            AnimateScale(1f);
        }

        void CacheComponents()
        {
            if (_rectTransform == null) _rectTransform = (RectTransform)transform;
            if (_layoutElement == null) _layoutElement = GetComponent<LayoutElement>();
            if (_sortingCanvas == null) _sortingCanvas = GetComponent<Canvas>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        void ResetVisualState()
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one;
                _rectTransform.localRotation = Quaternion.identity;
            }

            if (_layoutElement != null) _layoutElement.ignoreLayout = false;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            SetSorting(0);

            _hovered = false;
            _dragging = false;
            _dragPointerId = int.MinValue;
            _pointerOffset = Vector2.zero;
        }

        void CreatePlaceholder()
        {
            RemovePlaceholder();
            if (_originalParent == null) return;

            _placeholder = new GameObject(
                "CardPlaceholder",
                typeof(RectTransform),
                typeof(LayoutElement));
            _placeholder.layer = gameObject.layer;

            var placeholderRect = (RectTransform)_placeholder.transform;
            placeholderRect.SetParent(_originalParent, false);
            placeholderRect.SetSiblingIndex(_originalSiblingIndex);
            placeholderRect.sizeDelta = _rectTransform.sizeDelta;

            var placeholderLayout = _placeholder.GetComponent<LayoutElement>();
            placeholderLayout.minWidth = _layoutElement.minWidth;
            placeholderLayout.minHeight = _layoutElement.minHeight;
            placeholderLayout.preferredWidth = _layoutElement.preferredWidth;
            placeholderLayout.preferredHeight = _layoutElement.preferredHeight;
            placeholderLayout.flexibleWidth = _layoutElement.flexibleWidth;
            placeholderLayout.flexibleHeight = _layoutElement.flexibleHeight;
        }

        void RestoreToHand()
        {
            if (_originalParent != null)
            {
                _rectTransform.SetParent(_originalParent, false);
                _rectTransform.SetSiblingIndex(_originalSiblingIndex);
                _rectTransform.anchoredPosition = Vector2.zero;
                _rectTransform.localRotation = Quaternion.identity;
                _layoutElement.ignoreLayout = false;

                RemovePlaceholder();
                if (_originalParent is RectTransform parentRect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
            else
            {
                RemovePlaceholder();
            }
            _originalParent = null;
        }

        void RemovePlaceholder()
        {
            if (_placeholder == null) return;

            _placeholder.SetActive(false);
            Destroy(_placeholder);
            _placeholder = null;
        }

        bool TryGetPointerLocalPosition(PointerEventData eventData, out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            return _dragLayer != null &&
                   RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       _dragLayer,
                       eventData.position,
                       eventData.pressEventCamera,
                       out localPosition);
        }

        bool IsAboveHand(PointerEventData eventData)
        {
            if (_handArea == null) return false;

            var corners = new Vector3[4];
            _handArea.GetWorldCorners(corners);
            float handTop = RectTransformUtility.WorldToScreenPoint(
                eventData.pressEventCamera,
                corners[1]).y;
            return eventData.position.y > handTop;
        }

        void AnimateScale(float multiplier)
        {
            StopScaleAnimation();

            Vector3 target = Vector3.one * multiplier;
            if (hoverDuration <= 0f || !isActiveAndEnabled)
            {
                _rectTransform.localScale = target;
                return;
            }

            _scaleRoutine = StartCoroutine(ScaleRoutine(target));
        }

        IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 start = _rectTransform.localScale;
            float elapsed = 0f;

            while (elapsed < hoverDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / hoverDuration);
                _rectTransform.localScale = Vector3.LerpUnclamped(
                    start,
                    target,
                    1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }

            _rectTransform.localScale = target;
            _scaleRoutine = null;
        }

        void StopScaleAnimation()
        {
            if (_scaleRoutine == null) return;
            StopCoroutine(_scaleRoutine);
            _scaleRoutine = null;
        }

        void SetSorting(int sortingOrder)
        {
            if (_sortingCanvas == null) return;

            _sortingCanvas.overrideSorting = sortingOrder > 0;
            _sortingCanvas.sortingOrder = sortingOrder;
        }
    }
}
