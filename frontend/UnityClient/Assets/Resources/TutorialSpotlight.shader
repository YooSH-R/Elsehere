// 어두운 바깥 판과 둥근 금색 테두리를 같은 거리 함수로 그려 사각형 이음새를 없앤다.
Shader "A305/UI/TutorialSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Hole ("Hole", Vector) = (0, 0, 100, 100)
        _Radius ("Radius", Float) = 32
        _Pulse ("Pulse", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 local : TEXCOORD0;
                float4 color : COLOR;
            };
            float4 _Color;
            float4 _Hole;
            float _Radius;
            float _Pulse;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.local = input.vertex.xy;
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 halfSize = (_Hole.zw - _Hole.xy) * 0.5;
                float2 center = (_Hole.zw + _Hole.xy) * 0.5;
                float2 corner = abs(input.local - center) - halfSize + _Radius;
                float distance = length(max(corner, 0)) + min(max(corner.x, corner.y), 0) - _Radius;
                float smoothing = max(fwidth(distance), 0.5);
                float shade = smoothstep(-smoothing, smoothing, distance) * input.color.a;
                float edge = abs(distance);
                float rim = 1 - smoothstep(1.2, 2.6, edge);
                float glow = 0.16 * exp(-edge * edge / 72);
                float light = max(rim * 0.8, glow) * _Pulse;
                float3 gold = lerp(float3(1, 0.79, 0.25), float3(1, 0.97, 0.77), rim * 0.55);
                float alpha = light + shade * (1 - light);
                float3 color = (gold * light + input.color.rgb * shade * (1 - light)) / max(alpha, 0.0001);
                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
