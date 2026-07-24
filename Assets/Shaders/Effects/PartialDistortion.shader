Shader "ContextStage/Effects/Local Screen Effect"
{
    Properties
    {
        _CustomMask ("Custom Shape Mask", 2D) = "white" {}
        _EffectType ("Effect Type", Float) = 0
        _ShapeType ("Shape Type", Float) = 2
        _DistortionMode ("Distortion Mode", Float) = 1
        _Strength ("Strength", Range(-0.15, 0.15)) = 0.025
        _Feather ("Edge Feather", Range(0.001, 0.5)) = 0.08
        _CornerRadius ("Corner Radius", Range(0, 1)) = 0.25
        _RingRadius ("Ring Radius", Range(0, 1)) = 0.62
        _RingWidth ("Ring Width", Range(0.001, 0.5)) = 0.14
        _Frequency ("Frequency", Float) = 16
        _Speed ("Speed", Float) = 2.5
        _NoiseScale ("Noise Scale", Float) = 7
        _Direction ("Direction", Vector) = (1, 0, 0, 0)
        _Progress ("Progress", Range(0, 1)) = 0
        _EffectTime ("Effect Time", Float) = 0
        _ChromaticOffset ("Chromatic Offset", Range(0, 0.1)) = 0.012
        _PixelSize ("Pixel Size", Range(1, 128)) = 12
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LocalScreenEffect"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);
            TEXTURE2D(_CustomMask);
            SAMPLER(sampler_CustomMask);

            CBUFFER_START(UnityPerMaterial)
                float _EffectType;
                float _ShapeType;
                float _DistortionMode;
                float _Strength;
                float _Feather;
                float _CornerRadius;
                float _RingRadius;
                float _RingWidth;
                float _Frequency;
                float _Speed;
                float _NoiseScale;
                float4 _Direction;
                float _Progress;
                float _EffectTime;
                float _ChromaticOffset;
                float _PixelSize;
                float4 _TintColor;
                float _TintAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionHCS);
                output.uv = input.uv;
                return output;
            }

            float SdRoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            float SdCapsule(float2 p)
            {
                float2 a = float2(-0.55, 0.0);
                float2 b = float2( 0.55, 0.0);
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - 0.42;
            }

            float ShapeMask(float2 p, float2 uv)
            {
                float distanceField;

                if (_ShapeType < 0.5)
                {
                    distanceField = length(p) - 1.0;
                }
                else if (_ShapeType < 1.5)
                {
                    float radius = saturate(_CornerRadius) * 0.8;
                    distanceField = SdRoundedBox(p, float2(1.0, 1.0), radius);
                }
                else if (_ShapeType < 2.5)
                {
                    distanceField = abs(length(p) - _RingRadius) - _RingWidth;
                }
                else if (_ShapeType < 3.5)
                {
                    distanceField = SdCapsule(p);
                }
                else
                {
                    return SAMPLE_TEXTURE2D(_CustomMask, sampler_CustomMask, uv).a;
                }

                return 1.0 - smoothstep(0.0, max(0.0001, _Feather), distanceField);
            }

            float Noise(float2 p)
            {
                float a = sin(p.x * 1.73 + _EffectTime * _Speed);
                float b = sin(p.y * 2.31 - _EffectTime * (_Speed * 1.17));
                float c = sin((p.x + p.y) * 1.11 + _EffectTime * (_Speed * 0.63));
                return (a + b + c) / 3.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 local = input.uv * 2.0 - 1.0;
                float mask = saturate(ShapeMask(local, input.uv));
                clip(mask - 0.001);

                float radius = length(local);
                float2 radial = radius > 0.0001 ? local / radius : float2(0.0, 0.0);
                float2 direction = normalize(_Direction.xy + float2(0.0001, 0.0));
                float2 distortion;

                if (_DistortionMode < 0.5)
                {
                    distortion = radial * _Strength;
                }
                else if (_DistortionMode < 1.5)
                {
                    float wave = sin(radius * _Frequency - _Progress * _Frequency - _EffectTime * _Speed);
                    distortion = radial * wave * _Strength;
                }
                else if (_DistortionMode < 2.5)
                {
                    float noise = Noise(local * max(0.01, _NoiseScale));
                    distortion = direction * noise * _Strength;
                }
                else
                {
                    float bulge = saturate(1.0 - radius);
                    distortion = radial * bulge * _Strength;
                }

                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                half4 effectColor;

                if (_EffectType < 0.5)
                {
                    float2 sampleUV = saturate(screenUV + distortion * mask);
                    effectColor = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        sampleUV);
                }
                else if (_EffectType < 1.5)
                {
                    float2 offset = radial * _ChromaticOffset * mask;
                    half red = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        saturate(screenUV + offset)).r;
                    half green = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        screenUV).g;
                    half blue = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        saturate(screenUV - offset)).b;
                    effectColor = half4(red, green, blue, 1.0);
                }
                else if (_EffectType < 2.5)
                {
                    float2 pixelCount = max(float2(1.0, 1.0), _ScreenParams.xy / max(1.0, _PixelSize));
                    float2 pixelUV = (floor(screenUV * pixelCount) + 0.5) / pixelCount;
                    effectColor = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        saturate(pixelUV));
                }
                else
                {
                    half4 sceneColor = SAMPLE_TEXTURE2D(
                        _CameraSortingLayerTexture,
                        sampler_CameraSortingLayerTexture,
                        screenUV);
                    effectColor = lerp(sceneColor, _TintColor, saturate(_TintAmount));
                }

                effectColor.a = mask;
                return effectColor;
            }
            ENDHLSL
        }
    }
}
