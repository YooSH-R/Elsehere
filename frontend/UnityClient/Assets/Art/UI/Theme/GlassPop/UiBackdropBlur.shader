// 유리 창이 비춰 보일 뒤 화면. 카메라 화면을 줄여 복사한 뒤 가로·세로로 흐리게 한다(UiBackdropBlurFeature가 쓴다).
Shader "Hidden/A305/UiBackdropBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurSpread;

        half4 Tap(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }

        // 9탭 가우시안을 선형 보간 5탭으로 줄인 흐림
        half4 Blur(float2 uv, float2 direction)
        {
            float2 step = direction * _BlitTexture_TexelSize.xy * _BlurSpread;
            half4 color = Tap(uv) * 0.2270270270;
            color += (Tap(uv + step * 1.3846153846) + Tap(uv - step * 1.3846153846)) * 0.3162162162;
            color += (Tap(uv + step * 3.2307692308) + Tap(uv - step * 3.2307692308)) * 0.0702702703;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 half_texel = _BlitTexture_TexelSize.xy * 0.5;
                half4 color = Tap(input.texcoord + float2(half_texel.x, half_texel.y));
                color += Tap(input.texcoord + float2(-half_texel.x, half_texel.y));
                color += Tap(input.texcoord + float2(half_texel.x, -half_texel.y));
                color += Tap(input.texcoord + float2(-half_texel.x, -half_texel.y));
                return color * 0.25;
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                return Blur(input.texcoord, float2(1, 0));
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                return Blur(input.texcoord, float2(0, 1));
            }
            ENDHLSL
        }
    }
}
