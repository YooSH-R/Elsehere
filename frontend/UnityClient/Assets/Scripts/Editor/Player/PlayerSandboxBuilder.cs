using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    /// <summary>
    /// 플레이어 이동·카메라를 확인하는 PlayerSandbox 씬과 Player·PlayerCamera 프리팹, 데이터 에셋,
    /// 카메라 입력 액션을 만든다. 씬·프리팹·머티리얼은 덮어쓰고, 이미 있는 데이터 에셋의 수치는 유지한다.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/Sandbox/PlayerSandbox.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string CameraPrefabPath = "Assets/Prefabs/Player/PlayerCamera.prefab";
        private const string MovementSettingsPath = "Assets/Data/Player/PlayerMovementSettings.asset";
        private const string CameraSettingsPath = "Assets/Data/Player/PlayerCameraSettings.asset";
        private const string ArtFolder = "Assets/Art/Player";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerActionMap = "Player";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";

        // CharacterController와 도형 크기를 맞춘 사람 키(m)
        private const float BodyHeightMeters = 1.8f;
        private const float BodyRadiusMeters = 0.3f;

        [MenuItem("Tools/A305/Player/Build Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play Mode를 끝낸 뒤 PlayerSandbox를 만듭니다.");
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            AddCameraInputActions();
            var movementSettings = LoadOrCreateAsset<PlayerMovementSettings>(MovementSettingsPath);
            var cameraSettings = LoadOrCreateAsset<PlayerCameraSettings>(CameraSettingsPath);

            GameObject playerPrefab = BuildPlayerPrefab(movementSettings);
            GameObject cameraPrefab = BuildCameraPrefab(cameraSettings);
            BuildScene(playerPrefab, cameraPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log($"PlayerSandbox를 만들었습니다: {ScenePath}");
        }

        /// <summary>카메라 회전·줌·시점 전환 액션이 Player 맵에 없으면 추가한다. 이미 있으면 그대로 둔다.</summary>
        private static void AddCameraInputActions()
        {
            string fullPath = Path.GetFullPath(InputActionsPath);
            InputActionAsset actions = InputActionAsset.FromJson(File.ReadAllText(fullPath));
            InputActionMap playerMap = actions.FindActionMap(PlayerActionMap, throwIfNotFound: true);
            bool isChanged = false;

            isChanged |= AddActionIfMissing(
                playerMap,
                "CameraRotate",
                InputActionType.Button,
                "<Mouse>/rightButton",
                expectedControlLayout: "Button"
            );
            isChanged |= AddActionIfMissing(
                playerMap,
                "CameraZoom",
                InputActionType.Value,
                "<Mouse>/scroll/y",
                expectedControlLayout: "Axis"
            );
            isChanged |= AddActionIfMissing(
                playerMap,
                "ToggleView",
                InputActionType.Button,
                "<Keyboard>/v",
                expectedControlLayout: "Button"
            );

            if (!isChanged)
            {
                return;
            }

            File.WriteAllText(fullPath, actions.ToJson());
            Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool AddActionIfMissing(
            InputActionMap map,
            string actionName,
            InputActionType type,
            string binding,
            string expectedControlLayout
        )
        {
            if (map.FindAction(actionName) != null)
            {
                return false;
            }

            map.AddAction(
                actionName,
                type,
                binding,
                groups: KeyboardMouseGroup,
                expectedControlLayout: expectedControlLayout
            );
            return true;
        }

        private static GameObject BuildPlayerPrefab(PlayerMovementSettings movementSettings)
        {
            var root = new GameObject("Player");
            var characterController = root.AddComponent<CharacterController>();
            characterController.height = BodyHeightMeters;
            characterController.radius = BodyRadiusMeters;
            characterController.center = new Vector3(0f, BodyHeightMeters / 2f, 0f);

            var animationDriver = root.AddComponent<CharacterAnimationDriver>();
            var mover = root.AddComponent<PlayerMover>();

            // 모델로 바꿀 때는 Visual 아래만 교체한다. 콜라이더는 루트의 CharacterController가 맡는다.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            CreateShape(
                PrimitiveType.Capsule,
                "Body",
                visual.transform,
                new Vector3(0f, BodyHeightMeters / 2f, 0f),
                new Vector3(BodyRadiusMeters * 2f, BodyHeightMeters / 2f, BodyRadiusMeters * 2f),
                GetOrCreateMaterial("PlayerBody", new Color(0.25f, 0.45f, 0.85f)),
                keepCollider: false
            );
            // 몸이 어느 쪽을 보는지 알 수 있게 앞쪽에 표시를 단다.
            CreateShape(
                PrimitiveType.Cube,
                "FrontMarker",
                visual.transform,
                new Vector3(0f, 1.5f, BodyRadiusMeters),
                new Vector3(0.2f, 0.1f, 0.15f),
                GetOrCreateMaterial("PlayerFront", new Color(0.95f, 0.95f, 0.95f)),
                keepCollider: false
            );

            var moverFields = new SerializedObject(mover);
            moverFields.FindProperty("movementSettings").objectReferenceValue = movementSettings;
            moverFields.FindProperty("moveAction").objectReferenceValue = ActionReference("Move");
            moverFields.FindProperty("sprintAction").objectReferenceValue = ActionReference("Sprint");
            moverFields.FindProperty("jumpAction").objectReferenceValue = ActionReference("Jump");
            moverFields.FindProperty("animationDriver").objectReferenceValue = animationDriver;
            moverFields.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, PlayerPrefabPath);
        }

        private static GameObject BuildCameraPrefab(PlayerCameraSettings cameraSettings)
        {
            var root = new GameObject("PlayerCamera");
            root.tag = "MainCamera";
            root.AddComponent<Camera>();
            root.AddComponent<AudioListener>();
            var rig = root.AddComponent<PlayerCameraRig>();

            var rigFields = new SerializedObject(rig);
            rigFields.FindProperty("cameraSettings").objectReferenceValue = cameraSettings;
            rigFields.FindProperty("lookAction").objectReferenceValue = ActionReference("Look");
            rigFields.FindProperty("rotateHoldAction").objectReferenceValue = ActionReference(
                "CameraRotate"
            );
            rigFields.FindProperty("zoomAction").objectReferenceValue = ActionReference("CameraZoom");
            rigFields.FindProperty("toggleViewAction").objectReferenceValue = ActionReference(
                "ToggleView"
            );
            rigFields.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, CameraPrefabPath);
        }

        private static void BuildScene(GameObject playerPrefab, GameObject cameraPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );
            Transform systems = CreateGroup("Systems");
            Transform environment = CreateGroup("Environment");
            Transform characters = CreateGroup("Characters");
            Transform lighting = CreateGroup("Lighting");

            // 기본 카메라는 PlayerCamera 프리팹으로 대신하고, 기본 조명은 Lighting 그룹으로 옮긴다.
            foreach (GameObject defaultObject in scene.GetRootGameObjects())
            {
                if (defaultObject.GetComponent<Camera>() != null)
                {
                    Object.DestroyImmediate(defaultObject);
                }
                else if (defaultObject.GetComponent<Light>() != null)
                {
                    defaultObject.transform.SetParent(lighting, true);
                }
            }

            BuildEnvironment(environment);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetParent(characters, false);

            var playerCamera = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            playerCamera.transform.SetParent(systems, false);
            playerCamera.transform.SetPositionAndRotation(
                new Vector3(0f, 2.5f, -5f),
                Quaternion.Euler(20f, 0f, 0f)
            );

            Transform dialogueLookTarget = CreateDialogueTarget(characters);

            SetReference(player.GetComponent<PlayerMover>(), "cameraTransform", playerCamera.transform);
            SetReference(playerCamera.GetComponent<PlayerCameraRig>(), "followTarget", player.transform);

            var probeObject = new GameObject("DialogueViewProbe");
            probeObject.transform.SetParent(systems, false);
            var probe = probeObject.AddComponent<DialogueViewProbe>();
            SetReference(probe, "cameraRig", playerCamera.GetComponent<PlayerCameraRig>());
            SetReference(probe, "playerMover", player.GetComponent<PlayerMover>());
            SetReference(probe, "lookTarget", dialogueLookTarget);

            RequireReferences(player.GetComponent<PlayerMover>());
            RequireReferences(playerCamera.GetComponent<PlayerCameraRig>());
            RequireReferences(probe);

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {ScenePath}");
            }
        }

        /// <summary>이동·충돌·경사로·계단·카메라 벽 당김을 확인할 수 있는 바닥과 장애물.</summary>
        private static void BuildEnvironment(Transform environment)
        {
            Material ground = GetOrCreateMaterial("SandboxGround", new Color(0.55f, 0.62f, 0.5f));
            Material obstacle = GetOrCreateMaterial("SandboxObstacle", new Color(0.8f, 0.68f, 0.5f));

            CreateShape(
                PrimitiveType.Plane,
                "Ground",
                environment,
                Vector3.zero,
                new Vector3(6f, 1f, 6f),
                ground
            );
            CreateShape(
                PrimitiveType.Cube,
                "Box",
                environment,
                new Vector3(4f, 1f, 3f),
                new Vector3(2f, 2f, 2f),
                obstacle
            );
            CreateShape(
                PrimitiveType.Cube,
                "Wall",
                environment,
                new Vector3(0f, 1.5f, -7f),
                new Vector3(10f, 3f, 0.5f),
                obstacle
            );

            Transform ramp = CreateShape(
                PrimitiveType.Cube,
                "Ramp",
                environment,
                new Vector3(8f, 1f, 2f),
                new Vector3(3f, 0.3f, 8f),
                obstacle
            );
            ramp.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            CreateShape(
                PrimitiveType.Cube,
                "RampTop",
                environment,
                new Vector3(8f, 1.03f, 7.4f),
                new Vector3(3f, 2.05f, 3f),
                obstacle
            );

            // CharacterController 기본 계단 높이(0.3m)보다 낮게 0.25m씩 올린다.
            for (int step = 1; step <= 4; step++)
            {
                float height = step * 0.25f;
                CreateShape(
                    PrimitiveType.Cube,
                    $"Step{step}",
                    environment,
                    new Vector3(-6f, height / 2f, step * 0.8f),
                    new Vector3(2f, height, 0.8f),
                    obstacle
                );
            }
        }

        /// <summary>NPC 대신 대화 시점을 확인할 대상. 카메라는 머리 높이의 LookTarget을 바라본다.</summary>
        private static Transform CreateDialogueTarget(Transform characters)
        {
            var target = new GameObject("DialogueTarget");
            target.transform.SetParent(characters, false);
            target.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 6f),
                Quaternion.Euler(0f, 180f, 0f)
            );
            CreateShape(
                PrimitiveType.Capsule,
                "Body",
                target.transform,
                new Vector3(0f, BodyHeightMeters / 2f, 0f),
                new Vector3(BodyRadiusMeters * 2f, BodyHeightMeters / 2f, BodyRadiusMeters * 2f),
                GetOrCreateMaterial("SandboxDialogueTarget", new Color(0.9f, 0.55f, 0.25f))
            );

            var lookTarget = new GameObject("LookTarget");
            lookTarget.transform.SetParent(target.transform, false);
            lookTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            return lookTarget.transform;
        }

        private static Transform CreateGroup(string groupName)
        {
            return new GameObject(groupName).transform;
        }

        private static Transform CreateShape(
            PrimitiveType type,
            string shapeName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider = true
        )
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = shapeName;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localScale = localScale;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!keepCollider)
            {
                Object.DestroyImmediate(shape.GetComponent<Collider>());
            }

            return shape.transform;
        }

        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"{ArtFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                {
                    throw new InvalidOperationException("URP Lit 셰이더를 찾지 못했습니다.");
                }

                EnsureFolder(ArtFolder);
                material = new Material(litShader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T LoadOrCreateAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static InputActionReference ActionReference(string actionName)
        {
            InputActionReference reference = AssetDatabase
                .LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(candidate =>
                    candidate.action != null
                    && candidate.action.actionMap.name == PlayerActionMap
                    && candidate.action.name == actionName
                );

            if (reference == null)
            {
                throw new InvalidOperationException(
                    $"입력 액션 {PlayerActionMap}/{actionName}의 참조를 찾지 못했습니다."
                );
            }

            return reference;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool isSaved);
            Object.DestroyImmediate(root);

            if (!isSaved)
            {
                throw new InvalidOperationException($"프리팹을 저장하지 못했습니다: {path}");
            }

            return prefab;
        }

        private static void SetReference(Object owner, string propertyName, Object value)
        {
            var fields = new SerializedObject(owner);
            fields.FindProperty(propertyName).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>참조 필드가 하나라도 비어 있으면 실패시켜, 연결이 빠진 씬을 만들지 않게 한다.</summary>
        private static void RequireReferences(Object owner)
        {
            SerializedProperty property = new SerializedObject(owner).GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool isEmptyReference =
                    property.propertyType == SerializedPropertyType.ObjectReference
                    && property.name != "m_Script"
                    && property.objectReferenceValue == null;
                if (isEmptyReference)
                {
                    throw new InvalidOperationException(
                        $"{owner.GetType().Name}의 {property.name}가 연결되지 않았습니다."
                    );
                }
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string normalizedPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(normalizedPath).Replace('\\', '/');
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(normalizedPath));
        }
    }
}
