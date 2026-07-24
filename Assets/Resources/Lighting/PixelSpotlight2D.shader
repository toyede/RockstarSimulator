Shader "ContextStage/Lighting/Pixel Spotlight 2D"
{
    Properties
    {
        [HDR] _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _LightIntensity ("Light Intensity", Range(0, 4)) = 1
        _InnerRadius ("Inner Radius", Float) = 1
        _OuterRadius ("Outer Radius", Float) = 10
        _InnerAngle ("Inner Angle", Range(0, 360)) = 30
        _OuterAngle ("Outer Angle", Range(0, 360)) = 60
        _PixelsPerUnit ("World Pixels Per Unit", Range(1, 256)) = 100
        _BandCount ("Light Bands", Range(2, 16)) = 6
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.35
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

        // URP Additive Light처럼 픽셀 조명 색을 현재 화면에 누적한다.
        Blend One One
        ColorMask RGB

        Pass
        {
            Name "PixelSpotlight"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LightOrigin;
                float4 _LightDirection;
                float4 _LightColor;
                float _LightIntensity;
                float _InnerRadius;
                float _OuterRadius;
                float _InnerAngle;
                float _OuterAngle;
                float _PixelsPerUnit;
                float _BandCount;
                float _DitherStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                return output;
            }

            float StablePixelThreshold(float2 pixelCoordinate)
            {
                // 월드 픽셀 좌표에 고정된 디더. 조명이 움직여도 패턴 자체는 흔들리지 않는다.
                return frac(
                    52.9829189 *
                    frac(dot(pixelCoordinate, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float pixelsPerUnit = max(1.0, _PixelsPerUnit);
                float2 pixelCoordinate = floor(input.positionWS * pixelsPerUnit);
                float2 samplePositionWS = (pixelCoordinate + 0.5) / pixelsPerUnit;
                float2 toPixel = samplePositionWS - _LightOrigin.xy;

                float distanceToLight = length(toPixel);
                float2 lightDirection = normalize(
                    _LightDirection.xy + float2(0.000001, 0.0));
                float forwardDistance = dot(toPixel, lightDirection);
                clip(forwardDistance);
                clip(_OuterRadius - distanceToLight);

                float directionCosine = forwardDistance / max(distanceToLight, 0.0001);
                float innerCosine = cos(radians(_InnerAngle * 0.5));
                float outerCosine = cos(radians(_OuterAngle * 0.5));
                float angularLight = saturate(
                    (directionCosine - outerCosine) /
                    max(0.0001, innerCosine - outerCosine));

                float radialLight = 1.0 - saturate(
                    (distanceToLight - _InnerRadius) /
                    max(0.0001, _OuterRadius - _InnerRadius));
                float continuousLight = saturate(angularLight * radialLight);
                clip(continuousLight - 0.0001);

                float bandCount = max(2.0, round(_BandCount));
                float bandScale = bandCount - 1.0;
                float scaledLight = continuousLight * bandScale;
                float roundedLight = round(scaledLight) / bandScale;
                float ditheredLight =
                    (floor(scaledLight) +
                     step(StablePixelThreshold(pixelCoordinate), frac(scaledLight))) /
                    bandScale;
                float pixelLight = saturate(lerp(
                    roundedLight,
                    ditheredLight,
                    saturate(_DitherStrength)));
                clip(pixelLight - 0.0001);

                half3 lighting = max(half3(0, 0, 0), _LightColor.rgb)
                               * pixelLight
                               * max(0.0, _LightIntensity);
                return half4(lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
