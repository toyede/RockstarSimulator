using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextStage
{
    /// <summary>
    /// URP Spot Light 2D의 색과 범위를 따라가는 픽셀 단위 조명 패스.
    /// 실제 Light2D는 그림자를 담당하고, 이 렌더러는 월드 픽셀 격자에서 조명을 계산한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light2D))]
    public sealed class PixelSpotlight2D : MonoBehaviour
    {
        const string ShaderResourcePath = "Lighting/PixelSpotlight2D";
        const string ShaderName = "ContextStage/Lighting/Pixel Spotlight 2D";
        const string DefaultSortingLayer = "Effects";
        const int DefaultSortingOrder = -100;

        static readonly int LightOriginId = Shader.PropertyToID("_LightOrigin");
        static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
        static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        static readonly int LightIntensityId = Shader.PropertyToID("_LightIntensity");
        static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
        static readonly int OuterRadiusId = Shader.PropertyToID("_OuterRadius");
        static readonly int InnerAngleId = Shader.PropertyToID("_InnerAngle");
        static readonly int OuterAngleId = Shader.PropertyToID("_OuterAngle");
        static readonly int PixelsPerUnitId = Shader.PropertyToID("_PixelsPerUnit");
        static readonly int BandCountId = Shader.PropertyToID("_BandCount");
        static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");

        static Mesh s_quad;

        Light2D _light;
        GameObject _visualRoot;
        MeshRenderer _renderer;
        Material _material;
        MaterialPropertyBlock _properties;

        Color _visualColor = Color.white;
        float _visualIntensity = 1f;
        float _pixelsPerUnit = 100f;
        float _bandCount = 6f;
        float _ditherStrength = 0.35f;
        string _sortingLayer = DefaultSortingLayer;
        int _sortingOrder = DefaultSortingOrder;

        public bool IsReady => _renderer != null && _material != null;

        void Awake()
        {
            _light = GetComponent<Light2D>();
            EnsureVisual();
        }

        void OnEnable()
        {
            _light ??= GetComponent<Light2D>();
            EnsureVisual();
            if (_visualRoot != null) _visualRoot.SetActive(true);
        }

        void OnDisable()
        {
            if (_visualRoot != null) _visualRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_visualRoot != null) Destroy(_visualRoot);
        }

        void LateUpdate() => ApplyProperties();

        /// <summary>픽셀 격자와 표현 품질을 구성한다.</summary>
        public void Configure(
            float pixelsPerUnit,
            int bandCount,
            float ditherStrength,
            string sortingLayer = DefaultSortingLayer,
            int sortingOrder = DefaultSortingOrder)
        {
            _pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            _bandCount = Mathf.Clamp(bandCount, 2, 16);
            _ditherStrength = Mathf.Clamp01(ditherStrength);
            _sortingLayer = string.IsNullOrWhiteSpace(sortingLayer)
                ? DefaultSortingLayer
                : sortingLayer;
            _sortingOrder = sortingOrder;

            EnsureVisual();
            ApplySorting();
            ApplyProperties();
        }

        /// <summary>원본 Light2D와 별개로 최종 픽셀 조명 색·강도를 전달한다.</summary>
        public void SetVisualLight(Color color, float intensity)
        {
            _visualColor = color;
            _visualIntensity = Mathf.Max(0f, intensity);
        }

        void EnsureVisual()
        {
            if (_renderer != null && _material != null) return;

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[PixelSpotlight] 셰이더를 찾지 못했습니다: {ShaderName}", this);
                enabled = false;
                return;
            }

            _visualRoot = new GameObject("[Runtime] Pixel Spotlight Visual")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _visualRoot.transform.SetParent(transform, false);

            var filter = _visualRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = SharedQuad;

            _renderer = _visualRoot.AddComponent<MeshRenderer>();
            _material = new Material(shader)
            {
                name = $"{name} Pixel Spotlight (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _renderer.sharedMaterial = _material;
            _properties = new MaterialPropertyBlock();
            ApplySorting();
        }

        void ApplySorting()
        {
            if (_renderer == null) return;

            _renderer.sortingLayerName = _sortingLayer;
            if (_renderer.sortingLayerID == 0 && _sortingLayer != "Default")
                _renderer.sortingLayerName = "Default";
            _renderer.sortingOrder = _sortingOrder;
        }

        void ApplyProperties()
        {
            if (_light == null || _renderer == null || _properties == null) return;
            if (_light.lightType != Light2D.LightType.Point)
            {
                _renderer.enabled = false;
                return;
            }

            float outerRadius = Mathf.Max(0.01f, _light.pointLightOuterRadius);
            float innerRadius = Mathf.Clamp(
                _light.pointLightInnerRadius,
                0f,
                outerRadius - 0.001f);
            float outerAngle = Mathf.Clamp(_light.pointLightOuterAngle, 0.1f, 360f);
            float innerAngle = Mathf.Clamp(_light.pointLightInnerAngle, 0f, outerAngle);
            Vector2 direction = transform.up;

            _visualRoot.transform.localPosition = Vector3.zero;
            _visualRoot.transform.localRotation = Quaternion.identity;
            _visualRoot.transform.localScale = new Vector3(
                outerRadius * 2f,
                outerRadius * 2f,
                1f);

            _renderer.enabled = _visualIntensity > 0.0001f && _light.enabled;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetVector(
                LightOriginId,
                new Vector4(transform.position.x, transform.position.y, 0f, 0f));
            _properties.SetVector(
                LightDirectionId,
                new Vector4(direction.x, direction.y, 0f, 0f));
            _properties.SetColor(LightColorId, _visualColor);
            _properties.SetFloat(LightIntensityId, _visualIntensity);
            _properties.SetFloat(InnerRadiusId, innerRadius);
            _properties.SetFloat(OuterRadiusId, outerRadius);
            _properties.SetFloat(InnerAngleId, innerAngle);
            _properties.SetFloat(OuterAngleId, outerAngle);
            _properties.SetFloat(PixelsPerUnitId, _pixelsPerUnit);
            _properties.SetFloat(BandCountId, _bandCount);
            _properties.SetFloat(DitherStrengthId, _ditherStrength);
            _renderer.SetPropertyBlock(_properties);
        }

        static Mesh SharedQuad
        {
            get
            {
                if (s_quad != null) return s_quad;

                s_quad = new Mesh
                {
                    name = "Pixel Spotlight Quad",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f),
                        new Vector3( 0.5f, -0.5f, 0f),
                        new Vector3( 0.5f,  0.5f, 0f),
                        new Vector3(-0.5f,  0.5f, 0f),
                    },
                    triangles = new[] { 0, 2, 1, 0, 3, 2 },
                };
                s_quad.RecalculateBounds();
                return s_quad;
            }
        }
    }
}
