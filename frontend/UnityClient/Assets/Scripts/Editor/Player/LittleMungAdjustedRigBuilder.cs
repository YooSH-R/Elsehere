using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    /// <summary>기존 검수 에셋을 보존하고 수정한 발 골격의 플레이 가능한 사본을 만든다.</summary>
    public static class LittleMungAdjustedRigBuilder
    {
        public const string ModelPath =
            "Assets/Art/Player/LittleMung/AdjustedRig/LittleMungAdjusted.fbx";
        public const string PrefabPath =
            "Assets/Prefabs/Player/LittleMung/LittleMungAdjustedPlayer.prefab";
        public const string ScenePath =
            "Assets/Scenes/Sandbox/LittleMungAdjustedPlayerSandbox.unity";

        [MenuItem("Tools/A305/Player/Build Adjusted Foot Rig Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            ImportModel();
            if (!File.Exists(PrefabPath))
                CreatePrefab();
            if (!File.Exists(ScenePath))
                CreateScene();
            AssetDatabase.SaveAssets();
        }

        private static void ImportModel()
        {
            if (!File.Exists(ModelPath))
                throw new FileNotFoundException(
                    "export_player.py로 Unity용 FBX를 먼저 내보내세요.",
                    ModelPath
                );
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var original = (ModelImporter)
                AssetImporter.GetAtPath(LittleMungPlayerBuilder.ArtFolder + "/LittleMung.fbx");
            var description = importer.humanDescription;
            // 대응 이름만 재사용한다. 수정된 관절의 휴식 자세는 새 FBX에서 가져온다.
            description.human = original.humanDescription.human;
            importer.humanDescription = description;
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                LittleMungPlayerBuilder.ArtFolder + "/Body.mat"
            );
            foreach (
                var sourceMaterial in AssetDatabase
                    .LoadAllAssetsAtPath(ModelPath)
                    .OfType<Material>()
            )
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(sourceMaterial),
                    material
                );
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().Single();
            if (!avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException(
                    "수정 골격의 Humanoid Avatar가 유효하지 않습니다."
                );
        }

        private static void CreatePrefab()
        {
            if (!AssetDatabase.CopyAsset(LittleMungPlayerBuilder.PrefabPath, PrefabPath))
                throw new IOException("기존 플레이어의 검수 사본 생성에 실패했습니다.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var visual = root.transform.Find("Visual");
                Object.DestroyImmediate(visual.Find("Model").gameObject);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                var model = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),
                        visual
                    );
                model.name = "Model";
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    LittleMungPlayerBuilder.ArtFolder + "/Body.mat"
                );
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.sharedMaterials = Enumerable
                        .Repeat(material, skin.sharedMaterials.Length)
                        .ToArray();
                    skin.updateWhenOffscreen = true;
                }
                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        LittleMungAnimationBuilder.ControllerPath
                    );
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visual.localScale =
                    Vector3.one * (1.4f / LittleMungPlayerBuilder.BoundsOf(model).size.y);
                AssetDatabase
                    .LoadAssetAtPath<AnimationClip>(
                        LittleMungPlayerBuilder.ArtFolder + "/Idle.anim"
                    )
                    .SampleAnimation(model, 0f);
                visual.localRotation = Quaternion.FromToRotation(
                    BuilderTools.GetCharacterViewDirection(model, 0),
                    Vector3.forward
                );
                var bounds = BakedBounds(model);
                visual.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                BuilderTools.SetReference(
                    root.GetComponent<CharacterAnimationDriver>(),
                    "animator",
                    animator
                );
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Bounds BakedBounds(GameObject model)
        {
            Bounds result = default;
            bool first = true;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (first)
                        {
                            result = new Bounds(point, Vector3.zero);
                            first = false;
                        }
                        else
                            result.Encapsulate(point);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            return result;
        }

        private static void CreateScene()
        {
            if (!AssetDatabase.CopyAsset(LittleMungPlayerBuilder.ScenePath, ScenePath))
                throw new IOException("검수 씬 사본 생성에 실패했습니다.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var previous = Object.FindFirstObjectByType<PlayerMover>();
            var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
            var replacement = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                previous.transform.parent,
                previous.transform.position,
                previous.transform.rotation
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(replacement, camera.gameObject);
            Object.DestroyImmediate(previous.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
