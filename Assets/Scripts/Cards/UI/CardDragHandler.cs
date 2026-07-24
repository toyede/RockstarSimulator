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

        /// <summary>
        /// 지금 드래그 중인 카드. 없으면 null.
        /// 특별 관객 드롭 영역처럼 "무엇을 들고 있는지"에 따라 표시를 바꿔야 하는 쪽이 읽는다.
        /// (읽기 전용이며 카드 처리 순서에는 관여하지 않는다)
        /// </summary>
        public static CardDragHandler Current { get; private set; }

        RectTransform _rectTransform;
        LayoutElement _layoutElement;
        Canvas _sortingCanvas;
        CanvasGroup _canvasGroup;
        CardDefinition _card;

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
        readonly Vector3[] _worldCorners = new Vector3[4];

        public bool IsDragging => _dragging;
        public int HandIndex => _handIndex;

        /// <summary>이 카드의 데이터. 드롭 대상이 카드 종류를 확인할 때 쓴다.</summary>
        public CardDefinition Card => _card;

        /// <summary>현재 화면에 보이는 카드 가로폭의 절반. 스페셜 드롭 원의 반지름으로 쓴다.</summary>
        public float DropRadiusPixels
        {
            get
            {
                if (_rectTransform == null) return 0f;

                _rectTransform.GetWorldCorners(_worldCorners);
                Camera camera = ResolveCanvasCamera();
                Vector2 left = (
                    RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]) +
                    RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[1])) * 0.5f;
                Vector2 right = (
                    RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[2]) +
                    RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[3])) * 0.5f;
                return Vector2.Distance(left, right) * 0.5f;
            }
        }

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
            Current = this;   // 드롭 대상이 "지금 무슨 카드를 들고 있는지" 볼 수 있게 한다
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
            var originalParent = _originalParent;
            SpecialCardRequest specialRequest =
                shouldUse && _card != null && _card.Role == CardRole.Special
                    ? SpecialAudience.ResolveDropRequest(eventData.position, DropRadiusPixels)
                    : SpecialCardRequest.None;

            _hovered = false;
            _dragging = false;
            if (Current == this) Current = null;
            _dragPointerId = int.MinValue;
            RemovePlaceholder();

            // 사용을 먼저 시도한다. 성공하면 카드는 지금 놓인 자리(마우스 위치)에 그대로 두고
            // 사용 연출이 거기서 재생된다. 손패로 되돌리면 제자리에서 사라지게 되므로 되돌리지 않는다.
            if (shouldUse &&
                cardInput != null &&
                cardInput.TryUseCard(handIndex, specialRequest))
            {
                _originalParent = null;
                if (originalParent is RectTransform parentRect)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect); // 남은 카드 간격 정리
                return;
            }

            // 사용하지 못했으면 원래대로 손패에 돌려놓는다
            RestoreToHand();
            _canvasGroup.blocksRaycasts = true;
            SetSorting(0);
            AnimateScale(1f);
        }

        void CacheComponents()
        {
            if (_rectTransform == null) _rectTransform = (RectTransform)transform;
            if (_layoutElement == null) _layoutElement = GetComponent<LayoutElement>();
            if (_sortingCanvas == null) _sortingCanvas = GetComponent<Canvas>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_card == null) _card = GetComponent<CardDefinition>();
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
            if (Current == this) Current = null;
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

        Camera ResolveCanvasCamera()
        {
            if (_sortingCanvas == null) return null;

            var rootCanvas = _sortingCanvas.rootCanvas;
            return rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
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
