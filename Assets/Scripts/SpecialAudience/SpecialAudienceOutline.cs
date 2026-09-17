using UnityEngine;
using UnityEngine.Rendering;

namespace ContextStage
{
    public enum AudienceOutlineMode
    {
        HoverOverlay,
        Occluded
    }

    /// <summary>
    /// 본체 SpriteRenderer의 현재 프레임과 정렬 상태를 따라가는 외곽선 전용 표현.
    /// 머티리얼은 런타임에 한 번만 만들며, 원본 스프라이트 내부는 그리지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpecialAudienceOutline : MonoBehaviour
    {
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int OutlineAlphaCutoffId = Shader.PropertyToID("_OutlineAlphaCutoff");
        static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUVRect");

        const string ShaderName = "ContextStage/Special Audience Outline";

        [SerializeField] Shader outlineShader;

        SpriteRenderer _sourceRenderer;
        SpriteRenderer _outlineRenderer;
        Material _outlineMaterial;
        MaterialPropertyBlock _properties;
        Color _outlineColor = Color.white;
        float _outlineThickness = 2f;
        AudienceOutlineMode _mode;
        SortingGroup _sortingGroup;
        SpriteRenderer _groupedBody;
        MaterialPropertyBlock _bodyProperties;
        bool _sourceSuppressed;
        bool _originalForceRenderingOff;
        static AudienceOutlineRegions _regions;
        static bool _regionsLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegions()
        {
            _regions = null;
            _regionsLoaded = false;
        }

        public void Configure(
            SpriteRenderer sourceRenderer,
            Color outlineColor,
            float outlineThickness,
            AudienceOutlineMode mode = AudienceOutlineMode.HoverOverlay)
        {
            if (_sourceRenderer != sourceRenderer || mode != AudienceOutlineMode.Occluded)
                RestoreSource();
            _sourceRenderer = sourceRenderer;
            _outlineRenderer = GetComponent<SpriteRenderer>();
            _outlineColor = outlineColor;
            _outlineThickness = Mathf.Max(0f, outlineThickness);
            _mode = mode;

            EnsureMaterial();
            SyncRenderer();
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                RestoreSource();
                _mode = AudienceOutlineMode.HoverOverlay;
            }
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);

