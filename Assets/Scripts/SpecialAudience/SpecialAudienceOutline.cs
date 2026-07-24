using UnityEngine;

namespace ContextStage
{
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
        static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUVRect");

        const string ShaderName = "ContextStage/Special Audience Outline";

        [SerializeField] Shader outlineShader;

        SpriteRenderer _sourceRenderer;
        SpriteRenderer _outlineRenderer;
        Material _outlineMaterial;
        MaterialPropertyBlock _properties;
        Color _outlineColor = Color.white;
        float _outlineThickness = 2f;

        public void Configure(
            SpriteRenderer sourceRenderer,
            Color outlineColor,
            float outlineThickness)
        {
            _sourceRenderer = sourceRenderer;
            _outlineRenderer = GetComponent<SpriteRenderer>();
            _outlineColor = outlineColor;
            _outlineThickness = Mathf.Max(0f, outlineThickness);

            EnsureMaterial();
            SyncRenderer();
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);

            if (visible)
                SyncRenderer();
        }

        void LateUpdate()
        {
            SyncRenderer();
        }

        void OnDestroy()
        {
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

            if (sprite == null || _outlineMaterial == null) return;

            Texture texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            Vector4 uvRect = new Vector4(
                textureRect.xMin / texture.width,
                textureRect.yMin / texture.height,
                textureRect.xMax / texture.width,
                textureRect.yMax / texture.height);

            _properties ??= new MaterialPropertyBlock();
            _outlineRenderer.GetPropertyBlock(_properties);
            _properties.SetColor(OutlineColorId, _outlineColor);
            _properties.SetFloat(OutlineWidthId, _outlineThickness);
            _properties.SetVector(SpriteUvRectId, uvRect);
            _outlineRenderer.SetPropertyBlock(_properties);
        }

    }
}
