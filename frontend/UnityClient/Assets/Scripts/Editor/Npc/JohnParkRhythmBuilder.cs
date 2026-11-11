using System;
using System.IO;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>앰프를 잡은 원본 자세를 전용 Generic Avatar로 재생하는 독립 프리팹.</summary>
    public static class JohnParkRhythmBuilder
    {
        public const string AssetName = "LittleMung_JohnPark_Male";
        public const string ArtFolder = "Assets/Art/Npc/" + AssetName;
        public const string ModelPath = ArtFolder + "/" + AssetName + ".fbx";
        public const string PrefabPath = "Assets/Prefabs/Npc/" + AssetName + ".prefab";
        public const string ControllerPath = ArtFolder + "/JohnParkRhythm.controller";
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcJohnParkRhythmSandbox.unity";
        public const string StateName = "Rhythm";

        [MenuItem("Tools/A305/Npc/John Park/Build Rhythm Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            ConfigureModel();
            var material = PrepareMaterial();
            var controller = PrepareController();
            var prefab = PreparePrefab(material, controller);
            BuildSandbox(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("존박 리듬 프리팹과 검수 씬을 만들었습니다: " + ScenePath);
        }

        private static void ConfigureModel()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "존박 골격 복사본 FBX가 없습니다: " + ModelPath
                );

            // Humanoid 재해석으로 비대칭 오른팔 자세가 바뀌지 않도록 원본 본 곡선을 유지한다.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "";
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
                throw new InvalidOperationException(
                    "리듬 동작 한 개가 필요합니다: " + clips.Length
                );
            clips[0].name = "JohnPark_AmpRhythm";
            clips[0].loopTime = true;
            clips[0].loopPose = false;
            clips[0].keepOriginalOrientation = true;
            clips[0].keepOriginalPositionXZ = true;
            clips[0].lockRootRotation = true;
            clips[0].lockRootPositionXZ = true;
            clips[0].lockRootHeightY = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static Material PrepareMaterial()
        {
            string texturePath = ArtFolder + "/" + AssetName + "_BaseColor.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            string materialPath = ArtFolder + "/" + AssetName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit 셰이더가 없습니다.");
            material = new Material(shader) { name = AssetName };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static AnimatorController PrepareController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
                return controller;
            var clip = AssetDatabase
                .LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Single(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState(StateName);
            state.motion = clip;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            return controller;
        }

        private static GameObject PreparePrefab(Material material, AnimatorController controller)
        {
            BuilderTools.EnsureFolder(Path.GetDirectoryName(PrefabPath));
            bool exists = File.Exists(PrefabPath);
            var root = exists
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject(AssetName);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                    visual = (
                        (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform)
                    ).transform;
                    visual.name = "Visual";
                    // 남성 NPC의 기본 키. 다시 실행할 때 사용자가 조정한 크기는 유지한다.
                    visual.localScale *= 1.7f / BuilderTools.GetModelHeight(visual.gameObject);
                    foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        renderer.sharedMaterials = Enumerable
                            .Repeat(material, renderer.sharedMaterials.Length)
                            .ToArray();
                        renderer.updateWhenOffscreen = true;
                    }
                }
                var animator = visual.GetComponent<Animator>();
                if (animator == null)
                    animator = visual.gameObject.AddComponent<Animator>();
                animator.avatar = AssetDatabase
                    .LoadAllAssetsAtPath(ModelPath)
                    .OfType<Avatar>()
                    .Single();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        private static void BuildSandbox(GameObject prefab)
        {
            var groups = BuilderTools.NewSceneWithGroups();
            BuilderTools
                .InstantiatePrefab(
                    prefab,
                    groups.Characters,
                    new Vector3(.95f, 0, 0),
                    Quaternion.identity
                )
                .name = "JohnParkFront";
            BuilderTools
                .InstantiatePrefab(
                    prefab,
                    groups.Characters,
                    new Vector3(-.95f, 0, 0),
                    Quaternion.Euler(0, -35, 0)
                )
                .name = "JohnParkThreeQuarter";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(groups.Environment, false);
            floor.transform.localScale = Vector3.one * 2;
            floor.GetComponent<Renderer>().sharedMaterial = BuilderTools.GetOrCreateMaterial(
                ArtFolder,
                "PreviewFloor",
                new Color(.27f, .3f, .34f)
            );
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(groups.Systems, false);
            camera.transform.position = new Vector3(0, 1.05f, 5);
            camera.transform.LookAt(new Vector3(0, .85f, 0));
            camera.orthographic = true;
            camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.32f, .35f, .4f);
            var light = groups.Lighting.GetComponentInChildren<Light>();
            light.transform.rotation = Quaternion.Euler(35, -145, 0);
            light.intensity = 1.3f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            BuilderTools.EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(groups.Scene, ScenePath);
        }
    }
}
