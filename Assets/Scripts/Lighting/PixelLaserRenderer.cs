using UnityEngine;

namespace ContextStage
{
    /// <summary>광원을 늘리지 않는 월드 픽셀 빔. 같은 컴포넌트로 넓은 빔/얇은 레이저를 표시한다.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PixelLaserRenderer : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float length = 6f;
        [SerializeField, Min(0.001f)] float startWidth = 0.02f;
        [SerializeField, Min(0.001f)] float endWidth = 0.06f;
        [SerializeField] Material material;
        Mesh _mesh;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        public float Length => length;

        void OnEnable() => EnsureMesh();
        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }
        void EnsureMesh()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Stage Show Beam", hideFlags = HideFlags.HideAndDontSave };
                bool classic=material!=null && material.shader.name=="ContextStage/Lighting/Pixel Spotlight 2D";
                _mesh.vertices = classic ? new[] { new Vector3(-length,-length,0),new Vector3(length,-length,0),new Vector3(-length,length,0),new Vector3(length,length,0) }
                    : new[] { new Vector3(-startWidth / 2, 0, 0), new Vector3(startWidth / 2, 0, 0),new Vector3(-endWidth / 2, length, 0), new Vector3(endWidth / 2, length, 0) };
                _mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                _mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                _mesh.RecalculateBounds();
            }
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer.sharedMaterial = material;
            _renderer.sortingLayerName = "Effects";
            _renderer.sortingOrder = -100; // 월드 위, 반응 텍스트와 Overlay UI 아래.
            _block ??= new MaterialPropertyBlock();
        }
        public void SetVisual(Color color, float strength)
        {
            if (_renderer == null || _block == null || _mesh == null) EnsureMesh();
            _renderer.enabled = material != null && strength > 0.001f;
            color.a = Mathf.Clamp01(strength);
            _block.SetColor(ColorId, color);
            if(material!=null && material.shader.name=="ContextStage/Lighting/Pixel Spotlight 2D")
            {
                float angle=Mathf.Atan2(endWidth*0.5f,length)*Mathf.Rad2Deg*2;
                _block.SetVector("_LightOrigin",transform.position);
                _block.SetVector("_LightDirection",transform.up);
                _block.SetColor("_LightColor",color);
                _block.SetFloat("_LightIntensity",strength);
                _block.SetFloat("_OuterRadius",length);
                _block.SetFloat("_InnerRadius",length*0.12f);
                _block.SetFloat("_OuterAngle",angle);
                _block.SetFloat("_InnerAngle",angle*0.6f);
                _block.SetFloat("_PixelsPerUnit",100);
                _block.SetFloat("_BandCount",6);
                _block.SetFloat("_DitherStrength",0.18f);
            }
            _renderer.SetPropertyBlock(_block);
        }
#if UNITY_EDITOR
        public void EditorConfigure(Material shared, float distance, float width)
        {
            material = shared; length = distance; endWidth = width;
            startWidth = Mathf.Min(width, 0.035f);
            if (_mesh != null) { DestroyImmediate(_mesh); _mesh = null; }
            EnsureMesh();
        }
#endif
    }
}
