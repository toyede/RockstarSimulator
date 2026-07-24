using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 특별 관객의 "카드를 놓을 수 있는 영역". Collider2D 하나로 판정만 담당한다.
    ///
    /// 카드는 Screen Space Overlay Canvas 의 UI 라서 Collider2D 끼리 물리 충돌하지 않는다.
    /// 그래서 <b>포인터 스크린 좌표를 월드로 바꿔 Collider2D.OverlapPoint 로 검사</b>한다.
    /// (카드를 Rigidbody2D 구조로 개조하지 않는다)
    ///
    /// 이 컴포넌트는:
    ///   - 카드/덱/손패를 <b>절대</b> 건드리지 않는다 (제거·드로우·소비 전부 카드 담당 몫)
    ///   - Special Hit 계산을 복제하지 않고 SpecialAudienceManager 에 위임한다
    ///   - 포인터가 영역 안인지만 매 프레임 갱신해 Hover 표시와 드롭 판정에 쓴다
    ///
    /// 드롭 판정을 카드 코드 수정 없이 붙이는 방법:
    /// SpecialAudience.CurrentRequest 가 <b>포인터가 이 영역 안일 때만</b> 활성 요청을 돌려준다.
    /// CardSystem 은 이미 그 값을 판정기에 넘기고 있으므로, 영역 밖에서 놓으면 자동으로 일반 카드 판정이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialAudienceDropTarget : MonoBehaviour
    {
        [Header("판정 영역")]
        [SerializeField, Tooltip("드롭 판정에 쓸 Collider2D (isTrigger 권장). 크기·오프셋은 인스펙터에서 조절")]
        Collider2D hitCollider;

        [SerializeField, Tooltip("이 오브젝트를 따라다닌다 (특별 관객 액터). 위치만 복사하고 크기·회전은 따라가지 않는다")]
        Transform followTarget;

        [SerializeField, Tooltip("따라다닐 때의 위치 보정")]
        Vector2 followOffset = Vector2.zero;

        [Header("Hover 표시")]
        [SerializeField, Tooltip("Hover 시 확대할 대상 (보통 VisualRoot). 없으면 표시 없음")]
        Transform hoverScaleTarget;

        [SerializeField, Tooltip("Hover 시 배율")]
        float hoverScale = 1.15f;

        [SerializeField, Tooltip("확대·복귀 속도. 클수록 빠르게 붙는다")]
        float hoverScaleSpeed = 12f;

        [SerializeField, Tooltip("Special 역할 카드를 드래그할 때만 Hover 를 표시한다")]
        bool onlyForSpecialCards = true;

        [SerializeField, Tooltip("선택. 켜두면 요구 타입과 일치하는 Special 카드일 때만 표시한다")]
        bool requireMatchingStage = false;

        [SerializeField, Tooltip("포인터가 영역 안일 때 켜지는 부가 오브젝트 (선택. 비워두면 크기만 변한다)")]
        GameObject highlight;

        [Header("카메라")]
        [SerializeField, Tooltip("스크린 → 월드 변환에 쓸 카메라. 비워두면 Camera.main")]
        Camera worldCamera;

        // ---------------- 상태 ----------------

        bool _requestActive;      // 특별 관객이 요구 중인가 (Special Hit 처리 후에는 false)
        bool _pointerInside;      // 순수 기하 판정 — 드롭 게이트가 쓴다
        bool _hoverVisible;       // 표시용 — Special 카드를 들고 있을 때만 켠다
        Vector3 _hoverBaseScale = Vector3.one;
        bool _warnedNoCollider;

        /// <summary>지금 카드를 받을 수 있는 상태인가.</summary>
        public bool IsActive => _requestActive && hitCollider != null && hitCollider.enabled;

        /// <summary>
        /// 포인터가 영역 안에 있는가. <b>표시 조건과 무관한 순수 기하 판정</b>이다.
        ///
        /// 캐시값을 쓰지 않고 물어볼 때마다 새로 계산한다. 카드를 놓는 순간(OnEndDrag)이
        /// 이 컴포넌트의 Update 보다 먼저 올 수 있어서, 한 프레임 전 값으로 판정하면
        /// 빠르게 튕기듯 놓았을 때 결과가 어긋난다.
        /// </summary>
        public bool IsPointerOver => ContainsScreenPoint(ResolveCamera(), ReadPointerPosition());

        /// <summary>Hover 표시가 켜져 있는가.</summary>
        public bool IsHoverVisible => _hoverVisible;

        public Collider2D HitCollider => hitCollider;

        void Awake()
        {
            if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>(true);
            if (hoverScaleTarget != null) _hoverBaseScale = hoverScaleTarget.localScale;
            SetActiveInternal(false);
        }

        /// <summary>
        /// 런타임에 만들어 붙일 때 쓰는 초기화. (프리팹을 씬에 배치하지 않아도 판정이 살아있게 하기 위함)
        /// 인스펙터에서 이미 지정한 값이 있으면 덮어쓰지 않는다.
        /// </summary>
        public void Configure(Collider2D collider, Transform follow, Transform hoverScaleRoot)
        {
            if (collider != null) hitCollider = collider;
            if (follow != null) followTarget = follow;
            if (hoverScaleRoot != null)
            {
                hoverScaleTarget = hoverScaleRoot;
                _hoverBaseScale = hoverScaleRoot.localScale;
            }
            SetActiveInternal(false);
        }

        void OnEnable()
        {
            EventBus.Subscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Subscribe<SpecialAudienceEnded>(OnEnded);

            if (SpecialAudienceManager.HasInstance)
                SpecialAudienceManager.Instance.RegisterDropTarget(this);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Unsubscribe<SpecialAudienceEnded>(OnEnded);

            if (SpecialAudienceManager.HasInstance)
                SpecialAudienceManager.Instance.UnregisterDropTarget(this);

            // Disable 시 Hover·요청 상태를 정리해 다음에 켜질 때 잔상이 없게 한다
            SetHover(false);
            SetActiveInternal(false);
        }

        // ---------------- 이벤트 ----------------

        void OnSpawned(SpecialAudienceSpawned e) => SetActiveInternal(true);

        void OnEnded(SpecialAudienceEnded e)
        {
            // Special Hit 순간 즉시 닫아 중복 드롭을 막는다 (연출은 계속 재생돼도 판정은 끝)
            SetActiveInternal(false);
        }

        void SetActiveInternal(bool active)
        {
            _requestActive = active;
            if (hitCollider != null) hitCollider.enabled = active;
            if (!active) SetHover(false);
        }

        // ---------------- 판정 ----------------

        void Update()
        {
            if (followTarget != null)
            {
                // 위치만 따라간다. 액터는 점프·스쿼시로 스케일이 계속 변하므로
                // 그걸 물려받으면 히트 영역이 같이 떨린다.
                Vector3 p = followTarget.position;
                transform.position = new Vector3(p.x + followOffset.x, p.y + followOffset.y, transform.position.z);
            }

            if (!IsActive)
            {
                _pointerInside = false;
                SetHover(false);
                UpdateHoverScale();
                return;
            }

            // 1) 기하 판정 — Hover 표시용으로만 캐시한다 (게이트는 IsPointerOver 가 즉석 계산)
            _pointerInside = ContainsScreenPoint(ResolveCamera(), ReadPointerPosition());

            // 2) 표시 판정 — Special 카드를 드래그 중일 때만 켠다.
            SetHover(_pointerInside && ShouldShowHover());
            UpdateHoverScale();
        }

        /// <summary>지금 들고 있는 카드가 Hover 를 보여줄 만한 카드인가.</summary>
        bool ShouldShowHover()
        {
            if (!onlyForSpecialCards) return true;

            var dragging = CardDragHandler.Current;
            if (dragging == null) return false; // 드래그 중이 아니면 표시하지 않는다

            var card = dragging.Card;
            if (card == null || card.Role != CardRole.Special) return false;

            if (requireMatchingStage && SpecialAudienceManager.HasInstance)
                return card.TargetStage == SpecialAudienceManager.Instance.CurrentRequestType;

            return true;
        }

        /// <summary>확대·복귀를 부드럽게 따라가게 한다. (툭 끊기면 픽셀이 튀어 보인다)</summary>
        void UpdateHoverScale()
        {
            if (hoverScaleTarget == null) return;

            Vector3 target = _hoverVisible ? _hoverBaseScale * hoverScale : _hoverBaseScale;
            hoverScaleTarget.localScale = Vector3.Lerp(
                hoverScaleTarget.localScale,
                target,
                1f - Mathf.Exp(-hoverScaleSpeed * Time.deltaTime));
        }

        /// <summary>스크린 좌표가 영역 안인가. 카드가 UI 라서 이 경로를 쓴다.</summary>
        public bool ContainsScreenPoint(Camera camera, Vector2 screenPosition)
        {
            if (camera == null) return false;
            // z 는 카메라에서 z=0 평면까지의 거리. 직교 카메라에서는 x/y 가 z 와 무관하지만,
            // 나중에 원근 카메라로 바꿔도 판정이 어긋나지 않도록 정석대로 넣는다.
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            return ContainsWorldPoint(world);
        }

        public bool ContainsWorldPoint(Vector2 worldPosition)
        {
            if (!IsActive)
            {
                if (hitCollider == null) WarnNoColliderOnce();
                return false;
            }
            return hitCollider.OverlapPoint(worldPosition);
        }

        /// <summary>
        /// 카드가 이 영역에 놓였을 때의 판정. Special Hit 계산은 매니저가 하고 여기서는 위임만 한다.
        /// <b>카드를 지우거나 손패를 건드리지 않는다.</b>
        /// </summary>
        public bool TryDrop(HeatStage playedType, out SpecialHitReward reward)
        {
            reward = default;
            if (!IsActive) return false;
            if (!SpecialAudienceManager.HasInstance) return false;

            return SpecialAudienceManager.Instance.TrySpecialHit(playedType, out reward);
        }

        // ---------------- Hover ----------------

        /// <summary>Hover 표시만 켜고 끈다. 드롭 판정에는 영향이 없다.</summary>
        public void SetHover(bool isHovered)
        {
            if (_hoverVisible == isHovered) return;
            _hoverVisible = isHovered;

            if (highlight != null) highlight.SetActive(isHovered);
        }

        // ---------------- 유틸 ----------------

        Camera ResolveCamera()
        {
            // 매 프레임 찾지 않고 한 번 잡아 캐시한다
            if (worldCamera == null) worldCamera = Camera.main;
            return worldCamera;
        }

        static Vector2 ReadPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        void WarnNoColliderOnce()
        {
            if (_warnedNoCollider) return;
            _warnedNoCollider = true;
            Debug.LogWarning("[SpecialAudience] DropTarget 에 Collider2D 가 없습니다. " +
                             "HitArea 자식에 BoxCollider2D(isTrigger)를 두고 연결하세요.", this);
        }
    }
}
