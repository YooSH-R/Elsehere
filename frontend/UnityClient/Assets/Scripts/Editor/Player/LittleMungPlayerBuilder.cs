using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    /// <summary>공용 Player와 Production을 변경하지 않고 전용 검증 씬을 만든다.</summary>
    public static class LittleMungPlayerBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/LittleMungPlayerSandbox.unity";
        public const string PrefabPath = "Assets/Prefabs/Player/LittleMung/LittleMungPlayer.prefab";
        public const string ArtFolder = LittleMungAnimationBuilder.Folder;
        private const string DataFolder = "Assets/Data/Player/LittleMung";
        private const string MovementSettingsPath = DataFolder + "/Movement.asset";
        private const string CameraSettingsPath = "Assets/Data/Player/PlayerCameraSettings.asset";
        private const float JumpHeightMeters = 0.675f;
        private const float CameraRotateDegreesPerPixel = 0.15f;

        // 빠르게 방향을 바꾸면 카메라가 등 뒤로 크게 돌아 뒤로 가는데 앞으로 가는 것처럼 보여 끈다.
        private const float CameraFollowBehindSharpness = 0f;

        /// <summary>이미 만든 이동 설정·애니메이터·카메라 감도와 자동 뒤돌기를 현재 조작값으로 갱신한다. 프리팹과 씬은 그대로 둔다.</summary>
        [MenuItem("Tools/A305/Player/Apply Movement Tuning")]
        public static void ApplyMovementTuning()
        {
            if (!BuilderTools.CanBuild())
                return;
            LittleMungAnimationBuilder.ApplyLocomotionSpeeds();
            ApplyMovementSettings(
                BuilderTools.LoadOrCreateAsset<PlayerMovementSettings>(MovementSettingsPath)
            );
            var camera = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(CameraSettingsPath)
            );
            camera.FindProperty("rotateDegreesPerPixel").floatValue = CameraRotateDegreesPerPixel;
            camera.FindProperty("followBehindSharpness").floatValue = CameraFollowBehindSharpness;
            camera.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("이동 속도·애니메이션 재생 속도·카메라 감도를 갱신했습니다.");
        }

        [MenuItem("Tools/A305/Player/Build Little Mung Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            var controller = LittleMungAnimationBuilder.Build();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                prefab = BuildPrefab(controller);
            if (File.Exists(ScenePath))
            {
                Debug.Log("기존 LittleMung 검증 씬을 보존했습니다: " + ScenePath);
                return;
            }
            BuildScene(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("플레이어 검증 씬 생성 완료: " + ScenePath);
        }

        private static GameObject BuildPrefab(RuntimeAnimatorController controller)
        {
            BuilderTools.EnsureFolder(ArtFolder);
            BuilderTools.EnsureFolder(Path.GetDirectoryName(PrefabPath));
            BuilderTools.EnsureFolder(DataFolder);
            foreach (string name in new[] { "LittleMung.fbx", "BaseColor.png" })
                if (!File.Exists(ArtFolder + "/" + name))
                    throw new FileNotFoundException(
                        "버전 관리된 플레이어 에셋이 필요합니다. 저장소의 에셋을 복원하세요.",
                        ArtFolder + "/" + name
                    );
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/Body.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/BaseColor.png")
                );
                material.SetFloat("_Smoothness", 0.2f);
                material.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(material, ArtFolder + "/Body.mat");
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(ArtFolder + "/LittleMung.fbx");
            foreach (var entry in importer.GetExternalObjectMap().Keys.ToArray())
                importer.AddRemap(entry, material);
            importer.SaveAndReimport();

            var root = new GameObject("LittleMungPlayer");
            try
            {
                root.SetActive(false);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var model = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/LittleMung.fbx"),
                        visual.transform
                    );
                model.name = "Model";
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMaterials = Enumerable
                        .Repeat(material, skin.sharedMaterials.Length)
                        .ToArray();
                    skin.updateWhenOffscreen = true;
                }
                var animator = model.GetComponent<Animator>();
                if (
                    animator == null
                    || animator.avatar == null
                    || !animator.avatar.isHuman
                    || !animator.avatar.isValid
                )
                    throw new InvalidOperationException(
                        "플레이어용 유효한 Humanoid Avatar가 필요합니다."
                    );
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Bounds bounds = BoundsOf(model);
                visual.transform.localScale = Vector3.one * (1.4f / bounds.size.y);
                // Humanoid가 원본 바인드 방향을 정규화하므로 실제 대기 포즈에서 정면을 계산한다.
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtFolder + "/Idle.anim");
                idle.SampleAnimation(model, 0f);
                visual.transform.localRotation = Quaternion.FromToRotation(
                    BuilderTools.GetCharacterViewDirection(model, 0),
                    Vector3.forward
                );
                bounds = BakedBounds(model);
                visual.transform.position -= new Vector3(
                    bounds.center.x,
                    bounds.min.y,
                    bounds.center.z
                );
                var capsule = root.AddComponent<CharacterController>();
                capsule.height = 1.4f;
                capsule.radius = Mathf.Clamp(bounds.extents.x * 0.95f, 0.3f, 0.65f);
                capsule.center = new Vector3(0f, 0.7f, 0f);
                capsule.stepOffset = 0.15f;
                capsule.skinWidth = 0.025f;
                var driver = root.AddComponent<CharacterAnimationDriver>();
                var mover = root.AddComponent<PlayerMover>();
                BuilderTools.SetReference(driver, "animator", animator);
                var settings = BuilderTools.LoadOrCreateAsset<PlayerMovementSettings>(
                    MovementSettingsPath
                );
                ApplyMovementSettings(settings);
                BuilderTools.SetReference(mover, "movementSettings", settings);
                BuilderTools.SetReference(mover, "animationDriver", driver);
                BuilderTools.SetReference(
                    mover,
                    "moveAction",
                    BuilderTools.ActionReference("Player", "Move")
                );
                BuilderTools.SetReference(
                    mover,
                    "sprintAction",
                    BuilderTools.ActionReference("Player", "Sprint")
                );
                BuilderTools.SetReference(
                    mover,
                    "jumpAction",
                    BuilderTools.ActionReference("Player", "Jump")
                );
                root.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CopyIfMissing(string source, string target)
        {
            if (!File.Exists(target) && !AssetDatabase.CopyAsset(source, target))
                throw new IOException("플레이어 복사본을 만들 수 없습니다: " + source);
        }

        private static void ApplyMovementSettings(PlayerMovementSettings settings)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("walkSpeedMetersPerSecond").floatValue =
                LittleMungAnimationBuilder.WalkSpeed;
            serialized.FindProperty("sprintSpeedMetersPerSecond").floatValue =
                LittleMungAnimationBuilder.RunSpeed;
            serialized.FindProperty("jumpHeightMeters").floatValue = JumpHeightMeters;
            serialized.FindProperty("gravityMetersPerSecondSquared").floatValue = 9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Bounds BoundsOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds BakedBounds(GameObject model)
        {
            Bounds bounds = new Bounds();
            bool initialized = false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = skin.transform.TransformPoint(vertex);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else
                        bounds.Encapsulate(point);
                }
                Object.DestroyImmediate(mesh);
            }
            return bounds;
        }

        private static void BuildScene(GameObject prefab)
        {
            var groups = BuilderTools.NewSceneWithGroups();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.68f, 0.72f);
            var floor = BuilderTools.GetOrCreateMaterial(
                ArtFolder,
                "Floor",
                new Color(0.29f, 0.39f, 0.42f)
            );
            var obstacle = BuilderTools.GetOrCreateMaterial(
                ArtFolder,
                "Obstacle",
                new Color(0.68f, 0.53f, 0.35f)
            );
            BuilderTools.CreateShape(
                PrimitiveType.Plane,
                "Ground",
                groups.Environment,
                Vector3.zero,
                Vector3.one * 4,
                floor
            );
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Wall",
                groups.Environment,
                new Vector3(0, 1, 8),
                new Vector3(10, 2, 0.3f),
                obstacle
            );
            var ramp = BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Ramp",
                groups.Environment,
                new Vector3(4, 0.4f, 3),
                new Vector3(2, 0.2f, 4),
                obstacle
            );
            ramp.localRotation = Quaternion.Euler(-12, 0, 0);
            for (int i = 1; i <= 4; i++)
                BuilderTools.CreateShape(
                    PrimitiveType.Cube,
                    "Step" + i,
                    groups.Environment,
                    new Vector3(-3, i * 0.05f, 1 + i * 0.5f),
                    new Vector3(2, i * 0.1f, 0.5f),
                    obstacle
                );
            for (int i = -8; i <= 8; i++)
                BuilderTools.CreateShape(
                    PrimitiveType.Cube,
                    "FloorLine" + i,
                    groups.Environment,
                    new Vector3(i, 0.002f, 0),
                    new Vector3(0.01f, 0.002f, 16),
                    obstacle,
                    keepCollider: false
                );
            var player = BuilderTools.InstantiatePrefab(
                prefab,
                groups.Characters,
                Vector3.zero,
                Quaternion.identity
            );
            var camera = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSandboxBuilder.CameraPrefabPath),
                groups.Systems,
                new Vector3(0, 2, -4),
                Quaternion.Euler(15, 0, 0)
            );
            string cameraSettingsPath = DataFolder + "/Camera.asset";
            CopyIfMissing("Assets/Data/Player/PlayerCameraSettings.asset", cameraSettingsPath);
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(cameraSettingsPath);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("startDistanceMeters").floatValue = 3.5f;
            serialized.FindProperty("pivotHeightMeters").floatValue = 0.85f;
            serialized.FindProperty("eyeHeightMeters").floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SetReference(
                camera.GetComponent<PlayerCameraRig>(),
                "cameraSettings",
                settings
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera);
            AddInstructions(groups.UI);
            BuilderTools.SaveScene(groups.Scene, ScenePath);
        }

        private static void AddInstructions(Transform parent)
        {
            var canvas = new GameObject("Controls", typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(parent, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var label = new GameObject(
                "ControlsText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            label.transform.SetParent(canvas.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset"
            );
            text.text =
                "리틀 멍 · 플레이어 동작 검수\nWASD 이동  |  Shift 달리기  |  Space 점프\n마우스 왼쪽 버튼 + 드래그: 시점 회전  |  휠: 확대\n왼쪽: 계단 · 오른쪽: 경사로 · 앞쪽: 벽\n원본 외형 복사본 / 표정·립싱크 미적용";
            text.fontSize = 22;
            text.raycastTarget = false;
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(24, -20);
            rect.sizeDelta = new Vector2(-48, 170);
        }
    }
}
