using System;
using System.IO;
using A305.EditorTools.Common;
using A305.EditorTools.Player;
using A305.Player;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static A305.EditorTools.World.TownBlockoutGeometry;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>Production과 공용 플레이어 설정을 건드리지 않는 메인스트리트 크기 시험장.</summary>
    public static class TownMainstreetBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/TownMainstreetSandbox.unity";
        public const string SettingsPath =
            "Assets/Data/World/MainstreetBlockout/TownBlockoutSettings.asset";
        public const string CameraSettingsPath =
            "Assets/Data/World/MainstreetBlockout/CameraSettings.asset";
        public const string MovementSettingsPath =
            "Assets/Data/World/MainstreetBlockout/MovementSettings.asset";

        [MenuItem("Tools/A305/World/Build And Verify Character 160cm")]
        public static void BuildCharacter160AndVerify() => BuildCharacterHeightAndVerify(1.6f);

        [MenuItem("Tools/A305/World/Build And Verify Character 170cm")]
        public static void BuildCharacter170AndVerify() => BuildCharacterHeightAndVerify(1.7f);

        private static void BuildCharacterHeightAndVerify(float height)
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }
            var settings = BuilderTools.LoadOrCreateAsset<TownBlockoutSettings>(SettingsPath);
            var values = new SerializedObject(settings);
            values.FindProperty("characterHeightMeters").floatValue = height;
            values.ApplyModifiedPropertiesWithoutUndo();

            // Match the eye and orbit pivot to the uniformly scaled character.
            var cameraValues = new SerializedObject(PrepareCameraSettings());
            cameraValues.FindProperty("pivotHeightMeters").floatValue = 0.9f * height / 1.4f;
            cameraValues.FindProperty("eyeHeightMeters").floatValue = 0.94f * height / 1.4f;
            cameraValues.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            TownMainstreetVerification.BuildAndVerify();
        }

        [MenuItem("Tools/A305/World/Build And Verify Larger Mainstreet Sandbox")]
        public static void BuildLargerAndVerify()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }
            var settings = BuilderTools.LoadOrCreateAsset<TownBlockoutSettings>(SettingsPath);
            var values = new SerializedObject(settings);
            values.FindProperty("groundFloorHeightMeters").floatValue = 4f;
            values.FindProperty("upperFloorHeightMeters").floatValue = 3f;
            values.FindProperty("shopFootprintMeters").floatValue = 8f;
            values.FindProperty("hotelFootprintMeters").floatValue = 10f;
            values.FindProperty("homeFootprintMeters").floatValue = 8f;
            values.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            TownMainstreetVerification.BuildAndVerify();
        }

        [MenuItem("Tools/A305/World/Build Mainstreet Sandbox")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            // Replace the scene before loading assets that its unload can release.
            SceneGroups groups = BuilderTools.NewSceneWithGroups();
            var settings = BuilderTools.LoadOrCreateAsset<TownBlockoutSettings>(SettingsPath);
            if (settings.DoorwayHeightMeters + 0.1f >= settings.GroundFloorHeightMeters)
            {
                throw new InvalidOperationException(
                    "문 높이 위에 프레임과 벽이 들어갈 층고를 확보합니다."
                );
            }

            PlayerCameraSettings cameraSettings = PrepareCameraSettings();
            PlayerMovementSettings movement = CopySettingsIfMissing<PlayerMovementSettings>(
                "Assets/Data/Player/PlayerMovementSettings.asset",
                MovementSettingsPath
            );
            GameObject playerPrefab = PreparePlayer(movement);
            BuildStreet(groups.Environment, settings);
            BuildBuildings(groups.Environment, settings);
            BuildMarkers(groups.Environment, settings);
            BuildLighting(groups.Lighting);

            GameObject player = BuilderTools.InstantiatePrefab(
                playerPrefab,
                groups.Characters,
                new Vector3(6.5f, 0.1f, -11f - settings.HomeFootprintMeters),
                Quaternion.Euler(0f, -15f, 0f)
            );
            player.name = "BlockoutPlayer";
            float scale = settings.CharacterHeightMeters / 1.4f;
            player.transform.localScale = Vector3.one * scale;

            GameObject cameraPrefab = BuilderTools.LoadRequiredAsset<GameObject>(
                PlayerSandboxBuilder.CameraPrefabPath,
                PlayerSandboxBuilder.MenuPath
            );
            GameObject cameraObject = BuilderTools.InstantiatePrefab(
                cameraPrefab,
                groups.Systems,
                Vector3.zero,
                Quaternion.identity
            );
            cameraObject.name = "BlockoutCamera";
            BuilderTools.SetReference(
                cameraObject.GetComponent<PlayerCameraRig>(),
                "cameraSettings",
                cameraSettings
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, cameraObject);
            var visibility = player.AddComponent<BlockoutPlayerVisibility>();
            BuilderTools.SetReference(
                visibility,
                "cameraRig",
                cameraObject.GetComponent<PlayerCameraRig>()
            );
            BuilderTools.SetReference(visibility, "visualRoot", player.transform.Find("Visual"));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 180f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.65f, 0.81f, 0.9f);
            CameraOrbit orbit = cameraSettings.CreateOrbit();
            orbit.SetYaw(player.transform.eulerAngles.y);
            Pose pose = orbit.GetThirdPersonPose(
                player.transform.position + Vector3.up * cameraSettings.PivotHeightMeters
            );
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);

            GameObject visual = BuilderTools.LoadRequiredAsset<GameObject>(
                $"{PrefabFolder}/LittleMungV7Visual.prefab",
                "Build Mainstreet Sandbox"
            );
            GameObject npc = BuilderTools.InstantiatePrefab(
                visual,
                groups.Characters,
                new Vector3(10.5f, 0.1f, -10f),
                Quaternion.Euler(0f, 270f, 0f)
            );
            npc.name = "DialogueScaleReference";
            npc.transform.localScale = Vector3.one * scale;
            Transform faceTarget = Group(
                "DialogueLookTarget",
                npc.transform,
                new Vector3(0f, 0.94f, 0.43f)
            );
            var probe = player.AddComponent<DialogueViewProbe>();
            BuilderTools.SetReference(
                probe,
                "cameraRig",
                cameraObject.GetComponent<PlayerCameraRig>()
            );
            BuilderTools.SetReference(probe, "playerMover", player.GetComponent<PlayerMover>());
            BuilderTools.SetReference(probe, "lookTarget", faceTarget);
            BuildHud(groups.UI);

            BuilderTools.SaveScene(groups.Scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Mainstreet Sandbox ready: {ScenePath}. North extension: {settings.NorthExtensionMeters}m. Production Scene List unchanged."
            );
        }

        private static PlayerCameraSettings PrepareCameraSettings()
        {
            bool isNew =
                AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(CameraSettingsPath) == null;
            var settings = BuilderTools.LoadOrCreateAsset<PlayerCameraSettings>(CameraSettingsPath);
            if (isNew)
            {
                var values = new SerializedObject(settings);
                values.FindProperty("pivotHeightMeters").floatValue = 0.9f;
                values.FindProperty("eyeHeightMeters").floatValue = 0.94f;
                values.FindProperty("startDistanceMeters").floatValue = 4f;
                values.FindProperty("startPitchDegrees").floatValue = 25f;
                values.ApplyModifiedPropertiesWithoutUndo();
            }
            return settings;
        }

        private static T CopySettingsIfMissing<T>(string source, string destination)
            where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(destination);
            if (existing != null)
            {
                return existing;
            }
            T copy = Object.Instantiate(
                BuilderTools.LoadRequiredAsset<T>(source, PlayerSandboxBuilder.MenuPath)
            );
            BuilderTools.EnsureFolder(Path.GetDirectoryName(destination));
            AssetDatabase.CreateAsset(copy, destination);
            return copy;
        }

        private static GameObject PreparePlayer(PlayerMovementSettings movement)
        {
            GameObject visual = BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/LittleMungV7Visual.prefab",
                CharacterVisual
            );
            return BuilderTools.CreatePrefabIfMissing(
                $"{PrefabFolder}/LittleMungV7Player.prefab",
                () =>
                {
                    var source = BuilderTools.LoadRequiredAsset<GameObject>(
                        PlayerSandboxBuilder.PlayerPrefabPath,
                        PlayerSandboxBuilder.MenuPath
                    );
                    GameObject copy = Object.Instantiate(source);
                    copy.name = "LittleMungV7Player";
                    Transform previousVisual = copy.transform.Find("Visual");
                    if (previousVisual != null)
                    {
                        Object.DestroyImmediate(previousVisual.gameObject);
                    }
                    GameObject body = BuilderTools.InstantiatePrefab(
                        visual,
                        copy.transform,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    body.name = "Visual";
                    CharacterController controller = copy.GetComponent<CharacterController>();
                    controller.height = 1.4f;
                    controller.radius = 0.32f;
                    controller.center = Vector3.up * 0.7f;
                    controller.stepOffset = 0.15f;
                    controller.skinWidth = 0.02f;
                    BuilderTools.SetReference(
                        copy.GetComponent<PlayerMover>(),
                        "movementSettings",
                        movement
                    );
                    return copy;
                }
            );
        }

        private static void BuildStreet(Transform environment, TownBlockoutSettings settings)
        {
            Transform streets = Group("StreetModules", environment, Vector3.zero);
            Material grass = Material("Grass", "#A6BD87");
            Material road = Material("Road", "#697791");
            Material pavement = Material("Pavement", "#E8CFA8");
            Material extension = Material("ExtensionPavement", "#D5D9CE");
            float end = 30f + settings.NorthExtensionMeters;
            float cafeDoorZ = 5f - 2f * settings.ShopFootprintMeters;
            Box(
                "SiteGround",
                streets,
                new Vector3(0f, -0.2f, (end - 24f) / 2f),
                new Vector3(40f, 0.2f, end + 24f),
                grass
            );
            // Two-metre modules let the road grow without scaling an entire finished block.
            for (float z = -24f; z < end; z += 2f)
            {
                float length = Mathf.Min(2f, end - z);
                Box(
                    $"Road_{z:0}",
                    streets,
                    new Vector3(0f, -0.05f, z + length / 2f),
                    new Vector3(6f, 0.1f, length),
                    road
                );
                if (z < 6f || z >= 14f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box(
                            $"Sidewalk_{side}_{z:0}",
                            streets,
                            new Vector3(side * 5f, 0.025f, z + length / 2f),
                            new Vector3(4f, 0.15f, length),
                            z >= 30f ? extension : pavement
                        );
                    }
                }
                if (
                    (z < 6f || z >= 14f)
                    && (z + length <= cafeDoorZ - 1.5f || z >= cafeDoorZ + 1.5f)
                )
                {
                    Box(
                        "CentreLine",
                        streets,
                        new Vector3(-0.12f, 0.006f, z + length / 2f),
                        new Vector3(0.08f, 0.008f, length),
                        Material("Yellow", "#EBD17A"),
                        false
                    );
                    Box(
                        "CentreLine",
                        streets,
                        new Vector3(0.12f, 0.006f, z + length / 2f),
                        new Vector3(0.08f, 0.008f, length),
                        Material("Yellow", "#EBD17A"),
                        false
                    );
                }
            }
            // Fill the final metre at the exact Z=7/13 intersection edges.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(
                    "SidewalkJunctionSouth",
                    streets,
                    new Vector3(side * 5f, 0.025f, 6.5f),
                    new Vector3(4f, 0.15f, 1f),
                    pavement
                );
                Box(
                    "SidewalkJunctionNorth",
                    streets,
                    new Vector3(side * 5f, 0.025f, 13.5f),
                    new Vector3(4f, 0.15f, 1f),
                    pavement
                );
                Box(
                    "CrossRoadWing",
                    streets,
                    new Vector3(side * 11.5f, -0.05f, 10f),
                    new Vector3(17f, 0.1f, 6f),
                    road
                );
                Box(
                    "CrossSidewalkSouth",
                    streets,
                    new Vector3(side * 13.5f, 0.025f, 5f),
                    new Vector3(13f, 0.15f, 4f),
                    pavement
                );
                Box(
                    "CrossSidewalkNorth",
                    streets,
                    new Vector3(side * 13.5f, 0.025f, 15f),
                    new Vector3(13f, 0.15f, 4f),
                    pavement
                );
            }
            for (float x = -2.6f; x < 3f; x += 1.1f)
            {
                Box(
                    "CrosswalkStripe",
                    streets,
                    new Vector3(x + 0.325f, 0.008f, cafeDoorZ),
                    new Vector3(0.65f, 0.01f, 3f),
                    Material("Stripe", "#FFF8E8"),
                    false
                );
            }
            Box(
                "MeetingForecourt",
                streets,
                new Vector3(10f, 0.025f, -10f),
                new Vector3(6f, 0.15f, 6f),
                pavement
            );
            Box(
                "HousePorch",
                streets,
                new Vector3(8f, 0.025f, -13f - settings.HomeFootprintMeters / 2f),
                new Vector3(2f, 0.15f, settings.HomeFootprintMeters),
                pavement
            );
            Transform boundaries = Group("MovableSandboxBoundary", environment, Vector3.zero);
            Box(
                "NorthEnd",
                boundaries,
                new Vector3(0f, 0.5f, end),
                new Vector3(40f, 1f, 0.2f),
                extension
            );
            Box(
                "SouthEnd",
                boundaries,
                new Vector3(0f, 0.5f, -24f),
                new Vector3(40f, 1f, 0.2f),
                extension
            );
            Box(
                "WestEnd",
                boundaries,
                new Vector3(-20f, 0.5f, (end - 24f) / 2f),
                new Vector3(0.2f, 1f, end + 24f),
                extension
            );
            Box(
                "EastEnd",
                boundaries,
                new Vector3(20f, 0.5f, (end - 24f) / 2f),
                new Vector3(0.2f, 1f, end + 24f),
                extension
            );
        }

        private static void BuildBuildings(Transform environment, TownBlockoutSettings settings)
        {
            Transform buildings = Group("BuildingFootprints", environment, Vector3.zero);
            float homeSize = settings.HomeFootprintMeters;
            float shopSize = settings.ShopFootprintMeters;
            float hotelSize = settings.HotelFootprintMeters;
            Building(
                "Home",
                buildings,
                new Vector3(9f, 0.1f, -13f - homeSize / 2f),
                homeSize,
                homeSize,
                270f,
                settings,
                house: true
            );
            Building(
                "Cafe",
                buildings,
                new Vector3(-7f, 0.1f, 3f - 1.5f * shopSize),
                shopSize,
                shopSize,
                90f,
                settings
            );
            Building(
                "GeneralStore",
                buildings,
                new Vector3(-7f, 0.1f, 3f - shopSize / 2f),
                shopSize,
                shopSize,
                90f,
                settings,
                enterable: false
            );
            Building(
                "Hotel",
                buildings,
                new Vector3(7f, 0.1f, 3f - hotelSize / 2f),
                hotelSize,
                hotelSize,
                270f,
                settings
            );
            Material background = Material("Background", "#B1BAB0");
            for (int side = -1; side <= 1; side += 2)
            {
                Box(
                    "BackgroundShell",
                    buildings,
                    new Vector3(side * 11f, 2.1f, 21f),
                    new Vector3(8f, 4f, 8f),
                    background
                );
            }
            Transform future = Group("FutureBuildingSlots", environment, Vector3.zero);
            for (
                float z = 35f;
                z + shopSize < 30f + settings.NorthExtensionMeters;
                z += shopSize + 4f
            )
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(
                        "FutureBuildingLot",
                        future,
                        new Vector3(side * (7f + shopSize / 2f), -0.02f, z + shopSize / 2f),
                        new Vector3(shopSize, 0.04f, shopSize),
                        background,
                        false
                    );
                    Label(
                        "FutureLotLabel",
                        $"{shopSize:0.#} × {shopSize:0.#}m\n확장 부지",
                        future,
                        new Vector3(side * (7f + shopSize / 2f), 0.05f, z + shopSize / 2f),
                        new Vector3(90f, 0f, 0f),
                        0.55f
                    );
                }
            }
        }

        private static void BuildMarkers(Transform environment, TownBlockoutSettings settings)
        {
            Transform markers = Group("ScaleAndDistanceMarkers", environment, Vector3.zero);
            Material mark = Material("Marker", "#36516B");
            Box(
                "HeightGauge",
                markers,
                new Vector3(3.8f, 0.1f + settings.GroundFloorHeightMeters / 2f, -21.5f),
                new Vector3(0.06f, settings.GroundFloorHeightMeters, 0.06f),
                mark,
                false
            );
            foreach (
                float height in new[]
                {
                    settings.CharacterHeightMeters * 0.4f,
                    settings.CharacterHeightMeters,
                    settings.DoorwayHeightMeters,
                    settings.GroundFloorHeightMeters,
                }
            )
            {
                Box(
                    "HeightTick",
                    markers,
                    new Vector3(3.8f, 0.1f + height, -21.5f),
                    new Vector3(0.5f, 0.03f, 0.06f),
                    mark,
                    false
                );
                Label(
                    "HeightLabel",
                    $"{height:0.##}m",
                    markers,
                    new Vector3(4.5f, 0.1f + height, -21.6f),
                    Vector3.zero,
                    0.23f
                );
            }
            for (float z = 30f; z <= 30f + settings.NorthExtensionMeters; z += 10f)
            {
                Box(
                    "DistanceTick",
                    markers,
                    new Vector3(5f, 0.115f, z),
                    new Vector3(4f, 0.02f, 0.12f),
                    mark,
                    false
                );
                Label(
                    "DistanceLabel",
                    $"+{z - 30f:0}m",
                    markers,
                    new Vector3(5f, 0.14f, z - 0.7f),
                    new Vector3(90f, 0f, 0f),
                    0.6f
                );
            }
            Label(
                "NorthConnector",
                "북쪽 확장 / 6m 도로 + 4m 보도 × 2",
                markers,
                new Vector3(0f, 3f, 29f),
                Vector3.zero,
                0.45f
            );
            Label(
                "WestConnector",
                "서쪽 연결",
                markers,
                new Vector3(-18f, 2f, 10f),
                new Vector3(0f, 270f, 0f),
                0.4f
            );
            Label(
                "EastConnector",
                "동쪽 연결",
                markers,
                new Vector3(18f, 2f, 10f),
                new Vector3(0f, 90f, 0f),
                0.4f
            );
            Group("MeetingPlayerPoint", markers, new Vector3(8.5f, 0.1f, -10f));
            Group("NorthExtensionStart", markers, new Vector3(5f, 0.1f, 30f));
            Group(
                "NorthExtensionFinish",
                markers,
                new Vector3(5f, 0.1f, 28f + settings.NorthExtensionMeters)
            );
        }

        private static void BuildLighting(Transform parent)
        {
            Light sun = parent.GetComponentInChildren<Light>();
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.75f);
            RenderSettings.fog = false;
        }

        private static void BuildHud(Transform parent)
        {
            var root = new GameObject(
                "BlockoutInstructions",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)
            );
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var label = new GameObject(
                "Instructions",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            label.transform.SetParent(root.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(700f, 150f);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/UI/Fonts/Pretendard-Regular SDF.asset"
            );
            text.fontSize = 23f;
            text.color = new Color(0.1f, 0.15f, 0.2f);
            text.raycastTarget = false;
            text.text =
                "메인스트리트 블록아웃 · 리틀 멍 v7 비율 대체물\nWASD 이동 · Shift 달리기 · Space 점프\n마우스 왼쪽 드래그 회전 · 휠 줌 · V 시점 · T 대화 시야\n북쪽 회색 보도: 확장 거리 시험 / 문 안쪽: 통행 확인용 빈 공간";
        }
    }
}
