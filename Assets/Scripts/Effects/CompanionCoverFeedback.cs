using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class CompanionCoverFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer[] companions;
        [SerializeField] private Transform raccoon;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private RectTransform comboTarget;
        [SerializeField] private HudNumberFeedback comboFeedback;
        [Header("Companion motion")]
        [SerializeField, Min(0.1f)] private float poseDuration = 0.48f;
        [SerializeField, Range(0f, 0.3f)] private float squash = 0.1f;
        [SerializeField, Range(0f, 0.4f)] private float stretch = 0.18f;
        [SerializeField, Range(0f, 10f)] private float leanDegrees = 4f;
        [Header("Light and combo")]
        [SerializeField, Min(0f)] private float launchDelay = 0.18f;
        [SerializeField, Min(0.1f)] private float flightDuration = 0.58f;
        [SerializeField, Min(0.1f)] private float impactDuration = 0.42f;
        [SerializeField] private Vector2 instrumentOffset = new Vector2(0.1f, -0.05f);
        [SerializeField] private Color coverColor = new Color32(100, 218, 255, 255);
        [SerializeField, Range(0f, 1f)] private float borderStrength = 0.8f;
        [SerializeField] private string coverMessage = "COVER!";

        CompanionCoverGraphic _graphic;
        struct CompanionPose
        {
            public SpriteRenderer Source;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public Vector2 LaunchScreen;
        }
        readonly CompanionPose[] _poses = new CompanionPose[2];
        int _sourceCount;
        float _age = -1f;
        int _pending;
        bool _launched, _arrived;

        void OnEnable()
        {
            EventBus.Subscribe<ComboLossPrevented>(OnProtected);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<GameStateChanged>(OnStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboLossPrevented>(OnProtected);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnStateChanged);
            Clear();
        }

        void OnDestroy() { if (_graphic != null) Destroy(_graphic.gameObject); }

        void OnProtected(ComboLossPrevented e)
        {
            if (e.ProtectedCombo <= 0 || comboTarget == null || comboFeedback == null) return;
            if (_age >= 0f) { _pending++; return; }
            Begin();
        }

        void Begin()
        {
            _sourceCount = 0;
            if (companions != null)
                for (int i = 0; i < companions.Length && _sourceCount < _poses.Length; i++)
                {
                    var candidate = companions[i];
                    if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                        candidate.transform == raccoon || candidate.GetComponent<RaccoonAnimator>() != null) continue;
                    if (_sourceCount > 0 && _poses[0].Source == candidate) continue;
                    var pose = candidate.transform;
                    _poses[_sourceCount++] = new CompanionPose { Source = candidate,
                        Position = pose.localPosition, Scale = pose.localScale, Rotation = pose.localRotation };
                }
            if (_sourceCount == 0 || stageCamera == null)
            {
                _sourceCount = 0;
                _pending = 0;
                comboFeedback.PlayMessage(coverMessage, coverColor);
                return;
            }
            if (_graphic == null) _graphic = CompanionCoverGraphic.Create(comboTarget);
            _age = 0f;
            _launched = _arrived = false;
        }

        void LateUpdate()
        {
            if (_age < 0f || (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused)) return;
            for (int i = 0; i < _sourceCount; i++)
                if (_poses[i].Source == null || !_poses[i].Source.gameObject.activeInHierarchy) { Clear(); return; }
            _age += Time.unscaledDeltaTime;
            for (int i = 0; i < _sourceCount; i++) ApplyPose(_poses[i]);
            float travel = -1f, impact = -1f;
            if (_age >= launchDelay)
            {
                if (!_launched)
                {
                    for (int i = 0; i < _sourceCount; i++)
                    {
                        Bounds bounds = _poses[i].Source.bounds;
                        Vector3 origin = bounds.center + new Vector3(bounds.size.x * instrumentOffset.x,
                            bounds.size.y * instrumentOffset.y, 0f);
                        _poses[i].LaunchScreen = stageCamera.WorldToScreenPoint(origin);
                    }
                    _launched = true;
                }
                travel = (_age - launchDelay) / Mathf.Max(0.1f, flightDuration);
                impact = travel < 1f ? -1f :
                    (_age - launchDelay - flightDuration) / Mathf.Max(0.1f, impactDuration);
                if (!_arrived && travel >= 1f)
                {
                    comboFeedback.PlayMessage(coverMessage, coverColor);
                    _arrived = true;
                }
            }
            float glow = impact < 0f ? 0.65f * Mathf.Clamp01(_age / 0.08f) :
                (0.65f + 0.35f * Mathf.Sin(Mathf.Clamp01(impact) * Mathf.PI)) * (1f - Mathf.Clamp01(impact));
            if (_graphic != null) _graphic.Show(_poses[0].LaunchScreen,
                _poses[_sourceCount - 1].LaunchScreen, _sourceCount > 1,
                comboTarget, travel, impact, coverColor, glow * borderStrength);
            if (_age >= Mathf.Max(poseDuration, launchDelay + flightDuration + impactDuration))
            {
                RestorePoses();
                _sourceCount = 0;
                _age = -1f;
                if (_graphic != null) _graphic.Clear();
                if (_pending > 0) { _pending--; Begin(); }
            }
        }

        void ApplyPose(CompanionPose original)
        {
            RestorePose(original);
            var source = original.Source;
            if (source.sprite == null || _age >= poseDuration) return;
            float t = Mathf.Clamp01(_age / Mathf.Max(0.1f, poseDuration));
            float y;
            if (t < 0.2f) y = -squash * Mathf.Sin(t / 0.2f * Mathf.PI * 0.5f);
            else if (t < 0.4f) y = Mathf.Lerp(-squash, stretch, Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.2f));
            else
            {
                float settle = (t - 0.4f) / 0.6f;
                y = stretch * Mathf.Cos(settle * Mathf.PI * 2f) * Mathf.Pow(1f - settle, 2f);
            }
            var pose = source.transform;
            var bounds = source.sprite.bounds;
            Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, 0f);
            if (source.flipX) foot.x = -foot.x;
            if (source.flipY) foot.y = -foot.y;
            Vector3 footWorld = pose.TransformPoint(foot);
            float direction = raccoon == null ? 1f : Mathf.Sign(raccoon.position.x - pose.position.x);
            pose.localScale = Vector3.Scale(original.Scale, new Vector3(1f - y * 0.45f, 1f + y, 1f));
            pose.localRotation = original.Rotation * Quaternion.Euler(0f, 0f,
                -direction * leanDegrees * Mathf.Sin(t * Mathf.PI));
            pose.position += footWorld - pose.TransformPoint(foot);
        }

        static void RestorePose(CompanionPose original)
        {
            if (original.Source == null) return;
            original.Source.transform.localPosition = original.Position;
            original.Source.transform.localScale = original.Scale;
            original.Source.transform.localRotation = original.Rotation;
        }

        void RestorePoses() { for (int i = 0; i < _sourceCount; i++) RestorePose(_poses[i]); }

        void Clear()
        {
            RestorePoses();
            _sourceCount = 0;
            _age = -1f;
            _pending = 0;
            if (_graphic != null) _graphic.Clear();
        }

        void OnComboChanged(ComboChanged e) { if (e.CurrentCombo <= 0) Clear(); }
        void OnStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
        }
    }
}
