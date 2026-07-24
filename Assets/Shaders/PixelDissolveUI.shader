// 픽셀 디졸브 (Unity UI 전용)
//
// UI-Default 를 기반으로 디졸브만 얹었다. 그래서 Canvas 의 클리핑(RectMask2D/Mask),
// 스텐실 마스크, Image 의 Color 틴트가 전부 원래대로 동작한다.
//
// 노이즈는 텍스처 없이 셀 좌표 해시로 만든다. 시간값을 쓰지 않으므로 패턴이 고정되고,
// _DissolveAmount 만 0 → 1 로 움직이면 같은 자리에서 같은 순서로 사라진다.
// 카드마다 _NoiseOffset 을 다르게 주면 서로 다른 모양으로 사라진다.
//
// Shader Graph 대신 직접 쓴 이유: .shadergraph 는 병합 충돌 때 손댈 수 없고,
// UI 클리핑을 직접 처리해야 해서 오히려 손이 더 간다.
Shader "ContextStage/UI/Pixel Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.5)) = 0.08
        _EdgeColor ("Edge Color", Color) = (0.94,0.12,0.12,1)
        _PixelSize ("Pixel Block Count", Range(4,128)) = 24
        _NoiseScale ("Noise Scale", Range(0.1,8)) = 1
        _Direction ("Bottom To Top", Range(0,1)) = 0.5
        _NoiseOffset ("Noise Offset (Seed)", Vector) = (0,0,0,0)

        // ---- 아래는 UI-Default 와 동일한 표준 UI 프로퍼티 ----
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float  _DissolveAmount;
            float  _EdgeWidth;
            fixed4 _EdgeColor;
            float  _PixelSize;
            float  _NoiseScale;
            float  _Direction;
            float4 _NoiseOffset;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 셀 좌표 → 0~1 의 고정된 의사 난수. 시간을 쓰지 않아 패턴이 흔들리지 않는다.
            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // --- 픽셀 블록 단위로 UV 양자화 → 부드러운 페이드가 아니라 각진 픽셀로 사라진다 ---
                float blocks = max(1.0, _PixelSize);
                float2 cell = floor(IN.texcoord * blocks) * max(0.0001, _NoiseScale);
                float noise = Hash(cell + _NoiseOffset.xy);

                // 아래에서 위로 진행. _Direction 이 0이면 순수 랜덤, 1이면 아래쪽이 확실히 먼저 사라진다.
                float gradient = 1.0 - IN.texcoord.y;
                float value = lerp(noise, saturate(noise * 0.35 + gradient * 0.65), _Direction);

                // 경계 폭만큼 여유를 둬서 Amount=1 일 때 확실히 전부 사라지게 한다
                float threshold = _DissolveAmount * (1.0 + _EdgeWidth);
                float diff = value - threshold;

                clip(diff);  // hard cut — 알파 페이드가 아니라 픽셀 단위로 잘려나간다

                // --- 사라지는 경계에 타입 색 입히기 (디졸브가 시작된 뒤에만) ---
                float edge = 1.0 - saturate(diff / max(_EdgeWidth, 0.0001));
                edge *= step(0.0001, _DissolveAmount);
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);

                // --- 아래는 UI-Default 와 동일 (클리핑·알파클립) ---
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
