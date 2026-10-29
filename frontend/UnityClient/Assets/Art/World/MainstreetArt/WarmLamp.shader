Shader "Elsehere/WarmLamp"
{
    Properties
    {
        [HDR] _BaseColor ("Warm light color", Color) = (1, 0.73, 0.3, 1)
        _CoreStrength ("Center brightness", Range(0, 3)) = 1.2
        _EdgeStrength ("Edge brightness", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _CoreStrength;
                half _EdgeStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half facing = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                // Keep the amber rim visible, even when a realtime light sits inside the bulb.
                half strength = lerp(_EdgeStrength, _CoreStrength, sqrt(facing));
                return half4(MixFog(_BaseColor.rgb * strength, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
