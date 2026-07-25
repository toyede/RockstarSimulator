using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 특별 관객을 <b>무대 아래 일반 관객들 사이</b>에 세우고 돌아다니게 하는 월드 표현.
    ///
    /// - 등장하면 AudienceRosterPresenter가 표시 중인 관객 중 한 명의 자리를 골라 그 줄에 섞여 선다
    /// - 일정 시간마다 다른 관객 자리로 옮겨 다닌다 (줄이 바뀌면 크기·정렬 순서도 그 줄을 따라간다)
    /// - 걷는 동안에도 일반 관객과 같은 반동·흔들림·점프를 한다 (CrowdMotionProfile 재사용)
    ///
    /// 매니저를 직접 참조하지 않고 EventBus 만 구독하므로,
    /// UI 쪽 SpecialAudienceView 와 <b>동시에</b> 붙여 쓸 수 있다 (게이지는 UI, 캐릭터는 무대).
    ///
    /// AudienceRosterPresenter 참조나 표시 중인 관객이 없으면 오류를 남기고 표시하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpecialAudienceCrowdActor : MonoBehaviour
    {
        [Header("개별 관객 런타임")]
        [SerializeField, Tooltip("현재 개별 관객을 표시하는 Presenter. 씬 설치 도구가 연결한다")]
        AudienceRosterPresenter audiencePresenter;

        [SerializeField, Tooltip("등장 시 개별 관객 루트의 자식으로 들어가 같은 좌표계를 사용한다")]
        bool parentUnderAudience = true;

        [Header("적용 대상 (프리팹 구조에 맞게 분리)")]
        [SerializeField, Tooltip("위치를 옮길 대상. 비워두면 이 오브젝트.\n" +
                                 "프리팹에서는 루트를 지정해야 HitArea 까지 함께 움직인다")]
        Transform motionRoot;

        [SerializeField, Tooltip("크기·회전을 적용할 대상. 비워두면 이 오브젝트.\n" +
                                 "점프 스쿼시가 Collider 를 흔들지 않도록 시각 오브젝트만 지정한다")]
        Transform visualRoot;

        [Header("요구별 스프라이트 (프레임 1장 = 정지 이미지)")]
        [SerializeField] SpriteAnimationClip chillClip = new SpriteAnimationClip { clipName = "Chill" };
        [SerializeField] SpriteAnimationClip singalongClip = new SpriteAnimationClip { clipName = "Singalong" };
        [SerializeField] SpriteAnimationClip moshClip = new SpriteAnimationClip { clipName = "Mosh" };

        [Header("돌아다니기")]
        [SerializeField, Tooltip("이동 속도(초당 월드 유닛)")]
        float moveSpeed = 1.6f;

        [SerializeField, Tooltip("한 자리에 머무는 시간(초). 이 시간이 지나면 다른 관객 자리로 옮긴다")]
        float dwellDuration = 1.8f;

        [SerializeField, Tooltip("고른 관객 자리에서 좌우로 벗어나는 거리. 정확히 겹치지 않게 한다")]
        float lateralOffset = 0.45f;

        [SerializeField, Tooltip("옆 사람보다 살짝 앞에 세워 가려지지 않게 하는 정렬 보정")]
        int sortingOrderBonus = 1;

        [SerializeField, Tooltip("주변 관객 대비 크기 배율. 1보다 크면 눈에 잘 띈다")]
        float scaleMultiplier = 1.15f;

        [SerializeField, Tooltip("줄이 바뀔 때 크기가 따라붙는 속도")]
        float scaleLerpSpeed = 6f;

        [Header("움직임 (일반 관객과 같은 수치 구조)")]
        [SerializeField]
        CrowdMotionProfile motion = new CrowdMotionProfile
        {
            bobHeight = 0.07f, bobSpeed = 1.8f,
            swayAngle = 7f, swaySpeed = 1.6f,
            jumpHeight = 0.25f, jumpsPerSecond = 1.2f,
            airTimeRatio = 0.7f, squash = 0.12f,
        };

        [SerializeField, Tooltip("Special Hit 성공 시 잠깐 크게 뛰는 연출")]
        CrowdMotionProfile celebrateMotion = new CrowdMotionProfile
        {
            bobHeight = 0.05f, bobSpeed = 2.5f,
            swayAngle = 12f, swaySpeed = 3f,
            jumpHeight = 0.6f, jumpsPerSecond = 2.4f,
            airTimeRatio = 0.75f, squash = 0.18f,
        };

        [Header("Mosh Special Hit 화면 효과")]
        [SerializeField, Tooltip("지정하면 이 통합 화면 효과 프로필을 사용한다. 비어 있으면 기본 링 디스토션을 사용한다")]
        LocalScreenEffectProfile moshSpecialHitEffect;

        [SerializeField, Tooltip("효과 영역의 가로·세로 크기(월드 유닛)")]
        Vector2 moshSpecialHitEffectSize = new Vector2(10f, 10f);

        [SerializeField, Min(0f), Tooltip("프로필 강도 배율")]
        float moshSpecialHitStrengthMultiplier = 1f;

        [SerializeField, Min(0f), Tooltip("0이면 프로필 시간 사용, 0보다 크면 이 시간으로 덮어쓴다")]
        float moshSpecialHitDurationOverride;

        SpriteRenderer _renderer;
        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();

        bool _active;
        bool _celebrating;
        float _celebrateUntil;    // 환호 연출이 끝나는 시각
        float _activeHitHoldDuration;
        Vector3 _feet;            // 점프·반동을 뺀 실제 발 위치 (부모 기준 로컬)
        Vector3 _anchor;          // 지금 향하고 있는 자리 (부모 기준 로컬)
        float _repathAt;          // 다음에 자리를 옮길 시각
        float _targetScale = 1f;
        float _currentScale = 1f;
        float _hoverScaleMultiplier = 1f;
        float _facing = 1f;       // 스프라이트 좌우 반전
        float _phase;             // 개체 고유 위상 (일반 관객과 리듬이 겹치지 않게)

        public SpriteRenderer CharacterRenderer
        {
            get
            {
                if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
                return _renderer;
            }
        }

        /// <summary>지금 무대에 나와 있는가. (말풍선 등 부속 연출이 표시 여부를 맞출 때 쓴다)</summary>
        public bool IsActive => _active;

        /// <summary>
        /// 현재 줄에 맞춰 적용 중인 크기. 앞줄이면 크고 뒷줄이면 작다.
        /// 말풍선처럼 본체와 함께 커져야 하는 부속물이 이 값을 곱해서 쓴다.
        /// </summary>
        public float CurrentVisualScale => Mathf.Max(0.01f, _currentScale * _hoverScaleMultiplier);

        /// <summary>
        /// 바라보는 방향(-1 또는 1). visualRoot 의 X 스케일에 곱해지므로,
        /// visualRoot 아래에 글자를 두면 좌우가 뒤집힌다 — 말풍선은 루트 쪽에 두고 이 값을 참고만 한다.
        /// </summary>
        public float Facing => _facing;

        /// <summary>드롭 Hover가 본체 크기에 더할 최종 배율. 이동·점프 스케일과 곱해서 적용한다.</summary>
        public void SetHoverScaleMultiplier(float multiplier)
        {
            _hoverScaleMultiplier = Mathf.Max(0.01f, multiplier);
        }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _player.Bind(_renderer);
            _phase = Random.value * 10f;

            if (motionRoot == null)
            {
                SpecialAudienceDropTarget dropTarget =
                    GetComponentInParent<SpecialAudienceDropTarget>();
                motionRoot =
                    dropTarget != null ? dropTarget.transform : transform;
            }
            if (visualRoot == null)
                visualRoot = transform.parent != null
                    ? transform.parent
                    : transform;

            SetVisible(false);
        }

        void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            WarnIfDuplicateActor();
