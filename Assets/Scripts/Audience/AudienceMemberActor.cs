using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>(성향, 참여도 단계) 한 조합에 대한 동물별 애니메이션 세트.</summary>
    [Serializable]
    public struct AnimatedVariantGroup
    {
        public CrowdPreference preference;
        public AudienceEngagementStage stage;
        public SpriteAnimationClip[] variants;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
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
        [Tooltip("아트가 준비된 (성향, 단계) 조합만 채우면 된다. 목록에 없는 조합은 정지 스프라이트로 자동 폴백한다.\n" +
                 "Tools/Audience/Import Animated Species Sheets 로 채운다.")]
        [SerializeField] List<AnimatedVariantGroup> animatedVariants = new List<AnimatedVariantGroup>();

        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();

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
        [SerializeField, Min(0f)] float calmBobHeight = 0.025f;
        [SerializeField, Min(0f)] float middleBobHeight = 0.07f;
        [SerializeField, Min(0f)] float excitedBobHeight = 0.16f;

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
        float _visibility;
        float _reactionPulseRemaining;
        float _exitElapsed;
        bool _exiting;
        AudienceExitStyle _exitStyle;
        float _exitDirection = 1f;
        Color _characterColor = Color.white;
        Action _exitCompleted;

        public AudienceId BoundId => _boundId;
        public AudienceSnapshot Snapshot => _snapshot;
        public bool IsBound => _boundId.IsValid;
        public AudienceReactionPopup ReactionPopup => reactionPopup;
        public Vector3 LayoutPosition => _currentLayoutPosition;
        public float LayoutScale => _currentLayoutScale;
        public int SortingOrder =>
            characterRenderer != null ? characterRenderer.sortingOrder : 0;

        void Awake()
        {
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
            _player.Bind(characterRenderer);
        }

        public void OnSpawned()
        {
            _snapshot = default;
            _boundId = default;
            _visibility = 0f;
            _reactionPulseRemaining = 0f;
            _exitElapsed = 0f;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            _exitCompleted = null;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            _hasLayout = false;
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);
            if (reactionPopup != null) reactionPopup.ResetVisual();
            ApplyTransform();
        }

        public void OnDespawned()
        {
            _snapshot = default;
            _boundId = default;
            _exitCompleted = null;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);
            if (reactionPopup != null) reactionPopup.ResetVisual();
        }

        public void Bind(AudienceSnapshot snapshot, float calmUpperBound)
        {
            if (!snapshot.Id.IsValid)
                throw new ArgumentException("Audience snapshot requires a valid id.", nameof(snapshot));

            _boundId = snapshot.Id;
            _calmUpperBound = Mathf.Max(0.01f, calmUpperBound);
            _visibility = 0f;
            _exiting = false;
            _exitStyle = AudienceExitStyle.Default;
            _exitElapsed = 0f;
            _hasLayout = false;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(AudienceSnapshot snapshot)
        {
            if (!IsBound || snapshot.Id != _boundId) return;

            bool preferenceChanged = snapshot.Preference != _snapshot.Preference;
            bool stageChanged = snapshot.Stage != _snapshot.Stage;
            _snapshot = snapshot;
            if (preferenceChanged || stageChanged || characterRenderer.sprite == null)
                ApplyVisual(snapshot.Preference, snapshot.Stage);
            UpdateWarning();
        }

        public void SetLayout(Vector3 localPosition, float scale, int sortingOrder)
        {
            _layoutPosition = localPosition;
            _layoutScale = Mathf.Max(0.01f, scale);
            characterRenderer.sortingOrder = sortingOrder;
            warningBackgroundRenderer.sortingOrder = sortingOrder + 20;
            warningFillRenderer.sortingOrder = sortingOrder + 21;
            reactionPopup.SetSortingOrder(sortingOrder + 30);

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
            if (_exiting || !enabled) return;

            // 단일 결과 UI 원칙: 관객 반응은 애니메이션(펄스)으로만 구분한다.
            // 캐릭터 위 "LOVE IT +25" 같은 점수 텍스트는 우측 상단 HUD 로 일원화했으므로 띄우지 않는다.
            // (reactionPopup 은 다른 경로가 참조할 수 있어 컴포넌트 자체는 남겨둔다)
            if (reactionValue > 0)
                _reactionPulseRemaining = reactionPulseDuration;
        }

        public void PlayDeparture()
        {
            // 이탈은 캐릭터가 나가는 모습(PlayExit)으로 확인한다.
            // "LEFT THE SHOW" 텍스트는 단일 UI 원칙에 따라 표시하지 않는다.
        }

        public void PlayExit(Action completed)
            => PlayExit(AudienceExitStyle.Default, completed);

        public void PlayExit(
            AudienceExitStyle style,
            Action completed)
        {
            if (_exiting) return;
            _exiting = true;
            _exitStyle = style;
            _exitDirection = Mathf.Abs(_currentLayoutPosition.x) > 0.05f
                ? Mathf.Sign(_currentLayoutPosition.x)
                : (_boundId.Value & 1) == 0 ? -1f : 1f;
            _exitElapsed = 0f;
            _exitCompleted = completed;
            if (warningRoot != null) warningRoot.SetActive(false);
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(false);
        }

        public void SetCrisisThreatened(bool threatened)
        {
            if (crisisWarningRoot != null)
                crisisWarningRoot.SetActive(threatened && !_exiting);
        }

        void Update()
        {
            if (!IsBound) return;

            float deltaTime = Time.deltaTime;
            _player.Tick(deltaTime);

            float followStep = layoutFollowSpeed * deltaTime;
            _currentLayoutPosition = Vector3.MoveTowards(_currentLayoutPosition, _layoutPosition, followStep);
            _currentLayoutScale = Mathf.MoveTowards(_currentLayoutScale, _layoutScale, followStep);

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
                Action completed = _exitCompleted;
                _exitCompleted = null;
                _boundId = default;
                completed?.Invoke();
                return;
            }

            float enterSpeed = enterDuration > 0f ? deltaTime / enterDuration : 1f;
            _visibility = Mathf.MoveTowards(_visibility, 1f, enterSpeed);
            _reactionPulseRemaining = Mathf.Max(0f, _reactionPulseRemaining - deltaTime);
            ApplyTransform();
        }

        void ApplyVisual(CrowdPreference preference, AudienceEngagementStage stage)
        {
            _characterColor = ColorFor(preference);

            SpriteAnimationClip clip = AnimationVariantFor(preference, stage);
            if (clip != null && clip.IsValid)
            {
                _player.Play(clip, restart: true);
                return;
            }

            _player.Stop();
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

        /// <summary>같은 관객은 항상 같은 동물 variant 를 쓰도록 id 로 결정한다.</summary>
        SpriteAnimationClip AnimationVariantFor(CrowdPreference preference, AudienceEngagementStage stage)
        {
            for (int i = 0; i < animatedVariants.Count; i++)
            {
                AnimatedVariantGroup group = animatedVariants[i];
                if (group.preference != preference || group.stage != stage) continue;

                SpriteAnimationClip[] variants = group.variants;
                if (variants == null || variants.Length == 0) return null;
                return variants[Mod(_boundId.Value, variants.Length)];
            }
            return null;
        }

        static int Mod(int value, int length) => length <= 0 ? 0 : ((value % length) + length) % length;

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

        void ApplyTransform()
        {
            float time = Time.time + _phase;
            float bobHeight;
            float bobSpeed;
            switch (_snapshot.Stage)
            {
                case AudienceEngagementStage.Calm:
                    bobHeight = calmBobHeight;
                    bobSpeed = 1.4f;
                    break;
                case AudienceEngagementStage.Middle:
                    bobHeight = middleBobHeight;
                    bobSpeed = 2.4f;
                    break;
                case AudienceEngagementStage.Excited:
                    bobHeight = excitedBobHeight;
                    bobSpeed = 3.4f;
                    break;
                default:
                    bobHeight = 0f;
                    bobSpeed = 0f;
                    break;
            }

            float bob = Mathf.Abs(Mathf.Sin(time * bobSpeed)) * bobHeight;
            float pulse = reactionPulseDuration > 0f
                ? Mathf.Sin(
                    Mathf.Clamp01(_reactionPulseRemaining / reactionPulseDuration) *
                    Mathf.PI) * reactionPulseScale
                : 0f;

            // 입장은 뒤에서 작게 다가오고, 퇴장은 뒤로 물러나며 사라진다. (기획서 §9)
            float exitProgress = _exiting ? 1f - _visibility : 0f;
            Vector3 motionOffset;
            float sizeRatio;
            if (_exiting &&
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
                    : (1f - _visibility) * enterDistance;
                motionOffset = new Vector3(0f, depthOffset, 0f);
                sizeRatio = _exiting
                    ? _visibility
                    : Mathf.Lerp(enterStartScale, 1f, _visibility);
                transform.localRotation = Quaternion.identity;
            }

            transform.localPosition =
                _currentLayoutPosition +
                new Vector3(0f, bob, 0f) +
                motionOffset;
            transform.localScale =
                Vector3.one * (_currentLayoutScale * sizeRatio * (1f + pulse));

            Color color = _characterColor;
            color.a *= _visibility;
            characterRenderer.color = color;
        }
    }
}
