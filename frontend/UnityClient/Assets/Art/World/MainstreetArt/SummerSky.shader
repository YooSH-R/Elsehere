Shader "Elsehere/SummerSky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(0.3,0.65,0.9,1)
        _Horizon("Horizon",Color)=(0.7,0.88,0.93,1)
        _Stars("Night stars and moon",Range(0,1))=0
        _MoonDirection("Moon direction",Vector)=(0.3,0.5,0.81,0)
    }
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
            float4 _Zenith, _Horizon, _MoonDirection;
            float _Stars;
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 vertex : POSITION) { Varyings o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            // Scatter a few stars across a direction grid and fade them out near the horizon haze.
            float StarField(float3 direction)
            {
                float2 grid=float2(atan2(direction.x,direction.z)*36,direction.y*56);
                float2 cell=floor(grid);
                float2 jitter=float2(Hash(cell+1.7),Hash(cell+9.3))-0.5;
                float2 offset=frac(grid)-0.5-jitter*0.6;
                float lit=step(0.955,Hash(cell));
                float size=lerp(0.06,0.13,Hash(cell+4.1));
                return lit*smoothstep(size,0,length(offset))*saturate(direction.y*3);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction=normalize(input.direction);
                float height=saturate(direction.y*1.6);
                half4 color=lerp(_Horizon,_Zenith,pow(height,0.45));
                if (_Stars>0)
                {
                    float facing=dot(direction,normalize(_MoonDirection.xyz));
                    float moon=smoothstep(0.9988,0.9993,facing);
                    float halo=pow(saturate(facing),160)*0.35;
                    color.rgb+=_Stars*(StarField(direction)*0.9+moon*half3(0.98,0.96,0.86)+halo*half3(0.55,0.65,0.95));
                }
                return color;
            }
            ENDHLSL
        }
    }
}
