// 커서를 올린 NPC의 테두리(NpcClickTalk). 몸 그림에 재질로 하나 더 붙여, 법선 방향으로 화면 몇 픽셀만큼 부풀린 뒷면을 그린다.
// 몸 앞면이 먼저 깊이를 써 두므로 몸 바깥으로 삐져나온 부분만 테두리로 보인다. 그래서 불투명 그림 뒤인 투명 단계에서 그린다.
// 몸·옷·머리 그림마다 따로 부풀리므로 안쪽 경계에도 선이 생기는데, 카메라에서 조금 뒤로 밀어 바깥 윤곽만 남긴다.
Shader "A305/NpcHoverOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.9, 0.45, 0.95)
        _OutlineWidth("Outline Width (px)", Range(0, 8)) = 3
        _DepthPush("Depth Push (m)", Range(0, 0.3)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HoverOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _DepthPush;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 카메라에서 조금 뒤로 밀어, 바로 뒤에 몸이 있는 안쪽(머리카락 결·코·옷 경계)은 가려지고 바깥 윤곽만 남게 한다.
                // 카메라에서 나가는 시선 방향으로 밀어 화면 위치는 그대로 두고 깊이만 늘린다.
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                positionVS += normalize(positionVS) * _DepthPush;
                float4 positionCS = TransformWViewToHClip(positionVS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                // 화면과 거의 수직인 법선은 방향이 없으므로 밀지 않는다.
                float normalLength = length(normalCS);
                float2 direction = normalLength > 1e-5 ? normalCS / normalLength : float2(0, 0);
                // 거리와 상관없이 화면에서 같은 두께가 되도록 클립 공간에서 w를 곱해 픽셀 단위로 민다.
                positionCS.xy += direction * (_OutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