#endif
        }

        public AudienceRosterPresenter AudiencePresenter => audiencePresenter;

        public void Configure(AudienceRosterPresenter presenter)
        {
            audiencePresenter = presenter;
        }

        void OnEnable()
        {
            EventBus.Subscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Subscribe<SpecialAudienceEnded>(OnEnded);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Unsubscribe<SpecialAudienceEnded>(OnEnded);
        }

        // ---------------- 이벤트 ----------------

        void OnSpawned(SpecialAudienceSpawned e)
        {
            _celebrating = false;
            ApplyClip(e.RequestType);
            if (!AttachToCrowd()) return;
            PickNewSpot(immediate: true);
            SetVisible(true);
        }

        void OnEnded(SpecialAudienceEnded e)
        {
            // 성공했을 때만 잠깐 환호하고 사라진다. (숨기는 타이밍은 매니저의 연출 유지시간과 맞춘다)
            if (e.Reason == SpecialAudienceEndReason.SpecialHit)
            {
                _celebrating = true;
                _celebrateUntil = Time.time + _activeHitHoldDuration;
                return;
            }
            SetVisible(false);
        }

        void OnSpecialHit(SpecialHitLanded e)
        {
            _activeHitHoldDuration = Mathf.Max(0f, e.HoldDuration);
            if (!_active || e.RequestType != HeatStage.Mosh) return;

            Vector3 effectPosition = CharacterRenderer != null
                ? CharacterRenderer.bounds.center
                : motionRoot.position;

            if (moshSpecialHitEffect != null)
            {
                ScreenEffects.Play(
                    moshSpecialHitEffect,
                    effectPosition,
                    moshSpecialHitEffectSize,
                    moshSpecialHitStrengthMultiplier,
                    moshSpecialHitDurationOverride);
                return;
            }

            float radius = Mathf.Max(
                0.01f,
                Mathf.Max(moshSpecialHitEffectSize.x, moshSpecialHitEffectSize.y) * 0.5f);
            float strength = 0.04f * moshSpecialHitStrengthMultiplier;
            if (moshSpecialHitDurationOverride > 0f)
            {
                ScreenEffects.PlayDistortion(
                    effectPosition,
                    radius,
                    strength,
                    moshSpecialHitDurationOverride);
            }
            else
            {
                ScreenEffects.PlayDistortion(effectPosition, radius, strength);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void WarnIfDuplicateActor()
        {
            var actors = FindObjectsByType<SpecialAudienceCrowdActor>(FindObjectsSortMode.None);
            if (actors.Length <= 1) return;

            Debug.LogWarning($"[SpecialAudience] 특별 관객 액터가 {actors.Length}개 있습니다. " +
                             "하나만 남기세요 — 여러 개면 등장 이벤트에 모두 반응해 관객이 여러 명 보입니다.", this);
        }
#endif

        // ---------------- 배치 ----------------

        /// <summary>개별 관객 루트의 자식으로 들어가 좌표·크기 기준을 맞춘다.</summary>
        bool AttachToCrowd()
        {
            if (audiencePresenter == null &&
                AudienceRosterSystem.HasInstance)
            {
                audiencePresenter =
                    AudienceRosterSystem.Instance
                        .GetComponent<AudienceRosterPresenter>();
            }

            if (audiencePresenter == null ||
                audiencePresenter.MemberRoot == null)
            {
                Debug.LogError(
                    "[SpecialAudience] AudienceRosterPresenter 참조가 없습니다. " +
                    "관객 런타임 설치 도구로 연결하세요.",
                    this);
                SetVisible(false);
                return false;
            }

            // motionRoot는 SpecialAudience 프리팹 루트이므로 HitArea도 함께 이동한다.
            if (parentUnderAudience &&
                motionRoot.parent != audiencePresenter.MemberRoot)
            {
                motionRoot.SetParent(
                    audiencePresenter.MemberRoot,
                    worldPositionStays: false);
                motionRoot.localScale = Vector3.one;
            }

            return true;
        }

        /// <summary>
        /// 일반 관객 한 명을 골라 그 옆자리를 다음 목적지로 삼는다.
        /// 관객의 줄 정보(높이·크기·정렬 순서)를 그대로 물려받아 자연스럽게 섞인다.
        /// </summary>
        void PickNewSpot(bool immediate)
        {
            _repathAt = Time.time + Mathf.Max(0.1f, dwellDuration);

            if (audiencePresenter == null ||
                !audiencePresenter.TryGetRandomActor(out AudienceMemberActor picked))
            {
                Debug.LogWarning(
                    "[SpecialAudience] 표시 중인 개별 관객이 없어 특별 관객을 표시하지 않습니다.",
                    this);
                SetVisible(false);
                return;
            }

            // 후보를 몇 번 뽑아 지금 위치에서 너무 가깝지 않은 자리를 고른다.
            for (int i = 0; i < 4; i++)
            {
                if (!audiencePresenter.TryGetRandomActor(
                        out AudienceMemberActor candidate))
                    break;
                picked = candidate;
                if (Mathf.Abs(
                        candidate.LayoutLocalPosition.x -
                        motionRoot.localPosition.x) > lateralOffset)
                    break;
            }
            if (picked == null) return;

            float side = Random.value < 0.5f ? -1f : 1f;
            _anchor =
                picked.LayoutLocalPosition +
                new Vector3(lateralOffset * side, 0f, 0f);

            _targetScale = picked.LayoutScale * scaleMultiplier;
            _renderer.sortingOrder = picked.SortingOrder + sortingOrderBonus;

            if (immediate) SnapToAnchor();
        }

        void SnapToAnchor()
        {
            _feet = _anchor;
            motionRoot.localPosition = _anchor;
            _currentScale = _targetScale;
        }

        // ---------------- 움직임 ----------------

        void Update()
        {
            _player.Tick(Time.deltaTime);
            if (!_active) return;

            // 환호가 끝나면 사라진다 (매니저가 특별 관객을 숨기는 타이밍과 맞춰둔다)
            if (_celebrating && Time.time >= _celebrateUntil) { SetVisible(false); return; }

            // 1) 자리 이동 — 목표 자리로 걸어간다. 환호 중에는 제자리에서 뛴다.
            if (!_celebrating && Time.time >= _repathAt) PickNewSpot(immediate: false);

            float dx = _anchor.x - _feet.x;
            // 이동 애니메이션 원화가 왼쪽을 보고 있어 부호를 반대로 적용한다.
            if (Mathf.Abs(dx) > 0.01f) _facing = -Mathf.Sign(dx); // 진행 방향을 본다

            if (!_celebrating)
                _feet = Vector3.MoveTowards(_feet, _anchor, moveSpeed * Time.deltaTime);

            // 2) 제자리 움직임 — 일반 관객과 같은 방식으로 반동·흔들림·점프
            var profile = _celebrating ? celebrateMotion : motion;
            CrowdMotionEvaluator.Evaluate(profile, Time.time + _phase, out float height, out float sway, out float squash);

            // 발 위치는 따로 들고 있고 점프는 거기에 얹기만 한다 (점프하면서 자리가 밀려 올라가지 않는다)
            motionRoot.localPosition = _feet + new Vector3(0f, height, 0f);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, sway);

            _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * scaleLerpSpeed);
            visualRoot.localScale = new Vector3(
                _facing * _currentScale * (1f + squash * 0.5f) * _hoverScaleMultiplier,
                _currentScale * (1f - squash) * _hoverScaleMultiplier,
                1f);
        }

        // ---------------- 표시 ----------------

        void ApplyClip(HeatStage requestType)
        {
            var clip = ResolveClip(requestType);
            if (clip != null && clip.IsValid) _player.Play(clip, restart: true);
        }

        SpriteAnimationClip ResolveClip(HeatStage requestType)
            => requestType.Select(chillClip, singalongClip, moshClip);

        void SetVisible(bool visible)
        {
            _active = visible;
            _celebrating = false;
            if (_renderer != null) _renderer.enabled = visible;
        }
    }
}
