Shader "Elsehere/SummerSky"
{
    Properties { _Zenith("Zenith",Color)=(0.3,0.65,0.9,1) _Horizon("Horizon",Color)=(0.7,0.88,0.93,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _Zenith, _Horizon;
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 vertex : POSITION) { Varyings o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            half4 Frag(Varyings input) : SV_Target
            { float height=saturate(normalize(input.direction).y*1.6); return lerp(_Horizon,_Zenith,pow(height,0.45)); }
            ENDHLSL
        }
    }
}
