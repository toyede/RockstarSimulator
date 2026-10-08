Shader "ContextStage/Lighting/Pixel Show Beam"
{
    Properties { _Color("Color", Color) = (1,1,1,0.2) _Grid("World pixel grid", Float) = 32 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Grid;
            CBUFFER_END
            V vert(A v) { V o; float3 w=TransformObjectToWorld(v.vertex.xyz); w.xy=round(w.xy*_Grid)/_Grid;
                o.pos=TransformWorldToHClip(w); o.uv=v.uv; return o; }
            half4 frag(V v):SV_Target
            {
                float edge=abs(v.uv.x*2-1);
                float fade=smoothstep(1,0.6,v.uv.y);
                float halo=pow(saturate(1-edge),2);
                float core=pow(saturate(1-edge),12);
                return half4(lerp(_Color.rgb,half3(1,1,1),core*0.35), _Color.a*fade*(halo*0.5+core*0.5));
            }
            ENDHLSL
        }
    }
}
