using UnityEngine;

namespace ContextStage
{
    public enum LocalScreenEffectType
    {
        Distortion = 0,
        ChromaticSplit = 1,
        Pixelate = 2,
        ColorFlash = 3,
    }

    public enum DistortionShape
    {
        Circle = 0,
        RoundedBox = 1,
        Ring = 2,
        Capsule = 3,
        CustomMask = 4,
    }

    public enum DistortionMode
    {
        Radial = 0,
        Wave = 1,
        DirectionalNoise = 2,
        Bulge = 3,
    }

    /// <summary>
    /// 화면 일부에 재생할 효과의 공용 프로필.
    /// 도형 마스크는 모든 효과가 공유하고, 효과별 값만 해당 섹션에서 조절한다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Context Stage/Effects/Local Screen Effect Profile",
        fileName = "LocalScreenEffectProfile")]
    public class LocalScreenEffectProfile : ScriptableObject
    {
        [Header("효과")]
        [SerializeField] LocalScreenEffectType effectType = LocalScreenEffectType.Distortion;

        [Header("공용 영역")]
        [SerializeField] DistortionShape shape = DistortionShape.Ring;
        [SerializeField, Range(0.001f, 0.5f)] float feather = 0.08f;
        [SerializeField, Range(0f, 1f)] float cornerRadius = 0.25f;
        [SerializeField, Range(0f, 1f)] float ringRadius = 0.62f;
        [SerializeField, Range(0.001f, 0.5f)] float ringWidth = 0.14f;
        [SerializeField] Texture2D customMask;

        [Header("디스토션")]
        [SerializeField] DistortionMode mode = DistortionMode.Wave;
        [SerializeField, Range(-0.15f, 0.15f)] float strength = 0.025f;
        [SerializeField, Min(0f)] float frequency = 16f;
        [SerializeField, Min(0f)] float speed = 2.5f;
        [SerializeField, Min(0.01f)] float noiseScale = 7f;
        [SerializeField] Vector2 direction = Vector2.right;

        [Header("색 분리")]
        [SerializeField, Range(0f, 0.1f)] float chromaticOffset = 0.012f;

        [Header("픽셀화")]
        [SerializeField, Range(1f, 128f)] float pixelSize = 12f;

        [Header("컬러 플래시")]
        [SerializeField] Color tintColor = Color.white;
        [SerializeField, Range(0f, 1f)] float tintAmount = 0.65f;

        [Header("재생")]
        [SerializeField, Min(0.01f)] float duration = 0.8f;
        [SerializeField] AnimationCurve intensityOverLifetime =
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, 0f));

        public LocalScreenEffectType EffectType => effectType;
        public DistortionShape Shape => shape;
        public float Feather => feather;
        public float CornerRadius => cornerRadius;
        public float RingRadius => ringRadius;
        public float RingWidth => ringWidth;
        public Texture2D CustomMask => customMask;
        public DistortionMode Mode => mode;
        public float Strength => strength;
        public float Frequency => frequency;
        public float Speed => speed;
        public float NoiseScale => noiseScale;
        public Vector2 Direction =>
            direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        public float ChromaticOffset => chromaticOffset;
        public float PixelSize => pixelSize;
        public Color TintColor => tintColor;
        public float TintAmount => tintAmount;
        public float Duration => duration;

        public float EvaluateIntensity(float normalizedTime)
            => intensityOverLifetime == null
                ? 1f
                : Mathf.Max(0f, intensityOverLifetime.Evaluate(Mathf.Clamp01(normalizedTime)));

        internal void ConfigureAsRuntimeDistortion(float baseStrength, float baseDuration)
        {
            effectType = LocalScreenEffectType.Distortion;
            shape = DistortionShape.Ring;
            mode = DistortionMode.Wave;
            strength = baseStrength;
            duration = baseDuration;
        }
    }
}
