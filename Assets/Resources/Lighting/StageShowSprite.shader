Shader "ContextStage/Lighting/Stage Show Sprite"
{
    Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Cutoff("Alpha cutoff",Range(0,1))=0.12 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Cutoff;
            CBUFFER_END
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V vert(A v) { V o; SetUpSpriteInstanceProperties(); v.pos.xyz=UnityFlipSprite(v.pos.xyz,unity_SpriteProps.xy); o.pos=TransformObjectToHClip(v.pos.xyz); o.uv=v.uv; o.color=v.color*_Color*unity_SpriteColor; return o; }
            half4 frag(V v):SV_Target { half4 t=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv); clip(t.a-_Cutoff); return t*v.color; }
            ENDHLSL
        }
    }
}
