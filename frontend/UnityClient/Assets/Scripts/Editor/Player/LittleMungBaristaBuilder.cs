using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    public static class LittleMungBaristaBuilder
    {
        public const string ArtFolder = "Assets/Art/Player/LittleMung/Barista";
        public const string PrefabPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungBaristaPlayer.prefab";
        public const string ScenePath =
            "Assets/Scenes/Sandbox/LittleMungBaristaPlayerSandbox.unity";

        [MenuItem("Tools/A305/Player/Build Barista Variant Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            ImportOutfit();
            if (!File.Exists(PrefabPath))
                CreateVariant();
            if (!File.Exists(ScenePath))
                CreateScene();
            AssetDatabase.SaveAssets();
        }

        private static void ImportOutfit()
        {
            const string modelPath = ArtFolder + "/BaristaOutfit.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException(
                    "Export the Blender barista copy first.",
                    modelPath
                );
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var texture =
                AssetImporter.GetAtPath(ArtFolder + "/BaristaBaseColor.png") as TextureImporter;
            texture.maxTextureSize = 2048;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
            var baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Player/LittleMung/Body.mat"
            );
            var body = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/BaristaBody.mat");
            if (body == null)
            {
                body = new Material(baseMaterial) { name = "BaristaBody" };
                body.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/BaristaBaseColor.png")
                );
                AssetDatabase.CreateAsset(body, ArtFolder + "/BaristaBody.mat");
            }
            // Persist editable URP materials; Blender's Principled material is not a URP shader.
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
            {
                string name = source.name;
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    ArtFolder + "/" + name + ".mat"
                );
                if (material == null)
                {
                    var color = MaterialColor(name);
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    {
                        name = name,
                    };
                    material.SetColor("_BaseColor", color);
                    if (name == "IrisGreen")
                        material.SetTexture("_BaseMap", body.GetTexture("_BaseMap"));
                    material.SetFloat("_Smoothness", name == "ApronBrass" ? .55f : .15f);
                    material.SetFloat("_Metallic", name == "ApronBrass" ? .65f : 0f);
                    // Thin cloth must also be visible from the back while posing.
                    material.SetFloat("_Cull", 0f);
                    AssetDatabase.CreateAsset(material, ArtFolder + "/" + name + ".mat");
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
            }
            importer.SaveAndReimport();
        }

        private static Color MaterialColor(string name)
        {
            switch (name)
            {
                case "ForeheadSkin":
                    return new Color(.84f, .48f, .25f).gamma;
                case "GoldenHair":
                    return new Color(.60f, .28f, .055f).gamma;
                case "HairHighlights":
                    return new Color(.72f, .36f, .08f).gamma;
                case "TerracottaShirt":
                    return new Color(.47f, .125f, .065f).gamma;
                case "SageTrousers":
                    return new Color(.25f, .30f, .17f).gamma;
                case "CreamApron":
                    return new Color(.83f, .63f, .38f).gamma;
                case "ApronSeam":
                    return new Color(.57f, .37f, .19f).gamma;
                case "ShirtButtons":
                    return new Color(.36f, .085f, .04f).gamma;
                case "IrisGreen":
                    return new Color(.64f, 1.6f, .55f);
                default:
                    throw new InvalidOperationException("Unknown outfit material: " + name);
            }
        }

        [MenuItem("Tools/A305/Player/Refresh Barista Appearance")]
        public static void RefreshAppearance()
        {
            if (!BuilderTools.CanBuild())
                return;
            ImportOutfit();
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyAppearance(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
        }

        private static void CreateVariant()
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(
                LittleMungStairBuilder.PrefabPath
            );
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(parent);
            try
            {
                instance.name = "LittleMungBaristaPlayer";
                ApplyAppearance(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ApplyAppearance(GameObject instance)
        {
            var imported = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/BaristaOutfit.fbx")
            );
            var parent = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(LittleMungStairBuilder.PrefabPath)
            );
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                var previousOutfit = animator.transform.Find("BaristaOutfit");
                if (previousOutfit != null)
                    Object.DestroyImmediate(previousOutfit.gameObject);
                var bones = animator.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var baseSkin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
                var parentAnimator = parent.GetComponentInChildren<Animator>();
                foreach (var source in imported.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (source.name == "BaristaBody")
                    {
                        baseSkin.sharedMesh = CopyParentWeights(
                            parentAnimator.GetComponentInChildren<SkinnedMeshRenderer>(),
                            parentAnimator.transform,
                            source,
                            imported.transform
                        );
                        baseSkin.bones = source.bones.Select(b => FindBone(bones, b)).ToArray();
                        baseSkin.rootBone = FindBone(bones, source.rootBone);
                        baseSkin.sharedMaterials = source.sharedMaterials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(baseSkin);
                        continue;
                    }
                    var part = Object.Instantiate(source.gameObject, animator.transform);
                    part.name = source.name;
                    part.transform.localPosition = imported.transform.InverseTransformPoint(
                        source.transform.position
                    );
                    part.transform.localRotation =
                        Quaternion.Inverse(imported.transform.rotation) * source.transform.rotation;
                    part.transform.localScale = source.transform.lossyScale;
                    var skin = part.GetComponent<SkinnedMeshRenderer>();
                    skin.bones = source.bones.Select(b => FindBone(bones, b)).ToArray();
                    skin.rootBone = FindBone(bones, source.rootBone);
                    skin.updateWhenOffscreen = true;
                    skin.localBounds = source.localBounds;
                }
            }
            finally
            {
                Object.DestroyImmediate(imported);
                Object.DestroyImmediate(parent);
            }
        }

        private static Mesh CopyParentWeights(
            SkinnedMeshRenderer parent,
            Transform parentRoot,
            SkinnedMeshRenderer appearance,
            Transform importedRoot
        )
        {
            const string path = ArtFolder + "/BaristaBody.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var mesh = Object.Instantiate(appearance.sharedMesh);
            mesh.name = "BaristaBody";
            var parentMatrix = parentRoot.worldToLocalMatrix * parent.transform.localToWorldMatrix;
            var sourceMatrix =
                importedRoot.worldToLocalMatrix * appearance.transform.localToWorldMatrix;
            var parentVertices = parent.sharedMesh.vertices;
            var lookup = new Dictionary<Vector3Int, int>();
            Vector3Int Key(Vector3 point) =>
                new Vector3Int(
                    Mathf.RoundToInt(point.x * 100000),
                    Mathf.RoundToInt(point.y * 100000),
                    Mathf.RoundToInt(point.z * 100000)
                );
            for (int i = 0; i < parentVertices.Length; i++)
                lookup[Key(parentMatrix.MultiplyPoint3x4(parentVertices[i]))] = i;
            var weights = parent.sharedMesh.boneWeights;
            var mapping = parent
                .bones.Select(b =>
                    Array.FindIndex(appearance.bones, target => target.name == b.name)
                )
                .ToArray();
            var targetWeights = mesh.boneWeights;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (
                    !lookup.TryGetValue(
                        Key(sourceMatrix.MultiplyPoint3x4(vertices[i])),
                        out int original
                    )
                )
                {
                    var retained = targetWeights[i];
                    if (
                        appearance.bones[retained.boneIndex0].name != "Head"
                        || retained.weight0 < .5f
                    )
                        throw new InvalidOperationException(
                            "Only reshaped hair may differ from the parent: " + i
                        );
                    continue;
                }
                var weight = weights[original];
                weight.boneIndex0 = mapping[weight.boneIndex0];
                weight.boneIndex1 = mapping[weight.boneIndex1];
                weight.boneIndex2 = mapping[weight.boneIndex2];
                weight.boneIndex3 = mapping[weight.boneIndex3];
                if (
                    weight.boneIndex0 < 0
                    || weight.boneIndex1 < 0
                    || weight.boneIndex2 < 0
                    || weight.boneIndex3 < 0
                )
                    throw new InvalidOperationException(
                        "Parent skin bone is missing from the appearance export."
                    );
                targetWeights[i] = weight;
            }
            mesh.boneWeights = targetWeights;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Transform FindBone(Dictionary<string, Transform> bones, Transform source)
        {
            if (source == null || !bones.TryGetValue(source.name, out var bone))
                throw new InvalidOperationException(
                    "Outfit bone missing from the parent: " + source?.name
                );
            return bone;
        }

        private static void CreateScene()
        {
            if (!AssetDatabase.CopyAsset(LittleMungStairBuilder.ScenePath, ScenePath))
                throw new IOException("Could not create the separate barista sandbox.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldPlayer = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            var player = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                oldPlayer.transform.parent,
                oldPlayer.transform.position,
                oldPlayer.transform.rotation
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera.gameObject);
            Object.DestroyImmediate(oldPlayer.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
