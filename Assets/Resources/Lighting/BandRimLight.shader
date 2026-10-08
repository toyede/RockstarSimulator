Shader "ContextStage/Lighting/Band Rim Light"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _RimColor("Rim",Color)=(1,1,1,0)
        _SpriteUVRect("UV Bounds",Vector)=(0,0,1,1)
        _RimDirection("Direction",Vector)=(-0.5,1,0,0)
        _RimWidth("Width (texture pixels)",Range(1,4))=2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize, _RimColor, _SpriteUVRect, _RimDirection;
            float _RimWidth;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i)
            {
                V o; SetUpSpriteInstanceProperties();
                i.positionOS.xyz=UnityFlipSprite(i.positionOS.xyz,unity_SpriteProps.xy);
                o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o;
            }
            float Alpha(float2 uv)
            {
                float2 a=step(_SpriteUVRect.xy,uv),b=step(uv,_SpriteUVRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a*a.x*a.y*b.x*b.y;
            }
            half4 Frag(V i):SV_Target
            {
                float2 d=normalize(_RimDirection.xy+float2(0.0001,0))*_MainTex_TexelSize.xy*_RimWidth;
                float edge=saturate(Alpha(i.uv)-Alpha(i.uv+d));
                return half4(_RimColor.rgb,edge*_RimColor.a);
            }
            ENDHLSL
        }
    }
}
