Shader "Elsehere/TownHorizonSky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(.23,.65,.95,1)
        _Horizon("Horizon",Color)=(.72,.88,.94,1)
        _Stars("Night stars and moon",Range(0,1))=0
        _MoonDirection("Moon direction",Vector)=(.3,.5,.81,0)
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
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(vertex);
                o.direction = vertex.xyz;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float StarField(float3 d)
            {
                float2 grid = float2(atan2(d.x,d.z)*36,d.y*56);
                float2 cell = floor(grid);
                float2 jitter = float2(Hash(cell+1.7),Hash(cell+9.3))-.5;
                float2 offset = frac(grid)-.5-jitter*.6;
                float size = lerp(.06,.13,Hash(cell+4.1));
                return step(.955,Hash(cell))*smoothstep(size,0,length(offset))*saturate(d.y*3);
            }
            float Clouds(float3 d)
            {
                float2 grid = float2(atan2(d.x,d.z)*3.1831, d.y*8);
                float2 cell = floor(grid);
                float2 p = frac(grid)-.5;
                p -= (float2(Hash(cell+2.1),Hash(cell+7.3))-.5)*.28;
                p /= lerp(.7,1.08,Hash(cell+4.8));
                p.x *= 1.25;
                float shape = min(length((p-float2(-.19,-.05))/float2(.2,.14)),
                    length((p-float2(.17,-.04))/float2(.25,.15)));
                shape = min(shape,length((p-float2(0,.04))/float2(.2,.21)));
                return (1-smoothstep(.85,1,shape))*step(.68,Hash(cell))
                    *smoothstep(.08,.18,d.y)*(1-smoothstep(.56,.7,d.y));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                half4 color = lerp(_Horizon,_Zenith,pow(saturate(d.y*1.6),.45));
                half3 cloudColor = lerp(_Horizon.rgb,half3(1,.98,.93),.83);
                cloudColor *= lerp(1,.12,_Stars);
                color.rgb = lerp(color.rgb,cloudColor,Clouds(d));
                float facing = dot(d,normalize(_MoonDirection.xyz));
                float moon = smoothstep(.9988,.9993,facing);
                float halo = pow(saturate(facing),160)*.35;
                color.rgb += _Stars*(StarField(d)*.9 + moon*half3(.98,.96,.86) + halo*half3(.55,.65,.95));
                return color;
            }
            ENDHLSL
        }
    }
}
