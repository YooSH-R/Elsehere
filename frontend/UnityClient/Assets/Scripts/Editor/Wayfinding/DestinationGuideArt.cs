using System;
using System.IO;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Wayfinding
{
    /// <summary>
    /// 길 안내의 빛 그림과 재질. 그림은 흰색에 진하기만 담고 색은 길 안내가 설정의 야광색으로 칠한다.
    /// 밝은 인도 위에서도 보이도록 길과 바닥 빛 원은 덮어 칠하고(Alpha), 하늘을 배경으로 보는 빛기둥은 빛을 더한다(Additive).
    /// 모두 그림자 없는 반투명 Unlit이다. 없을 때만 만들어 디자인으로 바꾼 것은 그대로 둔다.
    /// </summary>
    internal static class DestinationGuideArt
    {
        private const string Folder = "Assets/Art/Wayfinding";

        // URP Unlit의 섞는 방식(_Blend) 번호
        private const float AlphaBlend = 0f;
        private const float AdditiveBlend = 2f;

        internal static Material RouteMaterial() =>
            Material(
                "GuideRoute",
                Texture("GuideRouteArrows", 128, 64, TextureWrapMode.Repeat, RouteArrow),
                AlphaBlend
            );

        internal static Material PoolMaterial() =>
            Material(
                "GuidePool",
                Texture("GuidePool", 128, 128, TextureWrapMode.Clamp, PoolRing),
                AlphaBlend
            );

        internal static Material BeamMaterial() =>
            Material(
                "GuideBeam",
                Texture("GuideBeam", 64, 128, TextureWrapMode.Clamp, BeamFade),
                AdditiveBlend
            );

        // 길 1m(u)와 길 폭(v) 한 칸. 가운데가 진한 띠 위에 출입문 쪽(u가 커지는 쪽)을 가리키는 꺾쇠를 하나 그린다.
        private static float RouteArrow(float u, float v)
        {
            float across = Mathf.Abs(v - .5f) * 2f;
            float band = .55f * Mathf.Pow(1f - across, .8f);
            float tip = .72f - across * .32f;
            float chevron = across < .8f ? Mathf.Clamp01(1f - Mathf.Abs(u - tip) / .1f) : 0f;
            return Mathf.Max(band, chevron);
        }

        // 바닥 빛 원. 안은 옅게 채우고 가장자리에 진한 고리를 둔다.
        private static float PoolRing(float u, float v)
        {
            float radius = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f;
            if (radius >= 1f)
                return 0f;
            float core = .5f * (1f - radius * radius);
            float ring = Mathf.Exp(-Mathf.Pow((radius - .8f) / .08f, 2f));
            return core + ring;
        }

        // 빛기둥. 바닥에서 가장 밝고 위로, 양옆으로 갈수록 옅어진다.
        private static float BeamFade(float u, float v)
        {
            float across = 1f - Mathf.Abs(u - .5f) * 2f;
            return .9f * Mathf.Pow(across, 1.5f) * Mathf.Pow(1f - v, .8f);
        }

        private static Material Material(string name, Texture2D texture, float blend)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
                throw new InvalidOperationException("URP Unlit 셰이더를 찾지 못했습니다.");
            var material = new Material(unlit);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", blend);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)(blend == AdditiveBlend ? BlendMode.One : BlendMode.OneMinusSrcAlpha)
            );
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat(
                "_DstBlendAlpha",
                (float)(blend == AdditiveBlend ? BlendMode.One : BlendMode.OneMinusSrcAlpha)
            );
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <param name="alpha">그림 위 자리(u 왼쪽→오른쪽, v 아래→위, 0~1)의 밝기</param>
        private static Texture2D Texture(
            string name,
            int width,
            int height,
            TextureWrapMode wrap,
            Func<float, float, float> alpha
        )
        {
            string path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;
            BuilderTools.EnsureFolder(Folder);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float value = Mathf.Clamp01(alpha((x + .5f) / width, (y + .5f) / height));
                pixels[y * width + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(value * 255)
                );
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
