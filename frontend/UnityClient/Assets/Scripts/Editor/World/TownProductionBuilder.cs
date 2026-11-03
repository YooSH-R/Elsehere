using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.Player;
using A305.Npc;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>승인된 아트를 프리팹으로 승격하고 기존 TownScene의 기능 연결을 보존한다.</summary>
    public static class TownProductionBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Production/TownScene.unity";
        internal const string DataFolder = "Assets/Data/World/TownProduction";
        private const string ArtRoot = "TownMainstreet";

        [MenuItem("Tools/A305/World/Repair Tutorial Passerby Placement")]
        public static void RepairTutorialPasserbyPlacement()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Transform passerby = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "TOWN_PASSERBY")
                .transform;
            PlaceTutorialPasserby(scene, passerby);
            BuilderTools.SaveScene(scene, ScenePath);
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        }

        private static void PlaceTutorialPasserby(Scene scene, Transform passerby)
        {
            SpaceDoorway homeDoor = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(door => door.DoorwayId == "HomeDoor");
            // 집 문 앞 2m에서 보도 쪽으로 2m, 길을 따라 5m: 감지 반경 8m 안의 열린 보도.
            // 도착 지점을 문 앞 1m로 줄이기 전과 같은 자리에 서도록 문에서 잰다.
            Vector3 front = homeDoor.transform.TransformDirection(new Vector3(0f, 0f, 2f));
            Vector3 offset = homeDoor.transform.TransformDirection(new Vector3(5f, 0f, 2f));
            passerby.SetPositionAndRotation(
                homeDoor.transform.position + front + offset + Vector3.up * .02f,
                Quaternion.LookRotation(-offset)
            );
            PrefabUtility.RecordPrefabInstancePropertyModifications(passerby);
            Debug.Log($"Tutorial passerby placed {offset.magnitude:F2}m from HomeDoor front.");
        }

        // 건물에서 나오면 문을 등지고 문 앞 인도 1m에 선다. 감지 영역은 문에서 0.6m 깊이까지 둔다.
        // 선 자리의 몸(반지름 0.33m)은 0.67m까지 와서 닿지 않고, 문 쪽으로 걸으면 큰 머리(반지름 0.68m)가 문짝에 먼저 닿아
        // 몸이 0.475m에서 멈추므로 그보다 넉넉히 깊어야 들어간다.
        private static readonly Vector3 DoorArrival = new Vector3(0, 0, 1f);
        private static readonly Vector3 DoorTriggerCenter = new Vector3(0, 1.3f, .3f);
        private static readonly Vector3 DoorTriggerSize = new Vector3(1.8f, 2.6f, .6f);

        [MenuItem("Tools/A305/World/Apply Town Door Arrival")]
        public static void ApplyDoorArrival()
        {
            if (!BuilderTools.CanBuild())
                return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (SpaceDoorway door in BuilderTools.FindAllInScene<SpaceDoorway>(scene))
                FitDoorToSidewalk(door);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        /// <summary>마을 출입문의 감지 영역과 나올 때 설 자리를 문 앞 인도에 맞춘다.</summary>
        internal static void FitDoorToSidewalk(SpaceDoorway door)
        {
            BoxCollider trigger = door.GetComponent<BoxCollider>();
            trigger.center = DoorTriggerCenter;
            trigger.size = DoorTriggerSize;
            door.ArrivalPoint.localPosition = DoorArrival;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            PrefabUtility.RecordPrefabInstancePropertyModifications(door.ArrivalPoint);
        }

        /// <summary>
        /// 캐릭터보다 키가 큰 사람이 바로 뒤에 서서 내려다보는 높이로 3인칭 카메라를 맞춘다.
        /// 몸 가운데를 4.2m 뒤에서 16도 내려다보면 카메라는 발 기준 약 2.1~2.5m 높이에 온다.
        /// </summary>
        [MenuItem("Tools/A305/World/Apply Town Follow Camera")]
        public static void ApplyFollowCamera()
        {
            BuilderTools.ThrowIfPlaying();
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                DataFolder + "/CameraSettings.asset"
            );
            if (settings == null)
                throw new InvalidOperationException("Town camera settings missing");
            var fields = new SerializedObject(settings);
            fields.FindProperty("startDistanceMeters").floatValue = 4.2f;
            fields.FindProperty("startPitchDegrees").floatValue = 16f;
            // 캐릭터가 따로 정하지 않았을 때 바라볼 몸 가운데 높이.
            fields.FindProperty("pivotHeightMeters").floatValue = 1.3f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/World/Apply Production Town Design")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            PrepareAssets();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Transform environment = Root(scene, "Environment");
            // This is a one-time greybox migration. Subsequent runs leave placed art untouched.
            if (environment.Find(ArtRoot) != null)
            {
                Debug.Log("TownMainstreet already exists; preserving scene art and placement.");
                return;
            }
            MigrateEnvironment(scene, environment);
            ConfigureCharacters(scene);
            ConfigureLighting(scene);
            BuilderTools.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log(
                "Production TownScene art applied; existing systems, UI, NPC identities and doorway links preserved."
            );
        }

        private static void PrepareAssets()
        {
            BuilderTools.EnsureFolder(TownBackgroundKit.Folder);
            BuilderTools.EnsureFolder(DataFolder);
            Scene source = EditorSceneManager.OpenScene(TownMainstreetBuilder.ScenePath);
            Transform environment = Root(source, "Environment");
            Export(environment.Find("StreetModules"), "StreetBase");
            Export(environment.Find("MainstreetArt"), "StreetFurnishing");
            Export(environment.Find("BlenderCafePaving"), "CafePaving");
            Export(
                environment.Find("StreetDepthAndGardens"),
                "StreetGardens",
                copy =>
                {
                    foreach (Transform child in copy.transform.Cast<Transform>().ToArray())
                        if (child.name.StartsWith("DistantShop"))
                            Object.DestroyImmediate(child.gameObject);
                }
            );
            foreach (string name in new[] { "Home", "Hotel" })
                Export(
                    environment.Find("BuildingFootprints/" + name),
                    name + "Exterior",
                    copy =>
                    {
                        foreach (Transform child in copy.transform.Cast<Transform>().ToArray())
                            if (!child.gameObject.activeSelf)
                                Object.DestroyImmediate(child.gameObject);
                    }
                );
            CopyAssetIfMissing(
                TownMainstreetBuilder.CameraSettingsPath,
                DataFolder + "/CameraSettings.asset"
            );
            CopyAssetIfMissing(
                "Assets/Data/World/MainstreetBlockout/ArtVolume.asset",
                DataFolder + "/ArtVolume.asset"
            );
            TownBackgroundKit.Prepare();
            PrepareCharacterVisuals();
            AssetDatabase.SaveAssets();
        }

        internal static void PrepareCharacterVisuals()
        {
            foreach (bool isPlayer in new[] { false, true })
            {
                string name = isPlayer ? "LittleMungV7PlayerVisual" : "LittleMungV7NpcVisual";
                BuilderTools.CreatePrefabIfMissing(
                    TownBackgroundKit.Folder + "/" + name + ".prefab",
                    () =>
                    {
                        var root = new GameObject(name);
                        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                            TownBlockoutGeometry.PrefabFolder + "/LittleMungV7Visual.prefab"
                        );
                        GameObject visual = BuilderTools.InstantiatePrefab(
                            model,
                            root.transform,
                            Vector3.zero,
                            Quaternion.identity
                        );
                        visual.transform.localScale = Vector3.one * (1.6f / 1.4f);
                        if (isPlayer)
                        {
                            var visibility = root.AddComponent<BlockoutPlayerVisibility>();
                            BuilderTools.SetReference(visibility, "visualRoot", root.transform);
                        }
                        return root;
                    }
                );
            }
        }

        private static void Export(Transform source, string name, Action<GameObject> edit = null)
        {
            if (source == null)
                throw new InvalidOperationException("Approved art source missing: " + name);
            BuilderTools.CreatePrefabIfMissing(
                TownBackgroundKit.Folder + "/" + name + ".prefab",
                () =>
                {
                    GameObject copy = Object.Instantiate(source.gameObject);
                    copy.name = name;
                    copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    edit?.Invoke(copy);
                    return copy;
                }
            );
        }

        private static void CopyAssetIfMissing(string source, string target)
        {
            if (
                AssetDatabase.LoadAssetAtPath<Object>(target) == null
                && !AssetDatabase.CopyAsset(source, target)
            )
                throw new InvalidOperationException("Cannot copy data asset: " + source);
        }

        private static void MigrateEnvironment(Scene scene, Transform environment)
        {
            string[] replaced =
            {
                "Assets/Prefabs/Town/TownStreets.prefab",
                "Assets/Prefabs/Town/TownHouse.prefab",
                "Assets/Prefabs/Town/RestaurantBuilding.prefab",
                "Assets/Prefabs/Town/AirportBuilding.prefab",
            };
            foreach (Transform child in environment.Cast<Transform>().ToArray())
            {
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    child.gameObject
                );
                if (replaced.Contains(prefab))
                    Object.DestroyImmediate(child.gameObject);
            }
            Transform art = Group(ArtRoot, environment, Vector3.zero);
            foreach (
                string name in new[]
                {
                    "StreetBase",
                    "StreetFurnishing",
                    "CafePaving",
                    "StreetGardens",
                }
            )
                TownBackgroundKit.Instance(name, art, Vector3.zero);
            Transform ground = art.Find("StreetBase/SiteGround");
            ground.localPosition = new Vector3(0, -.2f, 19);
            ground.localScale = new Vector3(40, .2f, 102);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ground);
            TownBackgroundKit.Instance("NorthStreetDressing", art, Vector3.zero);
            Transform backdrop = Group("BackgroundBuildings", art, Vector3.zero);
            TownBackgroundKit.Instance("VacantBuilding2F", backdrop, new Vector3(-7, .1f, -1), 90);
            TownBackgroundKit.Instance("SageResidence1F", backdrop, new Vector3(-7, .1f, -20), 90);
            for (int index = 0; index < 5; index++)
            {
                float z = 21f + index * 9f;
                foreach (int side in new[] { -1, 1 })
                {
                    if (side > 0 && index == 0)
                        continue;
                    string style = new[] { "Brick", "Butter", "Sage" }[
                        (index + (side > 0 ? 1 : 0)) % 3
                    ];
                    int floors = index == 3 ? 3 : 2;
                    TownBackgroundKit.Instance(
                        style + "Residence" + floors + "F",
                        backdrop,
                        new Vector3(side * 7, .1f, z),
                        side < 0 ? 90 : 270
                    );
                }
            }
            TownBackgroundKit.Instance("PocketPark", art, new Vector3(11, .1f, 21));
            TownBackgroundKit.Instance("ExtensionEnd", art, new Vector3(0, .1f, 69));
            TownBackgroundKit.Instance("ExtensionEnd", art, new Vector3(0, .1f, -32), 180);
            // Cross-street ends stop before the ground edge; moving these whole prefabs opens the next block.
            foreach (int side in new[] { -1, 1 })
            {
                TownBackgroundKit.Instance(
                    "CrossStreetEnd14m",
                    art,
                    new Vector3(side * 19, .1f, 10),
                    side > 0 ? 90 : 270
                );
                var boundary = Group("SideBoundary", art, new Vector3(side * 19.6f, 3, 19))
                    .gameObject.AddComponent<BoxCollider>();
                boundary.size = new Vector3(.5f, 6, 102);
            }
            AdaptEntrance(scene, "HomeDoor", "HomeExterior", new Vector3(9, .1f, -17), 270, -2);
            AdaptEntrance(scene, "CafeDoor", null, new Vector3(-7, .1f, -9), 90, 2);
            AdaptEntrance(scene, "HotelDoor", "HotelExterior", new Vector3(7, .1f, -2), 270, -3);
        }

        private static void AdaptEntrance(
            Scene scene,
            string id,
            string exterior,
            Vector3 position,
            float yaw,
            float localDoorX
        )
        {
            SpaceDoorway door = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(item => item.DoorwayId == id);
            Transform building = door.transform.parent;
            foreach (Transform child in building)
                if (child != door.transform)
                    child.gameObject.SetActive(false);
            building.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            if (exterior != null)
                TownBackgroundKit.Instance(exterior, building, Vector3.zero);
            else
            {
                GameObject cafe = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/World/CafeBlenderKit/Buildings/Cafe2F.prefab"
                );
                if (cafe == null)
                    throw new InvalidOperationException("Cafe2F prefab missing");
                BuilderTools.InstantiatePrefab(cafe, building, Vector3.zero, Quaternion.identity);
            }
            door.transform.localPosition = new Vector3(localDoorX, 0, .05f);
            door.transform.localRotation = Quaternion.identity;
            FitDoorToSidewalk(door);
            Transform visual = door.transform.Find("Visual");
            visual.localScale = new Vector3(1.5f, 1.18f, 1);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = Green;
            PrefabUtility.RecordPrefabInstancePropertyModifications(door.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        private static void ConfigureCharacters(Scene scene)
        {
            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            PlayerCharacterBuilder.ConfigurePlayer(player.gameObject, rig);
            player.transform.SetPositionAndRotation(
                new Vector3(4.8f, .12f, -18.5f),
                Quaternion.Euler(0, 12, 0)
            );
            foreach (NpcIdentity npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
            {
                // 마을 행인 Ben은 비즈니스맨 외형으로 튜토리얼에서 길을 묻는 집 앞에 선다.
                if (npc.Code == "TOWN_PASSERBY")
                {
                    NpcVisualBuilder.Connect(npc);
                    PlaceTutorialPasserby(scene, npc.transform);
                    continue;
                }

                AttachVisual(npc.transform);
                npc.transform.SetPositionAndRotation(
                    new Vector3(12.5f, .12f, 18.5f),
                    Quaternion.Euler(0, 270, 0)
                );
                npc.LookTarget.localPosition = new Vector3(0, 1.075f, .49f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc.LookTarget);
            }
            BuilderTools.SetReference(
                rig,
                "cameraSettings",
                AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(
                    DataFolder + "/CameraSettings.asset"
                )
            );
            Camera camera = rig.GetComponent<Camera>();
            camera.fieldOfView = 55;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 180;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        private static void AttachVisual(Transform character)
        {
            Transform original = character.Find("Visual");
            if (original != null)
                original.gameObject.SetActive(false);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TownBackgroundKit.Folder + "/LittleMungV7NpcVisual.prefab"
            );
            GameObject visual = BuilderTools.InstantiatePrefab(
                prefab,
                character,
                Vector3.zero,
                Quaternion.identity
            );
            visual.name = "TownVisual";
            CharacterController controller = character.GetComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = .36f;
            controller.center = Vector3.up * .8f;
            controller.stepOffset = .15f;
            controller.skinWidth = .02f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        private static void ConfigureLighting(Scene scene)
        {
            Transform lighting = Root(scene, "Lighting");
            Camera camera = BuilderTools
                .FindSingleInScene<PlayerCameraRig>(scene)
                .GetComponent<Camera>();
            MainstreetAtmosphere.Apply(Root(scene, "Environment"), lighting, camera);
            Volume volume = lighting.Find("MainstreetColor").GetComponent<Volume>();
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                DataFolder + "/ArtVolume.asset"
            );
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }

        internal static Transform Root(Scene scene, string name) =>
            scene.GetRootGameObjects().Single(root => root.name == name).transform;
    }
}