            if (visible)
                SyncRenderer();
        }

        void LateUpdate()
        {
            SyncRenderer();
        }

        void OnDisable()
        {
            RestoreSource();
            _mode = AudienceOutlineMode.HoverOverlay;
        }

        void OnDestroy()
        {
            RestoreSource();
            if (_outlineMaterial == null) return;

            if (Application.isPlaying) Destroy(_outlineMaterial);
            else DestroyImmediate(_outlineMaterial);
        }

        void EnsureMaterial()
        {
            if (_outlineRenderer == null || _outlineMaterial != null) return;

            Shader shader = outlineShader != null ? outlineShader : Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SpecialAudience] 외곽선 셰이더를 찾지 못했습니다: {ShaderName}", this);
                return;
            }

            _outlineMaterial = new Material(shader)
            {
                name = "SpecialAudienceOutline (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _outlineRenderer.sharedMaterial = _outlineMaterial;
            _outlineRenderer.color = Color.white;
        }

        void SyncRenderer()
        {
            if (_sourceRenderer == null || _outlineRenderer == null) return;

            EnsureMaterial();

            Sprite sprite = _sourceRenderer.sprite;
            _outlineRenderer.sprite = sprite;
            _outlineRenderer.flipX = _sourceRenderer.flipX;
            _outlineRenderer.flipY = _sourceRenderer.flipY;
            _outlineRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
            // Tight Mesh 스프라이트에서도 보이도록 본체 위에 "안쪽 경계선"을 겹쳐 그린다.
            // 셰이더가 캐릭터 내부를 투명하게 버리므로 반투명 복제처럼 보이지 않는다.
            _outlineRenderer.sortingOrder = _sourceRenderer.sortingOrder + 1;
            _outlineRenderer.enabled = _sourceRenderer.enabled;

            if (_mode == AudienceOutlineMode.Occluded)
                SyncOccludedBody();

            if (sprite == null || _outlineMaterial == null) return;

            Texture texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            if (_mode == AudienceOutlineMode.Occluded)
            {
                if (!_regionsLoaded)
                {
                    _regions = Resources.Load<AudienceOutlineRegions>(AudienceOutlineRegions.ResourcesPath);
                    _regionsLoaded = true;
                }
                if (_regions != null) textureRect = _regions.GetTextureRect(sprite);
            }
            Vector4 uvRect = new Vector4(
                textureRect.xMin / texture.width,
                textureRect.yMin / texture.height,
                textureRect.xMax / texture.width,
                textureRect.yMax / texture.height);

            _properties ??= new MaterialPropertyBlock();
            _outlineRenderer.GetPropertyBlock(_properties);
            Color visibleColor = _outlineColor;
            visibleColor.a *= _sourceRenderer.color.a;
            _properties.SetColor(OutlineColorId, visibleColor);
            _properties.SetFloat(OutlineWidthId, _outlineThickness);
            _properties.SetFloat(OutlineAlphaCutoffId,
                _mode == AudienceOutlineMode.Occluded ? 0.5f : 0f);
            _properties.SetVector(SpriteUvRectId, uvRect);
            _outlineRenderer.SetPropertyBlock(_properties);
        }

        void SyncOccludedBody()
        {
            if (_sortingGroup == null)
            {
                // 본체만 이 자식 아래에서 그려 경고·점수의 기존 정렬을 유지한다.
                _sortingGroup = gameObject.AddComponent<SortingGroup>();
                var body = new GameObject("OccludedBody", typeof(SpriteRenderer));
                body.layer = _sourceRenderer.gameObject.layer;
                body.transform.SetParent(transform, false);
                _groupedBody = body.GetComponent<SpriteRenderer>();
                _bodyProperties = new MaterialPropertyBlock();
            }

            if (!_sourceSuppressed)
            {
                _originalForceRenderingOff = _sourceRenderer.forceRenderingOff;
                _sourceSuppressed = true;
            }

            _sortingGroup.enabled = true;
            _sortingGroup.sortingLayerID = _sourceRenderer.sortingLayerID;
            _sortingGroup.sortingOrder = _sourceRenderer.sortingOrder;
            _groupedBody.sortingLayerID = _sourceRenderer.sortingLayerID;
            _groupedBody.sortingOrder = 0;
            _outlineRenderer.sortingOrder = 1;

            _groupedBody.sprite = _sourceRenderer.sprite;
            _groupedBody.sharedMaterial = _sourceRenderer.sharedMaterial;
            _groupedBody.color = _sourceRenderer.color;
            _groupedBody.flipX = _sourceRenderer.flipX;
            _groupedBody.flipY = _sourceRenderer.flipY;
            _groupedBody.spriteSortPoint = _sourceRenderer.spriteSortPoint;
            _groupedBody.maskInteraction = _sourceRenderer.maskInteraction;
            _groupedBody.renderingLayerMask = _sourceRenderer.renderingLayerMask;
            _groupedBody.enabled = _sourceRenderer.enabled;
            _groupedBody.forceRenderingOff = _originalForceRenderingOff;
            _sourceRenderer.GetPropertyBlock(_bodyProperties);
            _groupedBody.SetPropertyBlock(_bodyProperties);
            // 애니메이션은 원본 Renderer를 계속 갱신하고, 실제 그리기만 대체한다.
            _sourceRenderer.forceRenderingOff = true;
        }

        void RestoreSource()
        {
            if (_sourceSuppressed && _sourceRenderer != null)
                _sourceRenderer.forceRenderingOff = _originalForceRenderingOff;
            _sourceSuppressed = false;
            if (_groupedBody != null) _groupedBody.enabled = false;
            if (_sortingGroup != null) _sortingGroup.enabled = false;
        }

    }
}
