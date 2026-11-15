using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.EditorTools.World
{
    public static class ShopModelsBuilder
    {
        private const string Art = "Assets/Art/World/ShopModels";
        private const string Prefabs = "Assets/Prefabs/World/ShopModels";

        [MenuItem("Tools/A305/World/Apply Shop Models")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (string model in new[] { "Washer", "Dryer", "BarberChair", "ArcadeCabinet" })
                Prepare(model);
            foreach (
                string path in new[]
                {
                    "Assets/Prefabs/World/TownStorefronts/SudsLaundromat.prefab",
                    "Assets/Prefabs/World/TownStorefronts/MainStreetBarber.prefab",
                }.Concat(Directory.GetFiles(TownShopInteriorProps.Folder, "ArcadeCabinet*.prefab"))
            )
                BuilderTools.UpdatePrefabIfExists(path.Replace('\\', '/'), Apply);
            foreach (
                string name in new[]
                {
                    "SudsLaundromatInterior",
                    "MainStreetBarberInterior",
                    "LittleArcadeInterior",
                }
            )
            {
                var scene = EditorSceneManager.OpenScene(
                    TownShopSceneBuilder.Folder + "/" + name + ".unity"
                );
                foreach (GameObject root in scene.GetRootGameObjects())
                    Apply(root);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Prepare(string model)
        {
            string path = Art + "/" + model + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
            string texturePath = Art + "/" + model + "_BaseColor.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            var settings = textureImporter.GetPlatformTextureSettings("WebGL");
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.Automatic;
            textureImporter.SetPlatformTextureSettings(settings);
            textureImporter.SaveAndReimport();
            string materialPath = Art + "/" + model + ".mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                );
                material.SetFloat("_Smoothness", .18f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            BuilderTools.CreatePrefabIfMissing(
                Prefabs + "/" + model + ".prefab",
                () =>
                {
                    var root = new GameObject(model);
                    var visual = (GameObject)
                        PrefabUtility.InstantiatePrefab(
                            AssetDatabase.LoadAssetAtPath<GameObject>(path)
                        );
                    visual.transform.SetParent(root.transform, false);
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer
                            .sharedMaterials.Select(_ =>
                                AssetDatabase.LoadAssetAtPath<Material>(materialPath)
                            )
                            .ToArray();
                    return root;
                }
            );
        }

        private static void Apply(GameObject root)
        {
            foreach (Transform target in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (target == null)
                    continue;
                bool insideVisual = false;
                for (
                    Transform ancestor = target.parent;
                    ancestor != null;
                    ancestor = ancestor.parent
                )
                    insideVisual |= ancestor.name == "TripoVisual";
                if (insideVisual)
                    continue;
                string model =
                    target.name == "WashingMachine" ? "Washer"
                    : target.name == "Dryer" ? "Dryer"
                    : target.name == "BarberChair" ? "BarberChair"
                    : target.name.StartsWith("ArcadeCabinet", StringComparison.Ordinal)
                        ? "ArcadeCabinet"
                    : null;
                if (model == null)
                    continue;
                Transform[] installed = target
                    .Cast<Transform>()
                    .Where(child => child.name == "TripoVisual")
                    .ToArray();
                if (installed.Length > 1)
                    foreach (
                        Transform duplicate in installed.Where(child =>
                            PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == null
                        )
                    )
                        UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                if (target.Find("TripoVisual") != null)
                    continue;
                Renderer[] originals = target.GetComponentsInChildren<Renderer>();
                if (originals.Length == 0)
                    continue;
                Bounds bounds = LocalBounds(target, originals);
                var visual = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + model + ".prefab")
                    );
                visual.name = "TripoVisual";
                visual.transform.SetParent(target, false);
                Vector3 inheritedScale = target.lossyScale;
                visual.transform.localScale = new Vector3(
                    1 / inheritedScale.x,
                    1 / inheritedScale.y,
                    1 / inheritedScale.z
                );
                visual.transform.localRotation = Quaternion.Euler(
                    0,
                    model == "Washer" ? 90
                        : model == "BarberChair" ? -90
                        : 0,
                    0
                );
                Bounds source = LocalBounds(target, visual.GetComponentsInChildren<Renderer>());
                float scale = Mathf.Min(
                    bounds.size.x / source.size.x,
                    bounds.size.y / source.size.y,
                    bounds.size.z / source.size.z
                );
                visual.transform.localScale *= scale;
                visual.transform.localPosition = bounds.center - source.center * scale;
                foreach (Renderer renderer in originals)
                {
                    renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                Debug.Log("SHOP_MODEL " + model + " " + target.name + " " + bounds.size);
            }
        }

        private static Bounds LocalBounds(Transform root, Renderer[] renderers)
        {
            var result = new Bounds();
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(
                            bounds.center
                                + Vector3.Scale(
                                    bounds.extents,
                                    new Vector3(
                                        (corner & 1) == 0 ? -1 : 1,
                                        (corner & 2) == 0 ? -1 : 1,
                                        (corner & 4) == 0 ? -1 : 1
                                    )
                                )
                        )
                    );
                    if (!initialized)
                        result = new Bounds(point, Vector3.zero);
                    else
                        result.Encapsulate(point);
                    initialized = true;
                }
            }
            return result;
        }
    }
}
