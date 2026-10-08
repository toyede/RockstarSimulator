using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// One set of species animation variants for an audience preference and engagement stage.
    /// </summary>
    [Serializable]
    public struct AnimatedVariantGroup
    {
        public CrowdPreference preference;
        public AudienceEngagementStage stage;
        public SpriteAnimationClip[] variants;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(AudienceReactionVFX))]
    public sealed class AudienceMemberActor : MonoBehaviour, IPoolable
    {
        [Header("Character")]
        [SerializeField] SpriteRenderer characterRenderer;
        [SerializeField] Sprite chillSprite;
        [SerializeField] Sprite singalongSprite;
        [SerializeField] Sprite moshSprite;
        [SerializeField] Color chillColor = Color.white;
        [SerializeField] Color singalongColor = Color.white;
        [SerializeField] Color moshColor = Color.white;

        [Header("Character Animation (optional per preference/stage)")]
        [Tooltip("Configured preference/stage pairs use animation. Missing pairs use the matching static sprite.")]
        [SerializeField] List<AnimatedVariantGroup> animatedVariants =
            new List<AnimatedVariantGroup>();

        readonly SpriteAnimationPlayer _animationPlayer =
            new SpriteAnimationPlayer();

        [Header("Departure Warning")]
        [SerializeField] GameObject warningRoot;
        [SerializeField] Transform warningFill;
        [SerializeField] SpriteRenderer warningBackgroundRenderer;
        [SerializeField] SpriteRenderer warningFillRenderer;
        [SerializeField] Color warningBackgroundColor = new Color(0.06f, 0.07f, 0.10f, 0.9f);
        [SerializeField] Color warningFillColor = new Color(0.95f, 0.19f, 0.16f, 1f);

        [Header("Crisis Warning")]
        [SerializeField] GameObject crisisWarningRoot;

        [Header("Card Reaction")]
        [SerializeField] AudienceReactionPopup reactionPopup;
        [SerializeField] AudienceReactionVFX reactionVFX;
        [SerializeField, Min(0f)] float loveItJumpDuration = 0.28f;
        [SerializeField, Min(0f)] float loveItJumpHeight = 0.38f;

        [Header("Preference Hover")]
        [SerializeField] Collider2D preferenceHoverCollider;
        [SerializeField] SpecialAudienceOutline preferenceOutline;

        [Header("Motion")]
        [SerializeField, Min(0f)] float enterDuration = 0.6f;
        [SerializeField, Min(0f)] float exitDuration = 0.7f;
        [SerializeField, Min(0f)] float crisisExitDuration = 0.5f;
        [SerializeField, Min(0f)] float crisisHorizontalTravel = 3.2f;
        [SerializeField, Min(0f)] float enterDistance = 1.2f;
        [SerializeField, Min(0f)] float enterStartScale = 0.55f;
        [SerializeField, Min(0f)] float exitDistance = 1.6f;
        [SerializeField, Min(0.01f)] float layoutFollowSpeed = 4f;
        [SerializeField, Min(0f)] float reactionPulseDuration = 0.22f;
        [SerializeField, Min(0f)] float reactionPulseScale = 0.18f;
        [Header("Idle Motion (몰입도 단계별)")]
        [SerializeField, Tooltip("Calm — 차분하게 몸만 흔든다")]
        CrowdMotionProfile calmMotion = new CrowdMotionProfile
        {
            bobHeight = 0.03f, bobSpeed = 0.8f,
            swayAngle = 2f, swaySpeed = 0.6f,
            jumpHeight = 0f, jumpsPerSecond = 0f,
            speedVariance = 0.25f,
        };

        [SerializeField, Tooltip("Middle — 뚜렷하게 흔들고 기운다")]
        CrowdMotionProfile middleMotion = new CrowdMotionProfile
        {
            bobHeight = 0.08f, bobSpeed = 1.6f,
            swayAngle = 6f, swaySpeed = 1.3f,
            jumpHeight = 0f, jumpsPerSecond = 0f,
            speedVariance = 0.2f,
        };

        [SerializeField, Tooltip("Excited — 방방 뛴다 (착지 스쿼시 포함)")]
        CrowdMotionProfile excitedMotion = new CrowdMotionProfile
        {
            bobHeight = 0.04f, bobSpeed = 2f,
            swayAngle = 8f, swaySpeed = 2.2f,
            jumpHeight = 0.45f, jumpsPerSecond = 1.8f,
            airTimeRatio = 0.7f, squash = 0.15f,
            speedVariance = 0.15f,
        };

        [SerializeField, Min(0f), Tooltip("단계가 바뀔 때 움직임이 서서히 섞이는 시간(초)")]
        float motionBlendDuration = 0.6f;

        [SerializeField, Tooltip("좌우 반전을 개체마다 다르게 줘서 같은 스프라이트가 덜 반복돼 보이게 한다")]
        bool allowHorizontalFlip = true;

        [Header("Card-specific presentation (score-neutral)")]
        [SerializeField, Min(0.1f)] float cardMotionDuration = 0.9f;
        [SerializeField, Min(0f)] float moshSpreadDistance = 1f;
        float _cardMotionRemaining;
        float _arrivalDelay;
        string _motionCardId;
        bool _bossArrival;
        bool _arrivalCuePending;
        public void MarkBossArrivalCue() => _arrivalCuePending = true;
        float _gestureSide, _gestureSpread, _gestureDelay, _gestureStartedAt;
        int _gestureCue;
        static Material _warningMaterial;

        /// <summary>Only positive reactions join the card's gesture; no engagement or score changes.</summary>
        public void PlayCardMotion(string cardId, int reactionValue,
            float groupCenter = 0f, float groupMin = 0f, float groupMax = 0f, float groupStart = -1f)
        {
            if (_exiting) return;
            if (reactionValue <= 0) { ResetCardMotion(); return; }
            _motionCardId = cardId;
            float x = _currentLayoutPosition.x;
            _gestureSide = Mathf.Abs(x - groupCenter) > 0.05f ? Mathf.Sign(x - groupCenter) : ((_boundId.Value & 1) == 0 ? -1f : 1f);
            _gestureSpread = groupMax - groupMin > 0.05f ? Mathf.Max(0f, moshSpreadDistance) : 0.12f;
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float worldEdge = camera.transform.position.x + _gestureSide * camera.orthographicSize * camera.aspect;
                float edge = transform.parent != null ? transform.parent.InverseTransformPoint(new Vector3(worldEdge, transform.position.y, 0)).x : worldEdge;
                _gestureSpread = Mathf.Min(_gestureSpread, Mathf.Max(0, _gestureSide * (edge - x) - 0.4f));
            }
            _gestureDelay = cardId == "hands_up" && groupMax > groupMin ? Mathf.InverseLerp(groupMin, groupMax, x) * 0.25f : 0f;
            _gestureStartedAt = groupStart >= 0 ? groupStart : Time.time;
            _gestureCue = 0;
            _cardMotionRemaining = cardMotionDuration + _gestureDelay;
        }

        void ResetCardMotion()
        {
            _cardMotionRemaining = 0f;
            _motionCardId = null;
            _gestureCue = 0;
        }

        // 에셋 없이 코드만으로 그리는 호응도 바용 1x1 흰색 스프라이트.
        // pixelsPerUnit=1이라 localScale이 곧 월드 유닛 크기가 된다.
        static Sprite _solidSprite;
        static Sprite SolidSprite
        {
            get
            {
                if (_solidSprite == null)
                {
                    var texture = new Texture2D(1, 1);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    _solidSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                }
                return _solidSprite;
            }
        }

        AudienceSnapshot _snapshot;
        AudienceId _boundId;
        Vector3 _layoutPosition;
        float _layoutScale = 1f;
        // 실제로 화면에 그려지는 위치·크기. 재배치 시 목표값(_layoutPosition/_layoutScale)을
        // 향해 서서히 따라가며, 관객 수 변화로 인한 순간이동을 막는다.
        Vector3 _currentLayoutPosition;
        float _currentLayoutScale = 1f;
        bool _hasLayout;
        float _calmUpperBound = 33f;
        float _phase;

        // 개체별 편차 + 단계 전환 블렌드 (CrowdMemberView 와 같은 방식)
        CrowdMotionEvaluator.Variance _variance = CrowdMotionEvaluator.Variance.Identity;
        CrowdMotionProfile _motionFrom, _motionTo;
        float _motionBlend = 1f;
        AudienceEngagementStage _motionStage = AudienceEngagementStage.Calm;
        float _visibility;
        float _reactionPulseRemaining;
        float _reactionJumpRemaining;
        float _exitElapsed;
        bool _exiting;
        AudienceExitStyle _exitStyle;
        float _exitDirection = 1f;
        Color _characterColor = Color.white;
        Action _exitCompleted;

        public AudienceId BoundId => _boundId;
        public AudienceSnapshot Snapshot => _snapshot;
        public bool IsBound => _boundId.IsValid;

        /// <summary>캐릭터 본체 렌더러. 정전 실루엣 등 연출이 색을 덮어쓸 때 쓴다 (되돌리는 것은 연출 담당 책임).</summary>
        public SpriteRenderer CharacterRenderer => characterRenderer;
        public AudienceReactionPopup ReactionPopup => reactionPopup;
        public AudienceReactionVFX ReactionVFX => reactionVFX;
        public Vector3 LayoutLocalPosition => _layoutPosition;
        bool _tutorialInspectionPose;

        /// <summary>관객 확인 교육에서만 자세를 안정화한다. 풀 재사용 시 해제된다.</summary>
        public void SetTutorialInspectionPose(bool enabled)
        {
            _tutorialInspectionPose = enabled;
            if (enabled)
            {
                ResetCardMotion();
                _reactionPulseRemaining = _reactionJumpRemaining = 0f;
            }
            ApplyTransform();
        }
        public float LayoutScale => _layoutScale;
        public int SortingOrder =>
            characterRenderer != null ? characterRenderer.sortingOrder : 0;
        public Vector2 PreferenceHoverCenter =>
            preferenceHoverCollider != null
                ? preferenceHoverCollider.bounds.center
                : transform.position;

        void Awake()
        {
            if (reactionVFX == null)
                reactionVFX = GetComponent<AudienceReactionVFX>();
            if (reactionVFX == null)
                reactionVFX = gameObject.AddComponent<AudienceReactionVFX>();

            if (characterRenderer == null ||
                chillSprite == null ||
                singalongSprite == null ||
                moshSprite == null ||
                warningRoot == null ||
                warningFill == null ||
                warningBackgroundRenderer == null ||
                warningFillRenderer == null ||
                reactionPopup == null ||
                !reactionPopup.IsConfigured)
            {
                Debug.LogError(
                    "[AudienceMemberActor] Prefab references are incomplete.",
                    this);
                enabled = false;
                return;
            }

            warningBackgroundRenderer.sprite = SolidSprite;
            warningFillRenderer.sprite = SolidSprite;
            warningBackgroundRenderer.color = warningBackgroundColor;
            warningFillRenderer.color = warningFillColor;
            if (_warningMaterial == null)
                _warningMaterial = Resources.Load<Material>("Effects/AudienceWarningUnlit");
            if (_warningMaterial != null)
                warningBackgroundRenderer.sharedMaterial = warningFillRenderer.sharedMaterial = _warningMaterial;
            _animationPlayer.Bind(characterRenderer);
        }

        public void OnSpawned()
        {
            _tutorialInspectionPose = false;
            ResetCardMotion();
            _arrivalDelay = 0f;
            _snapshot = default;
            _boundId = default;
            _visibility = 0f;
            _reactionPulseRemaining = 0f;
            _reactionJumpRemaining = 0f;
            _exitElapsed = 0f;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            _exitCompleted = null;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            ResetMotion();
            _hasLayout = false;
            _animationPlayer.Stop();
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);
            if (reactionPopup != null) reactionPopup.ResetVisual();
            if (reactionVFX != null) reactionVFX.ResetVisual();
            SetPreferenceReveal(false, default, 0f);
            ApplyTransform();
        }

        public void OnDespawned()
        {
            _tutorialInspectionPose = false;
            ResetCardMotion();
            _snapshot = default;
            _boundId = default;
            _exitCompleted = null;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            _animationPlayer.Stop();
            if (characterRenderer != null)
                characterRenderer.flipX = false;
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);
            if (reactionPopup != null) reactionPopup.ResetVisual();
            if (reactionVFX != null) reactionVFX.ResetVisual();
            SetPreferenceReveal(false, default, 0f);
        }

        public void Bind(AudienceSnapshot snapshot, float calmUpperBound)
        {
            if (!snapshot.Id.IsValid)
                throw new ArgumentException("Audience snapshot requires a valid id.", nameof(snapshot));

            _boundId = snapshot.Id;
            _tutorialInspectionPose = false;
            ResetCardMotion();
            _arrivalDelay = (snapshot.Id.Value & 3) * 0.04f;
            _bossArrival = StageRuntimeDirector.CurrentStage != null && StageRuntimeDirector.CurrentStage.IsBoss;
            _arrivalCuePending = false;
            _calmUpperBound = Mathf.Max(0.01f, calmUpperBound);
            _visibility = 0f;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            _exitElapsed = 0f;
            _hasLayout = false;
            _snapshot = snapshot;
            SetPreferenceReveal(false, default, 0f);
            ApplyVisual(snapshot.Preference, snapshot.Stage);
            UpdateWarning();
        }

        public void ApplySnapshot(AudienceSnapshot snapshot)
        {
            if (!IsBound || snapshot.Id != _boundId) return;

            bool preferenceChanged = snapshot.Preference != _snapshot.Preference;
            bool stageChanged = snapshot.Stage != _snapshot.Stage;
            _snapshot = snapshot;
            if (preferenceChanged || stageChanged || characterRenderer.sprite == null)
                ApplyVisual(snapshot.Preference, snapshot.Stage);
            if (stageChanged) SetMotionStage(snapshot.Stage, instant: false);
            UpdateWarning();
        }

        public void SetLayout(Vector3 localPosition, float scale, int sortingOrder)
        {
            _layoutPosition = localPosition;
            _layoutScale = Mathf.Max(0.01f, scale);
            characterRenderer.sortingOrder = sortingOrder;
            warningBackgroundRenderer.sortingOrder = sortingOrder + 20;
            warningFillRenderer.sortingOrder = sortingOrder + 21;
            warningBackgroundRenderer.sortingLayerName = "Effects";
            warningFillRenderer.sortingLayerName = "Effects";
            reactionPopup.SetSortingOrder(sortingOrder + 30);
            reactionVFX.SetSorting(
                characterRenderer.sortingLayerName,
                sortingOrder + 24);

            // 스폰 직후 첫 배치는 즉시 스냅하고, 이후 관객 수 변화로 인한
            // 재배치만 Update()에서 서서히 따라가게 한다.
            if (!_hasLayout)
            {
                _currentLayoutPosition = _layoutPosition;
                _currentLayoutScale = _layoutScale;
                _hasLayout = true;
            }

            ApplyTransform();
        }

        public void PlayReaction(int reactionValue, float engagementDelta)
        {
            if (_exiting || !enabled || reactionPopup == null) return;
            if (reactionValue != 0)
                _reactionPulseRemaining = reactionPulseDuration;

            if (reactionPopup.IsStrongPositive(reactionValue))
            {
                _reactionJumpRemaining = loveItJumpDuration;
                reactionVFX.PlayLoveIt();
            }
            else if (reactionValue > 0)
            {
                reactionVFX.PlayInterested();
            }
            else if (reactionPopup.IsStrongNegative(reactionValue))
            {
                reactionVFX.PlayBored();
            }

            reactionPopup.Show(
                reactionValue,
                engagementDelta,
                characterRenderer.sortingOrder + 30);
        }

        public void PlayFeverBonus(int score)
        {
            if (_exiting || !enabled || reactionPopup == null || score <= 0)
                return;

            _reactionPulseRemaining = reactionPulseDuration;
            reactionVFX.PlayFeverBurst();
            reactionPopup.ShowFever(
                score,
                characterRenderer.sortingOrder + 30);
        }

        public void PlayExit(Action completed)
            => PlayExit(AudienceExitStyle.Default, completed);

        public void PlayExit(
            AudienceExitStyle style,
            Action completed)
        {
            if (_exiting) return;
            _exiting = true;
            ResetCardMotion();
            _exitStyle = style;
            _exitDirection = Mathf.Abs(_currentLayoutPosition.x) > 0.05f
                ? Mathf.Sign(_currentLayoutPosition.x)
                : (_boundId.Value & 1) == 0 ? -1f : 1f;
            _exitElapsed = 0f;
            _exitCompleted = completed;
            SetPreferenceReveal(false, default, 0f);
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);

            float duration = Mathf.Max(
                0.01f,
                _exitStyle == AudienceExitStyle.NearbyConcert
                    ? crisisExitDuration
                    : exitDuration);
            Vector2 worldDirection =
                _exitStyle == AudienceExitStyle.NearbyConcert
                    ? Vector2.right * _exitDirection
                    : Vector2.up;
            reactionVFX.PlayDeparture(worldDirection, duration);
        }

        public void SetCrisisThreatened(bool threatened)
        {
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(threatened && !_exiting);
        }

        public bool ContainsPreferenceHoverPoint(Vector2 worldPosition)
        {
            return IsBound &&
                   !_exiting &&
                   preferenceHoverCollider != null &&
                   preferenceHoverCollider.enabled &&
                   preferenceHoverCollider.OverlapPoint(worldPosition);
        }

        public void SetPreferenceReveal(
            bool visible,
            Color color,
            float outlineThickness,
            AudienceOutlineMode mode = AudienceOutlineMode.HoverOverlay)
        {
            if (preferenceOutline == null) return;

            if (visible)
            {
                preferenceOutline.Configure(
                    characterRenderer,
                    color,
                    outlineThickness,
                    mode);
            }

            preferenceOutline.SetVisible(visible);
        }

        void Update()
        {
            if (!IsBound) return;

            float deltaTime = Time.deltaTime;
            _cardMotionRemaining = Mathf.Max(0f, _cardMotionRemaining - deltaTime);
            if (_cardMotionRemaining > 0f && _cardMotionRemaining <= cardMotionDuration && !_exiting)
            {
                float t = 1f - _cardMotionRemaining / Mathf.Max(0.1f, cardMotionDuration);
                if (_motionCardId == "open_mosh_pit" && t >= 0.7f && _gestureCue == 0)
                { reactionVFX.PlayMoshLanding(); _gestureCue = 1; }
                else if (_motionCardId == "tempo_up")
                {
                    int beat = t >= 0.5f ? 2 : t >= 0.1f ? 1 : 0;
                    if (beat > _gestureCue) { reactionVFX.PlayChillBeat(); _gestureCue = beat; }
                }
                else if (_motionCardId == "hands_up" && _gestureCue == 0)
                { reactionVFX.PlaySingalongGesture(); _gestureCue = 1; }
            }
            if (!_tutorialInspectionPose || _exiting) _animationPlayer.Tick(deltaTime);

            // 단계 전환 블렌드 진행 (프로필 값이 툭 튀지 않게)
            if (_motionBlend < 1f && motionBlendDuration > 0f)
                _motionBlend = Mathf.Min(1f, _motionBlend + deltaTime / motionBlendDuration);

            float followStep = layoutFollowSpeed * deltaTime;
            if (!_exiting)
            {
                _currentLayoutPosition = Vector3.MoveTowards(_currentLayoutPosition, _layoutPosition, followStep);
                _currentLayoutScale = Mathf.MoveTowards(_currentLayoutScale, _layoutScale, followStep);
            }

            if (_exiting)
            {
                _exitElapsed += deltaTime;
                float duration = Mathf.Max(
                    0.01f,
                    _exitStyle == AudienceExitStyle.NearbyConcert
                        ? crisisExitDuration
                        : exitDuration);
                _visibility = 1f - Mathf.Clamp01(_exitElapsed / duration);
                ApplyTransform();

                if (_exitElapsed < duration) return;
                if (reactionVFX != null && reactionVFX.HasLiveParticles)
                    return;
                Action completed = _exitCompleted;
                _exitCompleted = null;
                _boundId = default;
                completed?.Invoke();
                return;
            }

            float enterSpeed = enterDuration > 0f ? deltaTime / enterDuration : 1f;
            if (_arrivalDelay > 0f) _arrivalDelay = Mathf.Max(0f, _arrivalDelay - deltaTime);
            else _visibility = Mathf.MoveTowards(_visibility, 1f, enterSpeed);
            if (_arrivalCuePending && !_exiting && _visibility >= 1f)
            {
                _arrivalCuePending = false;
                if (SilhouetteBlend < 0.5f) reactionVFX.PlayChillBeat();
            }
            _reactionPulseRemaining = Mathf.Max(0f, _reactionPulseRemaining - deltaTime);
            _reactionJumpRemaining = Mathf.Max(0f, _reactionJumpRemaining - deltaTime);
            ApplyTransform();
        }

        void ApplyVisual(
            CrowdPreference preference,
            AudienceEngagementStage stage)
        {
            _characterColor = ColorFor(preference);

            SpriteAnimationClip clip = AnimationVariantFor(preference, stage);
            if (clip != null && clip.IsValid)
            {
                _animationPlayer.Play(clip, restart: true);
                return;
            }

            _animationPlayer.Stop();
            characterRenderer.sprite = StaticSpriteFor(preference);
        }

        Color ColorFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillColor;
                case CrowdPreference.Singalong: return singalongColor;
                case CrowdPreference.Mosh: return moshColor;
                default: throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
            }
        }

        Sprite StaticSpriteFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillSprite;
                case CrowdPreference.Singalong: return singalongSprite;
                case CrowdPreference.Mosh: return moshSprite;
                default: throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
            }
        }

        SpriteAnimationClip AnimationVariantFor(
            CrowdPreference preference,
            AudienceEngagementStage stage)
        {
            for (int i = 0; i < animatedVariants.Count; i++)
            {
                AnimatedVariantGroup group = animatedVariants[i];
                if (group.preference != preference || group.stage != stage)
                    continue;

                SpriteAnimationClip[] variants = group.variants;
                if (variants == null || variants.Length == 0) return null;
                return variants[PositiveModulo(_boundId.Value, variants.Length)];
            }

            return null;
        }

        static int PositiveModulo(int value, int divisor)
            => divisor <= 0 ? 0 : ((value % divisor) + divisor) % divisor;

        /// <summary>
        /// [읽기 전용] 이 프리팹의 성향별 대기(Calm) 애니메이션 클립과 정지 스프라이트.
        /// 관객이 아닌 표현(보스전 라이벌 팬)이 같은 아트를 쓰기 위한 접근이며, 액터 상태는 건드리지 않는다.
        /// </summary>
        public bool TryGetIdleVisual(CrowdPreference preference, int seed, out SpriteAnimationClip clip, out Sprite staticSprite)
            => TryGetPresentationVisual(preference, AudienceEngagementStage.Calm, seed, out clip, out staticSprite, out _);

        public CrowdMotionProfile PresentationMotion(AudienceEngagementStage stage) => ProfileFor(stage);

        public bool TryGetPresentationVisual(CrowdPreference preference, AudienceEngagementStage stage, int seed,
            out SpriteAnimationClip clip, out Sprite staticSprite, out Color baseColor)
        {
            clip = null;
            staticSprite = null;
            baseColor = ColorFor(preference);
            switch (preference)
            {
                case CrowdPreference.Chill: staticSprite = chillSprite; break;
                case CrowdPreference.Singalong: staticSprite = singalongSprite; break;
                case CrowdPreference.Mosh: staticSprite = moshSprite; break;
            }
            for (int i = 0; i < animatedVariants.Count; i++)
            {
                AnimatedVariantGroup group = animatedVariants[i];
                if (group.preference != preference || group.stage != stage) continue;
                if (group.variants != null && group.variants.Length > 0)
                    clip = group.variants[PositiveModulo(seed, group.variants.Length)];
                break;
            }
            return (clip != null && clip.IsValid) || staticSprite != null;
        }

        void UpdateWarning()
        {
            bool visible =
                !_exiting &&
                _snapshot.Stage == AudienceEngagementStage.Calm &&
                _snapshot.Engagement > 0f;
            warningRoot.SetActive(visible);
            if (!visible) return;

            float ratio = Mathf.Clamp01(_snapshot.Engagement / _calmUpperBound);
            Vector3 scale = warningFill.localScale;
            scale.x = ratio;
            warningFill.localScale = scale;

            Vector3 position = warningFill.localPosition;
            position.x = -0.5f * (1f - ratio);
            warningFill.localPosition = position;
        }

        // ---------------- Idle 모션 (배경 군중과 공유하는 공식) ----------------

        /// <summary>스폰 시 개체 편차를 뽑고 현재 단계 프로필로 즉시 맞춘다.</summary>
        void ResetMotion()
        {
            _motionStage = _snapshot.Stage;
            _motionTo = ProfileFor(_motionStage);
            _motionFrom = _motionTo;
            _motionBlend = 1f;

            // id 기반이라 같은 관객은 항상 같은 개성을 갖는다 (재배치돼도 튀지 않음)
            int seed = _boundId.IsValid ? _boundId.Value : GetInstanceID();
            _variance = CrowdMotionEvaluator.MakeVariance(seed, _motionTo.speedVariance);
        }

        /// <summary>몰입도 단계가 바뀌면 프로필을 갈아탄다. 전환 중이면 지금 보이는 값에서 출발한다.</summary>
        void SetMotionStage(AudienceEngagementStage stage, bool instant)
        {
            if (_motionStage == stage && _motionTo != null && !instant) return;

            _motionStage = stage;
            _motionFrom = _motionBlend >= 1f || _motionFrom == null ? _motionTo : _motionFrom;
            _motionTo = ProfileFor(stage);
            _motionBlend = instant || motionBlendDuration <= 0f ? 1f : 0f;

            if (_motionFrom == null) _motionFrom = _motionTo;
        }

        CrowdMotionProfile ProfileFor(AudienceEngagementStage stage)
        {
            switch (stage)
            {
                case AudienceEngagementStage.Middle:  return middleMotion;
                case AudienceEngagementStage.Excited: return excitedMotion;
                default:                              return calmMotion;
            }
        }

        void ApplyTransform()
        {
            // 개체별 위상·속도 편차를 곱해 같은 공식을 써도 군무처럼 보이지 않게 한다.
            float time = (Time.time + _phase) * _variance.SpeedMultiplier;

            // 배경 군중(CrowdMemberView)·특별 관객과 완전히 같은 공식을 쓴다.
            // bob + sway + 점프(사인 아치) + 착지 스쿼시. 단계 전환은 부드럽게 섞인다.
            CrowdMotionEvaluator.EvaluateBlended(
                _motionFrom,
                _motionTo,
                _motionBlend,
                time,
                out float bob,
                out float swayAngle,
                out float squashAmount);

            float pulse = reactionPulseDuration > 0f
                ? Mathf.Sin(
                    Mathf.Clamp01(_reactionPulseRemaining / reactionPulseDuration) *
                    Mathf.PI) * reactionPulseScale
                : 0f;
            float reactionJump = loveItJumpDuration > 0f
                ? Mathf.Sin(
                    Mathf.Clamp01(_reactionJumpRemaining / loveItJumpDuration) *
                    Mathf.PI) * loveItJumpHeight
                : 0f;

            // 입장은 뒤에서 작게 다가오고, 퇴장은 뒤로 물러나며 사라진다. (기획서 §9)
            float exitProgress = _exiting ? 1f - _visibility : 0f;
            float arrivalProgress = Mathf.SmoothStep(0f, 1f, _visibility);
            Vector3 motionOffset;
            float sizeRatio;
            if (_exiting && _exitStyle == AudienceExitStyle.RivalStage)
            {
                float travel = Mathf.SmoothStep(0f, 1f, exitProgress);
                motionOffset = new Vector3(_exitDirection * travel * 0.25f,
                    travel * exitDistance * 2f, 0f);
                sizeRatio = Mathf.Lerp(0.8f, 1f, _visibility);
                transform.localRotation = Quaternion.Euler(0f, 0f, swayAngle * 0.5f);
            }
            else if (_exiting &&
                _exitStyle == AudienceExitStyle.NearbyConcert)
            {
                float travel = Mathf.Pow(exitProgress, 1.35f);
                motionOffset = new Vector3(
                    _exitDirection * crisisHorizontalTravel * travel,
                    Mathf.Sin(exitProgress * Mathf.PI * 5f) * 0.08f,
                    0f);
                sizeRatio = Mathf.Lerp(0.75f, 1f, _visibility);
                transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    -_exitDirection * exitProgress * 8f);
            }
            else
            {
                float depthOffset = _exiting
                    ? exitProgress * exitDistance
                    : (1f - arrivalProgress) * enterDistance * (_bossArrival ? 2.5f : 1f);
                float side = Mathf.Abs(_currentLayoutPosition.x) > 0.05f
                    ? Mathf.Sign(_currentLayoutPosition.x) : ((_boundId.Value & 1) == 0 ? -1f : 1f);
                motionOffset = new Vector3(side * (_exiting ? exitProgress * 0.7f : (1f - arrivalProgress) * 0.4f), depthOffset, 0f);
                sizeRatio = _exiting
                    ? Mathf.Lerp(0.75f, 1f, _visibility)
                    : Mathf.Lerp(enterStartScale, 1f, arrivalProgress);
                // 평상시에는 좌우로 기운다. (퇴장 연출은 위 분기가 자기 회전을 쓴다)
                transform.localRotation = Quaternion.Euler(0f, 0f, swayAngle * _variance.Flip);
            }

            if (_tutorialInspectionPose && !_exiting)
            {
                bob = pulse = reactionJump = squashAmount = 0f;
                // 입장 페이드와 배치 보간은 유지하되, 클릭할 위치는 움직이지 않는다.
                motionOffset = Vector3.zero;
                sizeRatio = 1f;
                transform.localRotation = Quaternion.identity;
            }

            float gestureSquash = 0f;
            float gestureRotation = 0f;
            // Compose with idle/entry/layout motion instead of competing transform tweens.
            if (!_tutorialInspectionPose && _cardMotionRemaining > 0f && _cardMotionRemaining <= cardMotionDuration && !_exiting)
            {
                float t = 1f - Mathf.Clamp01(_cardMotionRemaining / cardMotionDuration);
                float envelope = Mathf.Sin(t * Mathf.PI);
                if (_motionCardId == "open_mosh_pit")
                {
                    float spread = t < 0.28f ? Mathf.SmoothStep(0, 1, t / 0.28f)
                        : t < 0.4f ? 1f : 1f - Mathf.SmoothStep(0, 1, (t - 0.4f) / 0.35f);
                    motionOffset.x += _gestureSide * _gestureSpread * spread;
                    float leap = Mathf.Sin(Mathf.Clamp01((t - 0.4f) / 0.35f) * Mathf.PI) * 0.28f;
                    bob *= 0.15f; reactionJump = Mathf.Max(reactionJump, leap);
                    gestureRotation = -_gestureSide * envelope * 7f;
                    gestureSquash = t >= 0.7f ? Mathf.Sin(Mathf.Clamp01((t - 0.7f) / 0.3f) * Mathf.PI) * 0.05f : 0;
                }
                else if (_motionCardId == "tempo_up")
                {
                    float beat = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 4f));
                    bob *= 0.15f; reactionJump *= 0.2f; pulse *= 0.25f;
                    gestureSquash = beat * 0.045f;
                    gestureRotation = beat * _gestureSide * 4f;
                    motionOffset.x += Mathf.Sin(t * Mathf.PI * 4f) * 0.08f * envelope;
                }
                else if (_motionCardId == "hands_up")
                {
                    float leap = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI) * 0.2f;
                    bob *= 0.15f; reactionJump = Mathf.Max(reactionJump, leap);
                    float common = (Time.time - _gestureStartedAt) / Mathf.Max(0.1f, cardMotionDuration);
                    gestureRotation = Mathf.Sin(common * Mathf.PI * 4f) * 7f * envelope;
                    motionOffset.x += Mathf.Sin(common * Mathf.PI * 4f) * 0.1f * envelope;
                }
                else if (_motionCardId == "pass_mic" || _motionCardId == "response_call")
                {
                    motionOffset.y += envelope * (0.16f + 0.05f * Mathf.Sin(t * Mathf.PI * 4f + _phase));
                }
                else if (_snapshot.Preference == CrowdPreference.Chill)
                {
                    motionOffset.y += envelope * 0.06f;
                }
            }

            transform.localRotation *= Quaternion.Euler(0, 0, gestureRotation);
            squashAmount = Mathf.Clamp(squashAmount + gestureSquash, 0, 0.08f);

            transform.localPosition =
                _currentLayoutPosition +
                new Vector3(0f, bob + reactionJump, 0f) +
                motionOffset;
            // 착지 스쿼시: 눌리면 세로로 줄고 가로로 퍼진다 (점프에 무게감을 준다)
            float scale = _currentLayoutScale * sizeRatio * (1f + pulse);
            bool flipCharacter =
                allowHorizontalFlip &&
                _variance.Flip < 0f;
            characterRenderer.flipX = flipCharacter;
            transform.localScale = new Vector3(
                scale * (1f + squashAmount * 0.5f),
                scale * (1f - squashAmount),
                1f);
            // Hold the sprite's feet in place when rotating/squashing a center-pivot sprite.
            float bottom = characterRenderer.sprite != null ? characterRenderer.sprite.bounds.min.y : 0;
            Vector3 oldFoot = new Vector3(0, bottom * scale, 0);
            Vector3 newFoot = transform.localRotation * new Vector3(0, bottom * transform.localScale.y, 0);
            transform.localPosition += oldFoot - newFoot;
            warningRoot.transform.rotation = Quaternion.identity;
            reactionPopup.transform.rotation = Quaternion.identity;

            Color color = _characterColor;
            if (BossCameraDirector.RivalViewBlend > 0f)
                color = Color.Lerp(color, Color.black, BossCameraDirector.RivalViewBlend);
            // 정전 실루엣: 연출 담당이 정적 블렌드를 올리면 전 관객이 같은 검정으로 (색은 매 프레임 여기서 쓰므로 여기서 섞는다)
            if (SilhouetteBlend > 0f) color = Color.Lerp(color, SilhouetteColor, Mathf.Clamp01(SilhouetteBlend));
            color.a *= _visibility;
            characterRenderer.color = color;
        }

        /// <summary>[연출 전용] 0 = 평소, 1 = 완전 실루엣. BlackoutPresentation 이 올리고 끝나면 0 으로 되돌린다.</summary>
        public static float SilhouetteBlend { get; set; }
        public static Color SilhouetteColor { get; set; } = new Color(0.02f, 0.02f, 0.04f, 1f);
    }
}
