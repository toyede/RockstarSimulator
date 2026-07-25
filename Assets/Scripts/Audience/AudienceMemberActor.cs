using System;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
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

        [Header("Departure Warning")]
        [SerializeField] GameObject warningRoot;
        [SerializeField] Transform warningFill;
        [SerializeField] SpriteRenderer warningBackgroundRenderer;
        [SerializeField] SpriteRenderer warningFillRenderer;

        [Header("Motion")]
        [SerializeField, Min(0f)] float enterDuration = 0.35f;
        [SerializeField, Min(0f)] float exitDuration = 0.45f;
        [SerializeField, Min(0f)] float reactionPulseDuration = 0.22f;
        [SerializeField, Min(0f)] float reactionPulseScale = 0.18f;
        [SerializeField, Min(0f)] float calmBobHeight = 0.025f;
        [SerializeField, Min(0f)] float middleBobHeight = 0.07f;
        [SerializeField, Min(0f)] float excitedBobHeight = 0.16f;

        AudienceSnapshot _snapshot;
        AudienceId _boundId;
        Vector3 _layoutPosition;
        float _layoutScale = 1f;
        float _calmUpperBound = 33f;
        float _phase;
        float _visibility;
        float _reactionPulseRemaining;
        float _exitElapsed;
        bool _exiting;
        Color _characterColor = Color.white;
        Action _exitCompleted;

        public AudienceId BoundId => _boundId;
        public AudienceSnapshot Snapshot => _snapshot;
        public bool IsBound => _boundId.IsValid;

        void Awake()
        {
            if (characterRenderer == null ||
                chillSprite == null ||
                singalongSprite == null ||
                moshSprite == null ||
                warningRoot == null ||
                warningFill == null ||
                warningBackgroundRenderer == null ||
                warningFillRenderer == null)
            {
                Debug.LogError(
                    "[AudienceMemberActor] Prefab references are incomplete.",
                    this);
                enabled = false;
            }
        }

        public void OnSpawned()
        {
            _snapshot = default;
            _boundId = default;
            _visibility = 0f;
            _reactionPulseRemaining = 0f;
            _exitElapsed = 0f;
            _exiting = false;
            _exitCompleted = null;
            _phase = UnityEngine.Random.value * Mathf.PI * 2f;
            if (warningRoot != null) warningRoot.SetActive(false);
            ApplyTransform();
        }

        public void OnDespawned()
        {
            _snapshot = default;
            _boundId = default;
            _exitCompleted = null;
            _exiting = false;
            if (warningRoot != null) warningRoot.SetActive(false);
        }

        public void Bind(AudienceSnapshot snapshot, float calmUpperBound)
        {
            if (!snapshot.Id.IsValid)
                throw new ArgumentException("Audience snapshot requires a valid id.", nameof(snapshot));

            _boundId = snapshot.Id;
            _calmUpperBound = Mathf.Max(0.01f, calmUpperBound);
            _visibility = 0f;
            _exiting = false;
            _exitElapsed = 0f;
            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(AudienceSnapshot snapshot)
        {
            if (!IsBound || snapshot.Id != _boundId) return;

            bool preferenceChanged = snapshot.Preference != _snapshot.Preference;
            _snapshot = snapshot;
            if (preferenceChanged || characterRenderer.sprite == null)
                ApplyPreference(snapshot.Preference);
            UpdateWarning();
        }

        public void SetLayout(Vector3 localPosition, float scale, int sortingOrder)
        {
            _layoutPosition = localPosition;
            _layoutScale = Mathf.Max(0.01f, scale);
            characterRenderer.sortingOrder = sortingOrder;
            warningBackgroundRenderer.sortingOrder = sortingOrder + 20;
            warningFillRenderer.sortingOrder = sortingOrder + 21;
            ApplyTransform();
        }

        public void PlayReaction(int reactionValue)
        {
            if (reactionValue <= 0 || _exiting) return;
            _reactionPulseRemaining = reactionPulseDuration;
        }

        public void PlayExit(Action completed)
        {
            if (_exiting) return;
            _exiting = true;
            _exitElapsed = 0f;
            _exitCompleted = completed;
            if (warningRoot != null) warningRoot.SetActive(false);
        }

        void Update()
        {
            if (!IsBound) return;

            float deltaTime = Time.deltaTime;
            if (_exiting)
            {
                _exitElapsed += deltaTime;
                float duration = Mathf.Max(0.01f, exitDuration);
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

        void ApplyPreference(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill:
                    characterRenderer.sprite = chillSprite;
                    _characterColor = chillColor;
                    break;
                case CrowdPreference.Singalong:
                    characterRenderer.sprite = singalongSprite;
                    _characterColor = singalongColor;
                    break;
                case CrowdPreference.Mosh:
                    characterRenderer.sprite = moshSprite;
                    _characterColor = moshColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
            }
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
            float exitOffset = _exiting ? (1f - _visibility) * 0.8f : 0f;

            transform.localPosition =
                _layoutPosition + new Vector3(0f, bob + exitOffset, 0f);
            transform.localScale =
                Vector3.one * (_layoutScale * _visibility * (1f + pulse));

            Color color = _characterColor;
            color.a *= _visibility;
            characterRenderer.color = color;
        }
    }
}
