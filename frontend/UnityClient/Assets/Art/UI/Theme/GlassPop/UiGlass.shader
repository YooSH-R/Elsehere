// 유리 창. 뒤 화면을 흐리게 비추고 이미지 색을 옅게 섞는다.
// 그림 채널: R = 유리 모양, G = 위쪽 하이라이트·가장자리 빛, A = 모양 + 바깥 그림자.
// 흐린 뒤 화면이 없으면(렌더러 기능이 없을 때) 이미지 색을 진하게 칠해 글자가 읽히게 한다.
Shader "A305/UI/Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.42
        _Brighten ("Brighten", Range(0, 0.3)) = 0.05
        _SheenStrength ("Sheen Strength", Range(0, 1.5)) = 0.8
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.12, 0.05, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.34

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
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            half _TintStrength;
            half _Brighten;
            half _SheenStrength;
            fixed4 _ShadowColor;
            half _ShadowStrength;

            sampler2D _A305UiBackdrop;
            float _A305UiBackdropReady;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 art = tex2D(_MainTex, IN.texcoord);
                half glass = art.r;
                half sheen = art.g;
                half cover = art.a;

                half3 behind = tex2D(_A305UiBackdrop, IN.screenPos.xy / IN.screenPos.w).rgb;
                half strength = lerp(0.92, _TintStrength, saturate(_A305UiBackdropReady));
                half3 surface = lerp(behind, IN.color.rgb, strength) + _Brighten + sheen * _SheenStrength;

                half shadow = saturate(cover - glass) * _ShadowStrength;
                half alpha = glass + shadow * (1 - glass);
                half3 color = (surface * glass + _ShadowColor.rgb * shadow * (1 - glass)) / max(alpha, 0.0001);
                half4 result = half4(color, alpha * IN.color.a);

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
