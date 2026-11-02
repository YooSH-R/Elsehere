// 입체 버튼. 이미지 색으로 앞면을 칠하고 위가 밝은 그라데이션과 윗면 반사광, 아래 두께, 바닥 그림자를 그린다.
// 그림 채널: R = 앞면 모양, G = 앞면 위아래 위치(위 1 → 아래 0), B = 두께(앞면을 아래로 민 모양), A = 모양 + 그림자.
Shader "A305/UI/Pop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Shade ("Top To Bottom Shade", Range(0, 0.5)) = 0.24
        _Shine ("Top Shine", Range(0, 1)) = 0.32
        _BaseDarken ("Thickness Darken", Range(0.4, 1)) = 0.74
        _ShadowColor ("Shadow Color", Color) = (0.24, 0.14, 0.06, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.32

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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            half _Shade;
            half _Shine;
            half _BaseDarken;
            fixed4 _ShadowColor;
            half _ShadowStrength;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 art = tex2D(_MainTex, IN.texcoord);
                half face = art.r;
                half height = art.g;
                half base = art.b;
                half cover = art.a;
                half3 tone = IN.color.rgb;

                half3 faceColor = tone * (1 - _Shade * 0.5 + _Shade * height) + pow(height, 9) * _Shine;
                half3 baseColor = tone * _BaseDarken;

                // 바닥 그림자 → 두께 → 앞면 순서로 겹친다.
                half shadow = saturate(cover - max(face, base)) * _ShadowStrength;
                half alpha = base + shadow * (1 - base);
                half3 color = (baseColor * base + _ShadowColor.rgb * shadow * (1 - base)) / max(alpha, 0.0001);
                half next = face + alpha * (1 - face);
                color = (saturate(faceColor) * face + color * alpha * (1 - face)) / max(next, 0.0001);
                half4 result = half4(color, next * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
