using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace A305.EditorTools.World
{
    /// <summary>
    /// Assets/ThirdParty의 외부 모델을 고치지 않고 인스턴스로 놓는다. 크기는 겉모양 크기에 맞추고,
    /// 재질은 Builder가 정한 폴더의 URP 재질로 바꿔 끼운다.
    /// </summary>
    internal static class ThirdPartyModelKit
    {
        internal enum Fit
        {
            Height,
            Length,
        }

        internal static GameObject Model(
            string path,
            string name,
            Transform parent,
            Vector3 bottomCenter,
            float yaw,
            float size,
            Fit fit,
            string materialFolder
        )
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new InvalidOperationException("모델을 찾지 못했습니다: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.localRotation =
                Quaternion.Euler(0, yaw, 0) * instance.transform.localRotation;
            string family = Path.GetFileName(Path.GetDirectoryName(path));
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer
                    .sharedMaterials.Select(material => Urp(material, family, materialFolder))
                    .ToArray();
            Place(instance, bottomCenter, size, fit);
            return instance;
        }

        // bottomCenter는 부모 기준 좌표다. 겉모양의 바닥 가운데가 그 점에 오도록 크기와 위치를 맞춘다.
        internal static void Place(GameObject target, Vector3 bottomCenter, float size, Fit fit)
        {
            Transform t = target.transform;
            Vector3 baseScale = t.localScale / Mathf.Max(t.localScale.x, .0001f);
            t.localScale = baseScale;
            Bounds bounds = BoundsOf(target);
            float parentScale = t.parent != null ? t.parent.lossyScale.x : 1f;
            float current =
                (fit == Fit.Height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z))
                / parentScale;
            t.localScale = baseScale * (size / Mathf.Max(current, .0001f));
            bounds = BoundsOf(target);
            Vector3 world = t.parent != null ? t.parent.TransformPoint(bottomCenter) : bottomCenter;
            t.position += world - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        internal static Bounds BoundsOf(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material Urp(Material source, string family, string materialFolder)
        {
            if (source == null)
                return null;
            string name =
                family + "_" + new string(source.name.Where(char.IsLetterOrDigit).ToArray());
            string path = $"{materialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            Color color =
                source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.color
                : Color.white;
            Texture texture =
                source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex")
                : null;
            Material material = BuilderTools.GetOrCreateMaterial(materialFolder, name, color);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", .2f);
            if (color.a < .99f)
                MakeTransparent(material);
            else if (HasAlpha(texture))
                MakeCutout(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool HasAlpha(Texture texture)
        {
            if (texture == null)
                return false;
            var importer =
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            return importer != null && importer.DoesSourceTextureHaveAlpha();
        }

        // 원본에서 반투명인 재질(냉장고 유리문 등)은 URP에서도 반투명으로 둔다.
        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        // 잎·꽃처럼 투명한 부분이 있는 텍스처는 그 부분을 잘라 내고 양면으로 그린다.
        internal static void MakeCutout(Material material)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", .5f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
    }
}
