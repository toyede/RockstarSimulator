Shader "ContextStage/Special Audience Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (Pixels)", Range(0, 8)) = 2
        _OutlineAlphaCutoff ("Outline Alpha Cutoff", Range(0, 1)) = 0
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [PerRendererData] _BodyMask ("Body Selection Mask", 2D) = "white" {}
        _UseBodyMask ("Use Body Mask", Float) = 0
        _BodyMaskUVTransform ("Body Mask UV Transform", Vector) = (1, 1, 0, 0)
        _BodyMaskUVRect ("Body Mask UV Rect", Vector) = (0, 0, 1, 1)
        _BodyMaskSize ("Unpacked Body Mask Size", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BodyMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAlphaCutoff;
                float4 _SpriteUVRect;
                float4 _MainTex_TexelSize;
                float _UseBodyMask;
                float4 _BodyMaskUVTransform;
                float4 _BodyMaskUVRect;
                float4 _BodyMaskSize;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                SetUpSpriteInstanceProperties();
                input.positionOS.xyz = UnityFlipSprite(input.positionOS.xyz, unity_SpriteProps.xy);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float SampleSpriteAlpha(float2 uv)
            {
                float2 insideMin = step(_SpriteUVRect.xy, uv);
                float2 insideMax = step(uv, _SpriteUVRect.zw);
                float inside = insideMin.x * insideMin.y * insideMax.x * insideMax.y;
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                if (_UseBodyMask > 0.5)
                {
                    float2 maskUV = uv * _BodyMaskUVTransform.xy + _BodyMaskUVTransform.zw;
                    float2 maskMin = step(_BodyMaskUVRect.xy, maskUV);
                    float2 maskMax = step(maskUV, _BodyMaskUVRect.zw);
                    // R8 한 픽셀에 가로 8개의 선택 비트를 저장한다. 보간/손실 압축은 사용하지 않는다.
                    uint2 at = (uint2)clamp(floor(maskUV * _BodyMaskSize.xy), 0, _BodyMaskSize.xy - 1);
                    uint bits = (uint)round(LOAD_TEXTURE2D(_BodyMask, int2(at.x >> 3, at.y)).r * 255.0);
                    float selected = (bits >> (at.x & 7)) & 1;
                    alpha *= selected * maskMin.x * maskMin.y * maskMax.x * maskMax.y;
                }
                // 상시 표시에서는 반투명 애니메이션 잔상을 별도 테두리로 만들지 않는다.
                if (_OutlineAlphaCutoff > 0)
                    alpha = step(_OutlineAlphaCutoff, alpha);
                return alpha * inside;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;
                float centerAlpha = SampleSpriteAlpha(input.uv);

                float alpha0 = SampleSpriteAlpha(input.uv + float2( offset.x, 0));
                float alpha1 = SampleSpriteAlpha(input.uv + float2(-offset.x, 0));
                float alpha2 = SampleSpriteAlpha(input.uv + float2(0,  offset.y));
                float alpha3 = SampleSpriteAlpha(input.uv + float2(0, -offset.y));
                float alpha4 = SampleSpriteAlpha(input.uv + float2( offset.x,  offset.y));
                float alpha5 = SampleSpriteAlpha(input.uv + float2(-offset.x,  offset.y));
                float alpha6 = SampleSpriteAlpha(input.uv + float2( offset.x, -offset.y));
                float alpha7 = SampleSpriteAlpha(input.uv + float2(-offset.x, -offset.y));

                float surroundingMax = max(max(max(alpha0, alpha1), max(alpha2, alpha3)),
                                           max(max(alpha4, alpha5), max(alpha6, alpha7)));
                float surroundingMin = min(min(min(alpha0, alpha1), min(alpha2, alpha3)),
                                           min(min(alpha4, alpha5), min(alpha6, alpha7)));

                // Full Rect에서는 바깥쪽, Tight Mesh에서는 안쪽 경계선이 보인다.
                float outerOutline = surroundingMax * (1 - centerAlpha);
                float innerOutline = centerAlpha * (1 - surroundingMin);
                float outlineAlpha = saturate(max(outerOutline, innerOutline));
                return half4(_OutlineColor.rgb, _OutlineColor.a * outlineAlpha);
            }
            ENDHLSL
        }
    }
}
