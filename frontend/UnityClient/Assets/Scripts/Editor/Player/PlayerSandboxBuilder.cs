using System;
using System.IO;
using System.Linq;
using A305.Characters;
using A305.EditorTools.Common;
using A305.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    /// <summary>
    /// 플레이어 이동·카메라를 확인하는 PlayerSandbox 씬과 Player·PlayerCamera 프리팹, 데이터 에셋,
    /// 카메라 입력 액션을 만든다. Sandbox 씬은 덮어쓴다. 프리팹은 기능 구성만 맞추고 Visual 아래 모델을 유지하며,
    /// 이미 있는 머티리얼과 데이터 에셋의 값도 유지한다.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        public const string CameraPrefabPath = "Assets/Prefabs/Player/PlayerCamera.prefab";
        public const string MenuPath = "Tools/A305/Player/Build Sandbox";

        private const string ScenePath = "Assets/Scenes/Sandbox/PlayerSandbox.unity";
        private const string MovementSettingsPath = "Assets/Data/Player/PlayerMovementSettings.asset";
        private const string CameraSettingsPath = "Assets/Data/Player/PlayerCameraSettings.asset";
        private const string ArtFolder = "Assets/Art/Player";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerActionMap = "Player";
        private const string KeyboardMouseGroup = "Keyboard&Mouse";

        // CharacterController와 도형 크기를 맞춘 사람 키(m)
        private const float BodyHeightMeters = 1.8f;
        private const float BodyRadiusMeters = 0.3f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            AddCameraInputActions();
            var movementSettings = BuilderTools.LoadOrCreateAsset<PlayerMovementSettings>(
                MovementSettingsPath
            );
            var cameraSettings = BuilderTools.LoadOrCreateAsset<PlayerCameraSettings>(
                CameraSettingsPath
            );

            GameObject playerPrefab = BuildPlayerPrefab(movementSettings);
            GameObject cameraPrefab = BuildCameraPrefab(cameraSettings);
            BuildScene(playerPrefab, cameraPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log($"PlayerSandbox를 만들었습니다: {ScenePath}");
        }

        /// <summary>
        /// 씬에 놓인 플레이어와 카메라를 서로 연결한다. 이동 방향은 카메라를, 카메라는 플레이어를 기준으로 한다.
        /// </summary>
        public static void ConnectPlayerAndCamera(GameObject player, GameObject playerCamera)
        {
            BuilderTools.SetReference(
                player.GetComponent<PlayerMover>(),
                "cameraTransform",
                playerCamera.transform
            );
            BuilderTools.SetReference(
                playerCamera.GetComponent<PlayerCameraRig>(),
                "followTarget",
                player.transform
            );
            BuilderTools.RequireReferences(player.GetComponent<PlayerMover>());
            BuilderTools.RequireReferences(playerCamera.GetComponent<PlayerCameraRig>());
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

        /// <summary>
        /// Player 프리팹의 기능 구성(컴포넌트와 연결)만 맞춘다. 모델은 Visual 아래에 두며,
        /// Visual이 없을 때만 도형으로 된 대체 모양을 만든다. 모델에 맞춰 바꾼 CharacterController 크기도 유지한다.
        /// </summary>
        private static GameObject BuildPlayerPrefab(PlayerMovementSettings movementSettings)
        {
            return BuilderTools.UpdateOrCreatePrefab(
                PlayerPrefabPath,
                "Player",
                root => SetUpPlayer(root, movementSettings)
            );
        }

        private static void SetUpPlayer(GameObject root, PlayerMovementSettings movementSettings)
        {
            if (!root.TryGetComponent(out CharacterController characterController))
            {
                characterController = root.AddComponent<CharacterController>();
                characterController.height = BodyHeightMeters;
                characterController.radius = BodyRadiusMeters;
                characterController.center = new Vector3(0f, BodyHeightMeters / 2f, 0f);
            }

            var animationDriver = BuilderTools.GetOrAddComponent<CharacterAnimationDriver>(root);
            var mover = BuilderTools.GetOrAddComponent<PlayerMover>(root);

            // 모델로 바꿀 때는 Visual 아래만 교체한다. 콜라이더는 루트의 CharacterController가 맡는다.
            if (root.transform.Find("Visual") == null)
            {
                CreatePlaceholderVisual(root.transform);
            }

            BuilderTools.SetReference(mover, "movementSettings", movementSettings);
            BuilderTools.SetReference(mover, "moveAction", ActionReference("Move"));
            BuilderTools.SetReference(mover, "sprintAction", ActionReference("Sprint"));
            BuilderTools.SetReference(mover, "jumpAction", ActionReference("Jump"));
            BuilderTools.SetReference(mover, "animationDriver", animationDriver);
        }

        private static void CreatePlaceholderVisual(Transform root)
        {
            Transform visual = BuilderTools.CreateEmpty(
                "Visual",
                root,
                Vector3.zero,
                Quaternion.identity
            );
            BuilderTools.CreateShape(
                PrimitiveType.Capsule,
                "Body",
                visual,
                new Vector3(0f, BodyHeightMeters / 2f, 0f),
                new Vector3(BodyRadiusMeters * 2f, BodyHeightMeters / 2f, BodyRadiusMeters * 2f),
                ArtMaterial("PlayerBody", new Color(0.25f, 0.45f, 0.85f)),
                keepCollider: false
            );
            // 몸이 어느 쪽을 보는지 알 수 있게 앞쪽에 표시를 단다.
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "FrontMarker",
                visual,
                new Vector3(0f, 1.5f, BodyRadiusMeters),
                new Vector3(0.2f, 0.1f, 0.15f),
                ArtMaterial("PlayerFront", new Color(0.95f, 0.95f, 0.95f)),
                keepCollider: false
            );
        }

        /// <summary>PlayerCamera 프리팹의 기능 구성만 맞춘다. 카메라의 화면 설정은 유지한다.</summary>
        private static GameObject BuildCameraPrefab(PlayerCameraSettings cameraSettings)
        {
            return BuilderTools.UpdateOrCreatePrefab(
                CameraPrefabPath,
                "PlayerCamera",
                root => SetUpCamera(root, cameraSettings)
            );
        }

        private static void SetUpCamera(GameObject root, PlayerCameraSettings cameraSettings)
        {
            root.tag = "MainCamera";
            BuilderTools.GetOrAddComponent<Camera>(root);
            BuilderTools.GetOrAddComponent<AudioListener>(root);
            var rig = BuilderTools.GetOrAddComponent<PlayerCameraRig>(root);

            BuilderTools.SetReference(rig, "cameraSettings", cameraSettings);
            BuilderTools.SetReference(rig, "lookAction", ActionReference("Look"));
            BuilderTools.SetReference(rig, "rotateHoldAction", ActionReference("CameraRotate"));
            BuilderTools.SetReference(rig, "zoomAction", ActionReference("CameraZoom"));
            BuilderTools.SetReference(rig, "toggleViewAction", ActionReference("ToggleView"));
        }

        private static void BuildScene(GameObject playerPrefab, GameObject cameraPrefab)
        {
            SceneGroups groups = BuilderTools.NewSceneWithGroups();

            BuildEnvironment(groups.Environment);

            GameObject player = BuilderTools.InstantiatePrefab(
                playerPrefab,
                groups.Characters,
                Vector3.zero,
                Quaternion.identity
            );
            GameObject playerCamera = BuilderTools.InstantiatePrefab(
                cameraPrefab,
                groups.Systems,
                new Vector3(0f, 2.5f, -5f),
                Quaternion.Euler(20f, 0f, 0f)
            );
            ConnectPlayerAndCamera(player, playerCamera);

            Transform dialogueLookTarget = CreateDialogueTarget(groups.Characters);

            var probe = new GameObject("DialogueViewProbe").AddComponent<DialogueViewProbe>();
            probe.transform.SetParent(groups.Systems, false);
            BuilderTools.SetReference(probe, "cameraRig", playerCamera.GetComponent<PlayerCameraRig>());
            BuilderTools.SetReference(probe, "playerMover", player.GetComponent<PlayerMover>());
            BuilderTools.SetReference(probe, "lookTarget", dialogueLookTarget);
            BuilderTools.RequireReferences(probe);

            BuilderTools.SaveScene(groups.Scene, ScenePath);
        }

        /// <summary>이동·충돌·경사로·계단·카메라 벽 당김을 확인할 수 있는 바닥과 장애물.</summary>
        private static void BuildEnvironment(Transform environment)
        {
            Material ground = ArtMaterial("SandboxGround", new Color(0.55f, 0.62f, 0.5f));
            Material obstacle = ArtMaterial("SandboxObstacle", new Color(0.8f, 0.68f, 0.5f));

            BuilderTools.CreateShape(
                PrimitiveType.Plane,
                "Ground",
                environment,
                Vector3.zero,
                new Vector3(6f, 1f, 6f),
                ground
            );
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Box",
                environment,
                new Vector3(4f, 1f, 3f),
                new Vector3(2f, 2f, 2f),
                obstacle
            );
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Wall",
                environment,
                new Vector3(0f, 1.5f, -7f),
                new Vector3(10f, 3f, 0.5f),
                obstacle
            );

            Transform ramp = BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "Ramp",
                environment,
                new Vector3(8f, 1f, 2f),
                new Vector3(3f, 0.3f, 8f),
                obstacle
            );
            ramp.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            BuilderTools.CreateShape(
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
                BuilderTools.CreateShape(
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
            Transform target = BuilderTools.CreateEmpty(
                "DialogueTarget",
                characters,
                new Vector3(0f, 0f, 6f),
                Quaternion.Euler(0f, 180f, 0f)
            );
            BuilderTools.CreateShape(
                PrimitiveType.Capsule,
                "Body",
                target,
                new Vector3(0f, BodyHeightMeters / 2f, 0f),
                new Vector3(BodyRadiusMeters * 2f, BodyHeightMeters / 2f, BodyRadiusMeters * 2f),
                ArtMaterial("SandboxDialogueTarget", new Color(0.9f, 0.55f, 0.25f))
            );

            return BuilderTools.CreateEmpty(
                "LookTarget",
                target,
                new Vector3(0f, 1.6f, 0f),
                Quaternion.identity
            );
        }

        private static Material ArtMaterial(string materialName, Color color)
        {
            return BuilderTools.GetOrCreateMaterial(ArtFolder, materialName, color);
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
    }
}
