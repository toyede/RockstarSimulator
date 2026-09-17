Shader "ContextStage/Special Audience Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (Pixels)", Range(0, 8)) = 2
        _OutlineAlphaCutoff ("Outline Alpha Cutoff", Range(0, 1)) = 0
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
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

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAlphaCutoff;
                float4 _SpriteUVRect;
                float4 _MainTex_TexelSize;
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
