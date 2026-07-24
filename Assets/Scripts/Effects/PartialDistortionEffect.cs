using System.Collections;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Renderer2D의 Camera Sorting Layer Texture를 도형 영역 안에서만 굴절시킨다.
    /// 인스턴스별 값은 MaterialPropertyBlock으로 전달하므로 공용 머티리얼을 복제하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PartialDistortionEffect : MonoBehaviour
    {
        [Header("프리셋")]
        [SerializeField] DistortionProfile profile;
        [SerializeField] Material sourceMaterial;

        [Header("영역")]
        [SerializeField] Vector2 areaSize = new Vector2(5f, 3.5f);

        [Header("데모 재생")]
        [SerializeField] bool playOnEnable = true;
        [SerializeField] bool loop;
        [SerializeField, Min(0f)] float loopDelay = 0.35f;
        [SerializeField] bool useUnscaledTime = true;

        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Material _runtimeMaterial;
        MaterialPropertyBlock _properties;
        Coroutine _routine;
        bool _initialized;

        public bool IsPlaying => _routine != null;

        void Awake() => Initialize();

        void OnEnable()
        {
            Initialize();
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        void OnDestroy()
        {
            if (_runtimeMaterial == null) return;
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
        }

        public void Play()
        {
            Initialize();
            if (profile == null || !isActiveAndEnabled) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Apply(0f, 0f);
        }

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter.sharedMesh = LocalScreenEffectShader.SharedQuad;

            if (sourceMaterial != null)
            {
                _meshRenderer.sharedMaterial = sourceMaterial;
            }
            else
            {
                Shader shader = Shader.Find(LocalScreenEffectShader.ShaderName);
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader)
                    {
                        name = "PartialDistortion (Runtime)",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    _meshRenderer.sharedMaterial = _runtimeMaterial;
                }
                else
                {
                    Debug.LogError(
                        $"[ScreenEffects] 셰이더를 찾지 못했습니다: {LocalScreenEffectShader.ShaderName}",
                        this);
                }
            }

            _meshRenderer.sortingLayerName = LocalScreenEffectShader.SortingLayerName;
            _meshRenderer.sortingOrder = 0;
            transform.localScale = new Vector3(
                Mathf.Max(0.01f, areaSize.x),
                Mathf.Max(0.01f, areaSize.y),
                1f);

            _properties = new MaterialPropertyBlock();
            Apply(0f, 0f);
        }

        IEnumerator PlayRoutine()
        {
            do
            {
                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, profile.Duration);

                while (elapsed < duration)
                {
                    float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    elapsed += delta;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    Apply(progress, elapsed);
                    yield return null;
                }

                Apply(1f, duration);
                if (!loop) break;

                float wait = 0f;
                while (wait < loopDelay)
                {
                    wait += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }
            while (loop);

            _routine = null;
        }

        void Apply(float progress, float effectTime)
        {
            if (_meshRenderer == null || profile == null) return;

            LocalScreenEffectShader.Apply(_meshRenderer, _properties, profile, progress, effectTime);
        }
    }
}
