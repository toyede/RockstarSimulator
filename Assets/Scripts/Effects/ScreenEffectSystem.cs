using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>재생 중인 로컬 화면 이펙트를 제어하는 가벼운 핸들.</summary>
    public readonly struct ScreenEffectHandle
    {
        readonly LocalScreenEffectInstance _instance;
        readonly int _generation;

        internal ScreenEffectHandle(LocalScreenEffectInstance instance, int generation)
        {
            _instance = instance;
            _generation = generation;
        }

        public bool IsValid => _instance != null && _instance.IsGenerationActive(_generation);
        public void Stop() => _instance?.Stop(_generation);
        public void SetPosition(Vector3 worldPosition) =>
            _instance?.SetPosition(_generation, worldPosition);
        public void SetStrength(float multiplier) =>
            _instance?.SetStrength(_generation, multiplier);
    }

    /// <summary>프로필을 언제, 어디에, 어느 크기로 재생할지 지정한다.</summary>
    public readonly struct ScreenEffectRequest
    {
        public ScreenEffectRequest(
            LocalScreenEffectProfile profile,
            Vector3 worldPosition,
            Vector2 areaSize,
            float strengthMultiplier = 1f,
            float durationOverride = 0f,
            Transform followTarget = null,
            bool useUnscaledTime = true)
        {
            Profile = profile;
            WorldPosition = worldPosition;
            AreaSize = areaSize;
            StrengthMultiplier = strengthMultiplier;
            DurationOverride = durationOverride;
            FollowTarget = followTarget;
            UseUnscaledTime = useUnscaledTime;
        }

        public LocalScreenEffectProfile Profile { get; }
        public Vector3 WorldPosition { get; }
        public Vector2 AreaSize { get; }
        public float StrengthMultiplier { get; }
        public float DurationOverride { get; }
        public Transform FollowTarget { get; }
        public bool UseUnscaledTime { get; }
    }

    /// <summary>
    /// 로컬 화면 이펙트의 생성·재사용·좌표 변환을 담당한다.
    /// 팀 코드는 직접 참조 대신 ScreenEffects 파사드를 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenEffectSystem : MonoSingleton<ScreenEffectSystem>
    {
        const int DefaultPrewarmCount = 6;
        const float BuiltinDistortionStrength = 0.04f;
        const float BuiltinDistortionDuration = 0.7f;

        Material _sharedMaterial;
        GameObject _instancePrefab;
        LocalScreenEffectProfile _builtinDistortion;
        bool _ready;

        protected override void OnAwake()
        {
            Shader shader = Shader.Find(LocalScreenEffectShader.ShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[ScreenEffects] 셰이더를 찾지 못했습니다: {LocalScreenEffectShader.ShaderName}",
                    this);
                return;
            }

            _sharedMaterial = new Material(shader)
            {
                name = "Local Screen Effect (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };

            _builtinDistortion = ScriptableObject.CreateInstance<LocalScreenEffectProfile>();
            _builtinDistortion.name = "Builtin Ring Distortion";
            _builtinDistortion.hideFlags = HideFlags.HideAndDontSave;
            _builtinDistortion.ConfigureAsRuntimeDistortion(
                BuiltinDistortionStrength,
                BuiltinDistortionDuration);

            _instancePrefab = CreateRuntimePrefab();
            PoolManager.Prewarm(_instancePrefab, DefaultPrewarmCount);
            _ready = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_sharedMaterial != null) Destroy(_sharedMaterial);
            if (_builtinDistortion != null) Destroy(_builtinDistortion);
        }

        public ScreenEffectHandle Play(ScreenEffectRequest request)
        {
            if (!_ready || request.Profile == null) return default;

            Vector2 areaSize = new Vector2(
                Mathf.Max(0.01f, request.AreaSize.x),
                Mathf.Max(0.01f, request.AreaSize.y));

            var spawned = PoolManager.Spawn(
                _instancePrefab,
                request.WorldPosition,
                Quaternion.identity);
            var instance = spawned != null
                ? spawned.GetComponent<LocalScreenEffectInstance>()
                : null;
            if (instance == null) return default;

            int generation = instance.Configure(request, areaSize);
            return new ScreenEffectHandle(instance, generation);
        }

        public ScreenEffectHandle PlayBuiltinDistortion(
            Vector3 worldPosition,
            float radius,
            float strength,
            float duration)
        {
            float diameter = Mathf.Max(0.01f, radius * 2f);
            float multiplier = strength / BuiltinDistortionStrength;
            return Play(new ScreenEffectRequest(
                _builtinDistortion,
                worldPosition,
                new Vector2(diameter, diameter),
                multiplier,
                Mathf.Max(0.01f, duration)));
        }

        public ScreenEffectHandle PlayAtScreenPosition(
            LocalScreenEffectProfile profile,
            Vector2 screenPosition,
            float radiusPixels,
            Camera camera = null,
            float strengthMultiplier = 1f,
            float durationOverride = 0f)
        {
            camera = camera != null ? camera : Camera.main;
            if (camera == null || profile == null) return default;

            const float planeZ = 0f;
            float depth = Mathf.Abs(planeZ - camera.transform.position.z);
            Vector3 center = camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
            Vector3 edge = camera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x + Mathf.Max(1f, radiusPixels),
                    screenPosition.y,
                    depth));
            float radiusWorld = Vector3.Distance(center, edge);
            center.z = planeZ;

            return Play(new ScreenEffectRequest(
                profile,
                center,
                Vector2.one * (radiusWorld * 2f),
                strengthMultiplier,
                durationOverride));
        }

        GameObject CreateRuntimePrefab()
        {
            var prefab = new GameObject("[Runtime] Local Screen Effect");
            prefab.SetActive(false);
            prefab.transform.SetParent(transform, false);
            prefab.hideFlags = HideFlags.HideAndDontSave;

            var filter = prefab.AddComponent<MeshFilter>();
            filter.sharedMesh = LocalScreenEffectShader.SharedQuad;

            var renderer = prefab.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _sharedMaterial;
            renderer.sortingLayerName = LocalScreenEffectShader.SortingLayerName;
            renderer.sortingOrder = 0;

            prefab.AddComponent<LocalScreenEffectInstance>();
            return prefab;
        }
    }

    /// <summary>팀에서 사용하는 간단한 정적 진입점.</summary>
    public static class ScreenEffects
    {
        public static ScreenEffectHandle Play(
            LocalScreenEffectProfile profile,
            Vector3 worldPosition,
            Vector2 areaSize,
            float strengthMultiplier = 1f,
            float durationOverride = 0f)
            => ScreenEffectSystem.Instance.Play(new ScreenEffectRequest(
                profile,
                worldPosition,
                areaSize,
                strengthMultiplier,
                durationOverride));

        public static ScreenEffectHandle PlayAttached(
            LocalScreenEffectProfile profile,
            Transform target,
            Vector2 areaSize,
            float strengthMultiplier = 1f,
            float durationOverride = 0f)
        {
            if (target == null) return default;
            return ScreenEffectSystem.Instance.Play(new ScreenEffectRequest(
                profile,
                target.position,
                areaSize,
                strengthMultiplier,
                durationOverride,
                target));
        }

        public static ScreenEffectHandle PlayDistortion(
            Vector3 worldPosition,
            float radius = 1.6f,
            float strength = 0.04f,
            float duration = 0.7f)
            => ScreenEffectSystem.Instance.PlayBuiltinDistortion(
                worldPosition,
                radius,
                strength,
                duration);

        public static ScreenEffectHandle PlayScreen(
            LocalScreenEffectProfile profile,
            Vector2 screenPosition,
            float radiusPixels,
            Camera camera = null,
            float strengthMultiplier = 1f,
            float durationOverride = 0f)
            => ScreenEffectSystem.Instance.PlayAtScreenPosition(
                profile,
                screenPosition,
                radiusPixels,
                camera,
                strengthMultiplier,
                durationOverride);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    internal sealed class LocalScreenEffectInstance : MonoBehaviour, IPoolable
    {
        MeshRenderer _renderer;
        MaterialPropertyBlock _properties;
        LocalScreenEffectProfile _profile;
        Transform _followTarget;
        float _elapsed;
        float _duration;
        float _strengthMultiplier;
        bool _useUnscaledTime;
        bool _active;
        int _generation;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
        }

        public int Configure(ScreenEffectRequest request, Vector2 areaSize)
        {
            _generation++;
            _profile = request.Profile;
            _followTarget = request.FollowTarget;
            _elapsed = 0f;
            _duration = request.DurationOverride > 0f
                ? request.DurationOverride
                : Mathf.Max(0.01f, request.Profile.Duration);
            _strengthMultiplier = Mathf.Max(0f, request.StrengthMultiplier);
            _useUnscaledTime = request.UseUnscaledTime;
            _active = true;

            transform.position = request.WorldPosition;
            transform.localScale = new Vector3(areaSize.x, areaSize.y, 1f);
            _renderer.enabled = true;
            LocalScreenEffectShader.Apply(
                _renderer,
                _properties,
                _profile,
                0f,
                0f,
                _strengthMultiplier);
            return _generation;
        }

        void LateUpdate()
        {
            if (!_active || _profile == null) return;
            if (_followTarget != null) transform.position = _followTarget.position;

            _elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            LocalScreenEffectShader.Apply(
                _renderer,
                _properties,
                _profile,
                progress,
                _elapsed,
                _strengthMultiplier);

            if (_elapsed >= _duration) PoolManager.Despawn(gameObject);
        }

        public bool IsGenerationActive(int generation) => _active && _generation == generation;

        public void Stop(int generation)
        {
            if (!IsGenerationActive(generation)) return;
            PoolManager.Despawn(gameObject);
        }

        public void SetPosition(int generation, Vector3 worldPosition)
        {
            if (!IsGenerationActive(generation)) return;
            _followTarget = null;
            transform.position = worldPosition;
        }

        public void SetStrength(int generation, float multiplier)
        {
            if (!IsGenerationActive(generation)) return;
            _strengthMultiplier = Mathf.Max(0f, multiplier);
        }

        public void OnSpawned()
        {
            _active = false;
            if (_renderer != null) _renderer.enabled = false;
        }

        public void OnDespawned()
        {
            _active = false;
            _profile = null;
            _followTarget = null;
            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
                _renderer.enabled = false;
            }
        }
    }
}
