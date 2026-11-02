using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Characters
{
    public static class MaleBaseVariantBuilder
    {
        public const string VariantPath =
            "Assets/Prefabs/Characters/Mannequin/Npc/LittleMung_Npc_Male.prefab";
        public const string FaceScenePath =
            "Assets/Scenes/Sandbox/Mannequin/LittleMung_Npc_Male_Face.unity";
        public const string MotionScenePath =
            "Assets/Scenes/Sandbox/Mannequin/LittleMung_Npc_Male_Motion.unity";
        private const string LegacyName = "LittleMung_Base_Female";
        private const string MaleName = "LittleMung_Base_Male";
        private const string Art = MannequinMasterBuilder.ArtFolder;
        private const string Scenes = MannequinMasterBuilder.SceneFolder;
        private const string VisualPath = Art + "/LittleMung_Base_Male_Visual.prefab";
        private static readonly string[] SkinMaterials =
        {
            "MannequinSkin",
            "SpeakingLips",
            "UpperLeftLid",
            "LowerLeftLid",
            "UpperRightLid",
            "LowerRightLid",
        };

        [MenuItem("Tools/A305/Characters/Prepare Male Base And NPC Variant")]
        public static void Prepare()
        {
            BuilderTools.ThrowIfPlaying();
            MoveReviewedAssets();
            string shaderPath = Art + "/CharacterSkin.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Character skin shader must compile first.");
            string maskPath = Art + "/MannequinSkinMask.png";
            BuildSkinMask(maskPath);
            foreach (string name in SkinMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + name + ".mat");
                if (material == null)
                    throw new InvalidOperationException("Missing skin material: " + name);
                if (material.shader == shader)
                    continue;
                material.shader = shader;
                material.SetVector(CharacterSkinTone.TintProperty, Vector4.one);
                material.SetTexture(
                    "_SkinMask",
                    name == "MannequinSkin"
                        ? AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath)
                        : Texture2D.whiteTexture
                );
                EditorUtility.SetDirty(material);
            }
            var contents = PrefabUtility.LoadPrefabContents(VisualPath);
            try
            {
                contents.name = MaleName + "_Visual";
                var tone = contents.GetComponent<CharacterSkinTone>();
                if (tone == null)
                {
                    tone = contents.AddComponent<CharacterSkinTone>();
                    tone.Configure(
                        contents
                            .GetComponentsInChildren<Renderer>(true)
                            .Where(r =>
                                r.sharedMaterials.Any(m =>
                                    m != null && SkinMaterials.Contains(m.name)
                                )
                            )
                            .ToArray()
                    );
                }
                PrefabUtility.SaveAsPrefabAsset(contents, VisualPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            CreateVariant();
            CreateFacePreview();
            CreateMotionPreview();
            AssetDatabase.SaveAssets();
        }

        private static void MoveReviewedAssets()
        {
            Move(Art + "/" + LegacyName + "_Visual.prefab");
            foreach (int height in new[] { 160, 170 })
            {
                string prefab =
                    MannequinMasterBuilder.PrefabFolder
                    + "/"
                    + LegacyName
                    + "_"
                    + height
                    + ".prefab";
                Move(prefab);
                RenameRoot(prefab.Replace(LegacyName, MaleName));
                Move(Art + "/" + LegacyName + "_" + height + "_Camera.asset");
                Move(Scenes + "/" + LegacyName + "_" + height + "_Motion.unity");
            }
            Move(Scenes + "/" + LegacyName + "_Face.unity");
            foreach (string suffix in new[] { "_Face", "_160_Motion", "_170_Motion" })
            {
                string path = Scenes + "/" + MaleName + suffix + ".unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (
                    var item in scene
                        .GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                )
                    if (item.name.StartsWith(LegacyName, StringComparison.Ordinal))
                    {
                        item.name = item.name.Replace(LegacyName, MaleName);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(item.gameObject);
                    }
                EditorSceneManager.SaveScene(scene);
            }
            string authorFolder = Path.GetFullPath("../ArtSources/Characters/LittleMungMannequin");
            string oldBlend = Path.Combine(authorFolder, LegacyName + "_Face.blend");
            string newBlend = Path.Combine(authorFolder, MaleName + "_Face.blend");
            if (File.Exists(oldBlend))
            {
                if (File.Exists(newBlend))
                    throw new IOException(
                        "Preserve both authored files; destination already exists."
                    );
                File.Move(oldBlend, newBlend);
            }
        }

        private static void Move(string oldPath)
        {
            string path = oldPath.Replace(LegacyName, MaleName);
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    throw new FileNotFoundException(path);
                return;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new IOException("Destination exists: " + path);
            string error = AssetDatabase.MoveAsset(oldPath, path);
            if (!string.IsNullOrEmpty(error))
                throw new IOException(error);
        }

        private static void RenameRoot(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = root.name.Replace(LegacyName, MaleName);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildSkinMask(string path)
        {
            if (File.Exists(path))
                return;
            var source = AssetDatabase
                .LoadAssetAtPath<Material>(Art + "/MannequinSkin.mat")
                .GetTexture("_BaseMap");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source))))
                    throw new InvalidDataException("Cannot decode the reviewed skin atlas.");
                var pixels = image.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    float warm = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.04f, .16f, c.r - c.b));
                    float skin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .50f, c.r));
                    pixels[i] = new Color(warm * skin, warm * skin, warm * skin, 1);
                }
                image.SetPixels(pixels);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void CreateVariant()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) != null)
                return;
            BuilderTools.EnsureFolder(Path.GetDirectoryName(VariantPath).Replace('\\', '/'));
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(parent);
            try
            {
                child.name = "LittleMung_Npc_Male";
                var tone = child.GetComponent<CharacterSkinTone>();
                tone.SetSkinColor(new Color(.72f, .51f, .36f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(tone);
                PrefabUtility.SaveAsPrefabAsset(child, VariantPath);
            }
            finally
            {
                Object.DestroyImmediate(child);
            }
        }

        private static void CreateFacePreview()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FaceScenePath) != null)
            {
                var existing = EditorSceneManager.OpenScene(FaceScenePath, OpenSceneMode.Single);
                StripMovementForFacePreview(
                    Object.FindFirstObjectByType<CharacterFaceController>().gameObject
                );
                EditorSceneManager.SaveScene(existing);
                return;
            }
            if (!AssetDatabase.CopyAsset(Scenes + "/" + MaleName + "_Face.unity", FaceScenePath))
                throw new IOException("Could not create NPC face preview copy.");
            var scene = EditorSceneManager.OpenScene(FaceScenePath, OpenSceneMode.Single);
            var old = Object.FindFirstObjectByType<CharacterFaceController>();
            var child = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath),
                    scene
                );
            child.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            child.transform.localScale = old.transform.localScale;
            child.GetComponentInChildren<Animator>().enabled = false;
            StripMovementForFacePreview(child);
            var panel = Object.FindFirstObjectByType<FacePosePreviewPanel>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var skin = child.GetComponentInChildren<SkinnedMeshRenderer>();
            panel.Configure(
                child.GetComponent<CharacterFaceController>(),
                camera,
                skin.transform.TransformPoint(new Vector3(0, .65f, 0)),
                skin.transform.TransformVector(new Vector3(-2, 0, 0))
            );
            Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.SaveScene(scene);
        }

        private static void StripMovementForFacePreview(GameObject visual)
        {
            foreach (var behavior in visual.GetComponentsInChildren<MonoBehaviour>().Reverse())
                if (behavior is not CharacterFaceController && behavior is not CharacterSkinTone)
                    Object.DestroyImmediate(behavior);
        }

        private static void CreateMotionPreview()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MotionScenePath) != null)
                return;
            if (
                !AssetDatabase.CopyAsset(
                    Scenes + "/" + MaleName + "_170_Motion.unity",
                    MotionScenePath
                )
            )
                throw new IOException("Could not create NPC motion preview copy.");
            var scene = EditorSceneManager.OpenScene(MotionScenePath, OpenSceneMode.Single);
            var player = Object.FindFirstObjectByType<A305.Player.PlayerMover>();
            var old = player.transform.Find("Visual");
            var child = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath),
                    player.transform
                );
            child.transform.localPosition = old.localPosition;
            child.transform.localRotation = old.localRotation;
            child.transform.localScale = old.localScale;
            Object.DestroyImmediate(old.gameObject);
            child.name = "Visual";
            BuilderTools.SetReference(
                player.GetComponent<CharacterAnimationDriver>(),
                "animator",
                child.GetComponentInChildren<Animator>()
            );
            EditorSceneManager.SaveScene(scene);
        }
    }
}
