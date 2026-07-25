// 팝업 뒤 배경용 Kawase 블러.
//
// Graphics.Blit 으로 RenderTexture 사이를 오가며 여러 번 돌린다.
// Kawase 는 패스당 대각선 4탭만 쓰면서 오프셋을 키워 가는 방식이라,
// 같은 흐림 정도를 가우시안보다 훨씬 적은 샘플로 만든다.
// (모바일·저사양에서 흔히 쓰는 방식이고, 잼 규모에서는 사실상 공짜다)
//
// UI 셰이더가 아니다 — 최종 결과는 RawImage 가 기본 UI 셰이더로 그린다.
// 어둡게 깔거나 색을 입히는 건 RawImage 의 color 로 하면 되므로 여기서 다루지 않는다.
Shader "ContextStage/UI/Kawase Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Sample Offset (pixels)", Float) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 텍셀 중심에서 0.5 만큼 비켜 찍어 하드웨어 이중선형 보간이
                // 한 번의 샘플로 네 텍셀을 섞게 한다 (탭 수 대비 흐림 효율이 좋다)
                float2 texel = _MainTex_TexelSize.xy * (_Offset + 0.5);

                fixed4 sum = tex2D(_MainTex, i.uv + float2( texel.x,  texel.y));
                sum += tex2D(_MainTex, i.uv + float2(-texel.x,  texel.y));
                sum += tex2D(_MainTex, i.uv + float2( texel.x, -texel.y));
                sum += tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y));
                return sum * 0.25;
            }
            ENDCG
        }
    }

    Fallback Off
}
