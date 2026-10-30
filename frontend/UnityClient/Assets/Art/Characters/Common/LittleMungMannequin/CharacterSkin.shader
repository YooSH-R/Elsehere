Shader "Elsehere/CharacterSkin"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _SkinMask("Skin Mask", 2D) = "white" {}
        _SkinTint("Skin Tint Multiplier", Vector) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.12
        _Metallic("Metallic", Range(0,1)) = 0
        [HideInInspector] _SpecColor("Specular", Color) = (.2,.2,.2,1)
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,1)
        [HideInInspector] _BumpScale("Normal Scale", Float) = 1
        [HideInInspector] _OcclusionStrength("Occlusion", Float) = 1
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define InitializeStandardLitSurfaceData InitializeUntintedSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData
            TEXTURE2D(_SkinMask);
            SAMPLER(sampler_SkinMask);
            float4 _SkinTint;
            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
            {
                InitializeUntintedSurfaceData(uv, surfaceData);
                half mask = SAMPLE_TEXTURE2D(_SkinMask, sampler_SkinMask, uv).r;
                surfaceData.albedo *= lerp(half3(1,1,1), _SkinTint.rgb, mask);
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
