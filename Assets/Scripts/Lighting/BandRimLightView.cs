using UnityEngine;

namespace ContextStage
{
    /// <summary>원본 프레임/머티리얼은 유지하고 방향성 역광만 별도 스프라이트로 표시한다.</summary>
    [DefaultExecutionOrder(450), DisallowMultipleComponent]
    public sealed class BandRimLightView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer source;
        [SerializeField] Material material;
        SpriteRenderer _overlay;
        MaterialPropertyBlock _properties;
        float _strength;
        Color _color;
        static readonly int ColorId = Shader.PropertyToID("_RimColor");
        static readonly int RectId = Shader.PropertyToID("_SpriteUVRect");
        static readonly int DirectionId = Shader.PropertyToID("_RimDirection");

        void Awake() => Ensure();
        void Ensure()
        {
            if (_overlay != null) return;
            var child = new GameObject("[Band Rim Light]");
            child.transform.SetParent(transform, false);
            _overlay = child.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = material;
            _overlay.sortingLayerName = "Effects";
            _overlay.sortingOrder = -80;
            _properties = new MaterialPropertyBlock();
        }
        public void SetVisual(Color color, float strength)
        { _color = color; _strength = Mathf.Clamp01(strength); }
        void OnDisable() { _strength = 0; if (_overlay != null) _overlay.enabled = false; }
        void OnDestroy() { if (_overlay != null) Destroy(_overlay.gameObject); }
        void LateUpdate()
        {
            Ensure();
            bool visible = source != null && source.enabled && source.gameObject.activeInHierarchy
                && source.sprite != null && material != null && _strength > 0.001f;
            _overlay.enabled = visible;
            if (!visible) return;
            _overlay.sprite = source.sprite;
            _overlay.flipX = source.flipX; _overlay.flipY = source.flipY;
            _overlay.color = Color.white;
            _properties.SetVector(RectId, UnityEngine.Sprites.DataUtility.GetOuterUV(source.sprite));
            Vector3 direction = source.transform.InverseTransformDirection(new Vector3(-0.5f, 1f, 0));
            if (source.flipX) direction.x = -direction.x;
            if (source.flipY) direction.y = -direction.y;
            _properties.SetVector(DirectionId, direction.normalized);
            _properties.SetColor(ColorId, new Color(_color.r, _color.g, _color.b, _strength));
            _overlay.SetPropertyBlock(_properties);
        }
#if UNITY_EDITOR
        public void EditorConfigure(SpriteRenderer target, Material shared) { source = target; material = shared; }
#endif
    }
}
