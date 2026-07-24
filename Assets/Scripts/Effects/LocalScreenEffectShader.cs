using UnityEngine;

namespace ContextStage
{
    /// <summary>통합 로컬 화면 이펙트 셰이더의 공용 프로퍼티와 쿼드.</summary>
    internal static class LocalScreenEffectShader
    {
        public const string ShaderName = "ContextStage/Effects/Local Screen Effect";
        public const string SortingLayerName = "Effects";

        static readonly int EffectTypeId = Shader.PropertyToID("_EffectType");
        static readonly int ShapeTypeId = Shader.PropertyToID("_ShapeType");
        static readonly int DistortionModeId = Shader.PropertyToID("_DistortionMode");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int FeatherId = Shader.PropertyToID("_Feather");
        static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
        static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
        static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        static readonly int FrequencyId = Shader.PropertyToID("_Frequency");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        static readonly int DirectionId = Shader.PropertyToID("_Direction");
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        static readonly int CustomMaskId = Shader.PropertyToID("_CustomMask");
        static readonly int ChromaticOffsetId = Shader.PropertyToID("_ChromaticOffset");
        static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        static readonly int TintAmountId = Shader.PropertyToID("_TintAmount");

        static Mesh s_sharedQuad;

        public static Mesh SharedQuad
        {
            get
            {
                if (s_sharedQuad != null) return s_sharedQuad;

                s_sharedQuad = new Mesh
                {
                    name = "Local Screen Effect Quad",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f),
                        new Vector3( 0.5f, -0.5f, 0f),
                        new Vector3( 0.5f,  0.5f, 0f),
                        new Vector3(-0.5f,  0.5f, 0f),
                    },
                    uv = new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f),
                    },
                    triangles = new[] { 0, 2, 1, 0, 3, 2 },
                };
                s_sharedQuad.RecalculateBounds();
                return s_sharedQuad;
            }
        }

        public static void Apply(
            MeshRenderer renderer,
            MaterialPropertyBlock properties,
            LocalScreenEffectProfile profile,
            float progress,
            float effectTime,
            float strengthMultiplier = 1f)
        {
            if (renderer == null || properties == null || profile == null) return;

            float intensity = profile.EvaluateIntensity(progress) * Mathf.Max(0f, strengthMultiplier);
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(EffectTypeId, (float)profile.EffectType);
            properties.SetFloat(ShapeTypeId, (float)profile.Shape);
            properties.SetFloat(DistortionModeId, (float)profile.Mode);
            properties.SetFloat(StrengthId, profile.Strength * intensity);
            properties.SetFloat(FeatherId, profile.Feather);
            properties.SetFloat(CornerRadiusId, profile.CornerRadius);
            properties.SetFloat(RingRadiusId, profile.RingRadius);
            properties.SetFloat(RingWidthId, profile.RingWidth);
            properties.SetFloat(FrequencyId, profile.Frequency);
            properties.SetFloat(SpeedId, profile.Speed);
            properties.SetFloat(NoiseScaleId, profile.NoiseScale);
            Vector2 direction = profile.Direction;
            properties.SetVector(DirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
            properties.SetFloat(ProgressId, progress);
            properties.SetFloat(EffectTimeId, effectTime);
            properties.SetFloat(ChromaticOffsetId, profile.ChromaticOffset * intensity);
            properties.SetFloat(PixelSizeId, Mathf.Lerp(1f, profile.PixelSize, intensity));
            properties.SetColor(TintColorId, profile.TintColor);
            properties.SetFloat(TintAmountId, profile.TintAmount * intensity);
            if (profile.CustomMask != null)
                properties.SetTexture(CustomMaskId, profile.CustomMask);
            renderer.SetPropertyBlock(properties);
        }
    }
}
