using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.ObjectHover;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>기존 마을의 빈 건물 1층을 드럭스토어 정면으로 꾸며 출입구를 연결하고 없는 실내만 새로 만든다.</summary>
    public static class FirstDayShopBuilder
    {
        private const string ScenePath = "Assets/Scenes/Production/ShopScene.unity";
        private const string MenuPath = "Tools/A305/Mission/Build First Day Journey";
        private const string StoreFrontName = "DrugstoreFront";
        private const string RetiredStoreFrontName = "ConvenienceStoreFront";
        private const string VacantGroundFloorPath =
            "TownMainstreet/BackgroundBuildings/VacantBuilding2F/ButterResidence2F/ButterGroundFloor8m";
        private const string KitPartFolder = "Assets/Prefabs/World/CafeBlenderKit/Parts/";
        private const string TownSignText = "DRUGSTORE";
        private const string PriceSignText = "DRUGSTORE\nUMBRELLAS · 8 COINS";
        private const string CameraSettingsPath = "Assets/Data/Shop/ShopCameraSettings.asset";

        // 문 바로 앞에 서면 3인칭 카메라가 문에 막혀 캐릭터에 붙으므로 매장 안쪽에 내려 준다.
        private static readonly Vector3 EntryOffset = new Vector3(0, .1f, 3.6f);

        [MenuItem("Tools/A305/Mission/Adjust Shop Counter Sightline")]
        public static void AdjustSightline()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            NpcIdentity staff = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(npc => npc.Code == "SHOP_STAFF");
            // 시선은 직원 외형의 눈 높이에 맞춘다.
            NpcVisualBuilder.Connect(staff);
            Transform counter = scene
                .GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownShopInterior/ShopCounter");
            float meshHeight = counter.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            counter.localScale = new Vector3(1, .8f / meshHeight, 1);
            counter.localPosition = new Vector3(0, .4f, 2.4f);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        public static void BuildShop()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Scene existing = EditorSceneManager.OpenScene(ScenePath);
                SpaceSceneSetup.Apply(existing);
                SetSignText(
                    BuilderTools.EnsureGroups(existing).Environment,
                    "TownShopInterior/ShopPriceSign/PriceLabel",
                    PriceSignText
                );
                DrugstoreInteriorBuilder.Apply(existing);
                ConfigureInteriorCamera(existing);
                BuilderTools.SaveScene(existing, ScenePath);
                return;
            }
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single
            );
            SceneGroups groups = BuilderTools.EnsureGroups(scene);
            Transform art = Group("TownShopInterior", groups.Environment, Vector3.zero);
            Solid(
                "ShopFloor",
                art,
                new Vector3(0, -.12f, 0),
                new Vector3(12, .2f, 14),
                Cream,
                "floor"
            );
            Solid("BackWall", art, new Vector3(0, 2, 7), new Vector3(12, 4, .2f), Cream, "wall");
            Solid("WestWall", art, new Vector3(-6, 2, 0), new Vector3(.2f, 4, 14), Cream, "wall");
            Solid("EastWall", art, new Vector3(6, 2, 0), new Vector3(.2f, 4, 14), Cream, "wall");
            Solid(
                "EntranceLeft",
                art,
                new Vector3(-3.5f, 2, -7),
                new Vector3(5, 4, .2f),
                Cream,
                "wall"
            );
            Solid(
                "EntranceRight",
                art,
                new Vector3(3.5f, 2, -7),
                new Vector3(5, 4, .2f),
                Cream,
                "wall"
            );
            Solid(
                "ShopCounter",
                art,
                new Vector3(0, .4f, 2.4f),
                new Vector3(5, .8f, .85f),
                Wood,
                "counter"
            );
            Transform shelf = Solid(
                "UmbrellaShelf",
                art,
                new Vector3(-3.8f, 1, 3.7f),
                new Vector3(1.6f, 2, .4f),
                Green,
                "shelf"
            );
            ObjectWordDefinition umbrella = UmbrellaWord();
            for (int index = 0; index < 3; index++)
            {
                Transform display = Group(
                    "Umbrella" + index,
                    art,
                    new Vector3(-3.8f, 1.9f, 1.8f - index * 1.2f)
                );
                Transform canopy = Ball(
                    "Canopy",
                    display,
                    new Vector3(0, .4f, 0),
                    new Vector3(1.2f, .35f, 1.2f),
                    index % 2 == 0 ? Green : Navy
                );
                Block(
                    "Handle",
                    display,
                    new Vector3(0, -.2f, 0),
                    new Vector3(.05f, 1.15f, .05f),
                    Wood
                );
                var hit = display.gameObject.AddComponent<BoxCollider>();
                hit.size = new Vector3(1.2f, 1.5f, 1.2f);
                var hover = display.gameObject.AddComponent<HoverableObject>();
                BuilderTools.SetReference(hover, "word", umbrella);
            }
            Transform sign = Solid(
                "ShopPriceSign",
                art,
                new Vector3(0, 2.8f, 6.7f),
                new Vector3(6, 1.3f, .12f),
                Green,
                "sign"
            );
            TownBlockoutGeometry
                .Label(
                    "PriceLabel",
                    PriceSignText,
                    sign,
                    new Vector3(0, 0, -.1f),
                    Vector3.zero,
                    .35f
                )
                .color = Cream.color;
            Door(
                groups.Environment,
                "TownDoor",
                new Vector3(0, 0, -6.8f),
                0,
                "TownScene",
                "ShopDoor"
            );
            Staff(groups);
            var sun = new GameObject("ShopSun").AddComponent<Light>();
            sun.transform.SetParent(groups.Lighting, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .6f);
            DrugstoreInteriorBuilder.Apply(scene);
            BuilderTools.SaveScene(scene, ScenePath);
            SpaceSceneSetup.Apply(scene);
            ConfigureInteriorCamera(scene);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        [MenuItem("Tools/A305/Mission/Apply Shop Interior Camera")]
        public static void ApplyInteriorCamera()
        {
            BuilderTools.ThrowIfPlaying();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureInteriorCamera(scene);
            BuilderTools.SaveScene(scene, ScenePath);
        }

        /// <summary>
        /// 카페·호텔처럼 좁은 실내용 카메라 거리와 시야를 쓰고, 입장 지점을 문에서 매장 안쪽으로 옮긴다.
        /// 마을용 카메라 거리(5m)는 매장 안에서 벽에 막혀 캐릭터에 붙는다. 설정 에셋은 없을 때만 만든다.
        /// </summary>
        private static void ConfigureInteriorCamera(Scene scene)
        {
            PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
            var settings = AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>(CameraSettingsPath);
            if (settings == null)
            {
                BuilderTools.EnsureFolder("Assets/Data/Shop");
                settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
                var data = new SerializedObject(settings);
                data.FindProperty("startDistanceMeters").floatValue = 3.8f;
                data.FindProperty("minDistanceMeters").floatValue = 1.5f;
                data.FindProperty("maxDistanceMeters").floatValue = 5.2f;
                data.FindProperty("startPitchDegrees").floatValue = 15;
                data.FindProperty("pivotHeightMeters").floatValue = 1.5f;
                data.FindProperty("eyeHeightMeters").floatValue = 1.074f;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, CameraSettingsPath);
            }
            BuilderTools.SetReference(rig, "cameraSettings", settings);
            Camera camera = rig.GetComponent<Camera>();
            camera.fieldOfView = 65;
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 90;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);

            SpaceDoorway door = BuilderTools
                .FindAllInScene<SpaceDoorway>(scene)
                .Single(doorway => doorway.DoorwayId == "TownDoor");
            door.ArrivalPoint.localPosition = EntryOffset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(door.ArrivalPoint);
            Transform player = BuilderTools.FindSingleInScene<PlayerMover>(scene).transform;
            player.position = door.ArrivalPoint.position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        }

        /// <summary>
        /// 마을의 빈 2층 건물 1층을 드럭스토어 정면으로 꾸미고 출입구를 그 벽에 붙인다.
        /// 옆 카페와 같은 상점 부품(출입문·진열창·차양·벽등)으로 한 건물처럼 보이게 하고, 간판은 카페처럼 차양에 적는다.
        /// 이미 꾸민 정면은 디자인을 고쳤을 수 있으므로 그대로 둔다.
        /// </summary>
        public static void ConnectTownEntrance(SceneGroups groups)
        {
            Transform existingFront = groups.Environment.Find(StoreFrontName);
            Transform retiredFront = groups.Environment.Find(RetiredStoreFrontName);
            if (existingFront == null && retiredFront != null)
            {
                // 상점을 편의점에서 드럭스토어로 바꿨다. 꾸민 정면은 그대로 두고 이름과 간판 글자만 바꾼다.
                retiredFront.name = StoreFrontName;
                existingFront = retiredFront;
            }
            if (existingFront != null)
            {
                SetSignText(groups.Environment, StoreFrontName + "/StoreSign", TownSignText);
                HideVacantClosure(groups.Environment);
                AttachDoor(groups, existingFront);
                return;
            }
            Transform groundFloor = groups.Environment.Find(VacantGroundFloorPath);
            if (groundFloor == null)
                throw new System.InvalidOperationException(
                    "Vacant building for the store missing: " + VacantGroundFloorPath
                );
            Bounds building = groundFloor.Find("Walls").GetComponent<Renderer>().bounds;
            // 빈 건물은 길(+X)을 향한다. 정면 가운데 바닥에 카페 1층과 같은 좌표계(앞쪽 +Z)를 둔다.
            Transform front = BuilderTools.CreateEmpty(
                StoreFrontName,
                groups.Environment,
                new Vector3(building.max.x, building.min.y, building.center.z),
                Quaternion.Euler(0, 90, 0)
            );
            HideResidentialFront(groundFloor, building.max.x);
            HideVacantClosure(groups.Environment);
            // 부품은 벽 앞면과 겹쳐 깜빡이지 않도록 몇 cm 앞에 붙인다.
            KitPart("Entrance4m", front, new Vector3(0, 0, .02f));
            foreach (float x in new[] { -3f, 3f })
                KitPart("WindowShop2m", front, new Vector3(x, 0, .04f));
            Material awning = Color("StoreAwningCoral", "#D9573F");
            foreach (float x in new[] { -3f, -1f, 1f, 3f })
            foreach (
                Renderer part in KitPart("Awning2m", front, new Vector3(x, 2.3f, .04f))
                    .GetComponentsInChildren<Renderer>()
            )
            {
                part.sharedMaterial = part.name.EndsWith("Edge") ? Cream : awning;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }
            foreach (float x in new[] { -3f, 0f, 3f })
                KitPart("WallLamp", front, new Vector3(x, 3.48f, .1f));
            TextMeshPro sign = Text(
                "StoreSign",
                TownSignText,
                front,
                new Vector3(0, 2.83f, .89f),
                .42f
            );
            sign.transform.localRotation = Quaternion.Euler(65, 180, 0);
            AttachDoor(groups, front);
            // 예전에는 건물 2m 앞에 간판과 문을 따로 세웠다.
            Transform floatingSign = groups.Environment.Find("TownShopSign");
            if (floatingSign != null)
                Object.DestroyImmediate(floatingSign.gameObject);
        }

        private static void HideVacantClosure(Transform environment)
        {
            Transform vacant = environment.Find(
                "TownMainstreet/BackgroundBuildings/VacantBuilding2F"
            );
            foreach (Transform child in vacant)
            {
                if (
                    child.name
                    is not ("ClosedShutter" or "ShutterSlat" or "StatusPlate" or "Status")
                )
                    continue;
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }

        /// <summary>빈 건물 1층 정면의 닫힌 창·현관문·걸레받이는 진열창과 출입문으로 바꾸므로 숨긴다.</summary>
        private static void HideResidentialFront(Transform groundFloor, float frontX)
        {
            foreach (Transform child in groundFloor)
            {
                bool isFrontDressing =
                    child.name
                    is "ClosedWindow"
                        or "DoorFrame"
                        or "DoorPanel"
                        or "Handle"
                        or "ClosedResidentialDoor"
                        or "Skirting";
                Renderer shape = child.GetComponentInChildren<Renderer>();
                if (!isFrontDressing || shape == null || shape.bounds.center.x < frontX - .5f)
                    continue;
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }

        /// <summary>출입문을 드럭스토어 정면 가운데에 붙이고, 카페처럼 문 그림을 출입문 틀에 맞춘다.</summary>
        private static void AttachDoor(SceneGroups groups, Transform front)
        {
            if (
                !BuilderTools
                    .FindAllInScene<SpaceDoorway>(groups.Scene)
                    .Any(door => door.DoorwayId == "ShopDoor")
            )
                Door(groups.Environment, "ShopDoor", Vector3.zero, 0, "ShopScene", "TownDoor");
            SpaceDoorway doorway = BuilderTools
                .FindAllInScene<SpaceDoorway>(groups.Scene)
                .Single(door => door.DoorwayId == "ShopDoor");
            doorway.transform.SetParent(front, false);
            doorway.transform.localPosition = new Vector3(0, 0, .07f);
            doorway.transform.localRotation = Quaternion.identity;
            var trigger = doorway.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0, 1.3f, .4f);
            trigger.size = new Vector3(1.8f, 2.6f, .8f);
            doorway.ArrivalPoint.localPosition = new Vector3(0, 0, 2);
            // 상점답게 안이 비치는 유리문 색으로 칠한다.
            Transform visual = doorway.transform.Find("Visual");
            visual.localScale = new Vector3(1.5f, 1.18f, 1);
            foreach (Renderer panel in visual.GetComponentsInChildren<Renderer>())
            {
                panel.sharedMaterial = Glass;
                PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorway.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorway.ArrivalPoint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        private static GameObject KitPart(string name, Transform parent, Vector3 position) =>
            BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(
                    KitPartFolder + name + ".prefab",
                    MenuPath
                ),
                parent,
                position,
                Quaternion.identity
            );

        /// <summary>이미 만든 간판은 모양과 위치를 두고 글자만 바꾼다.</summary>
        private static void SetSignText(Transform environment, string labelPath, string text)
        {
            Transform label = environment.Find(labelPath);
            if (label == null)
                throw new System.InvalidOperationException(
                    "Store sign label missing: " + labelPath
                );
            var sign = label.GetComponent<TextMeshPro>();
            sign.text = text;
            EditorUtility.SetDirty(sign);
        }

        private static void Door(
            Transform parent,
            string id,
            Vector3 position,
            float yaw,
            string destination,
            string arrival
        )
        {
            GameObject root = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(
                    SpaceSceneBuilder.DoorwayPrefabPath,
                    MenuPath
                ),
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
            root.name = id;
            var doorway = root.GetComponent<SpaceDoorway>();
            BuilderTools.SetText(doorway, "doorwayId", id);
            BuilderTools.SetText(doorway, "destinationScene", destination);
            BuilderTools.SetText(doorway, "destinationDoorwayId", arrival);
            var trigger = root.GetComponent<BoxCollider>();
            trigger.center = new Vector3(0, 1.3f, .45f);
            trigger.size = new Vector3(1.8f, 2.6f, 1);
            doorway.ArrivalPoint.localPosition = EntryOffset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            PrefabUtility.RecordPrefabInstancePropertyModifications(doorway.ArrivalPoint);
        }

        private static void Staff(SceneGroups groups)
        {
            GameObject staff = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(NpcBuilder.PasserbyPrefabPath, MenuPath),
                groups.Characters,
                new Vector3(0, .05f, 3.6f),
                Quaternion.Euler(0, 180, 0)
            );
            staff.name = "ShopStaff";
            BuilderTools.SetText(staff.GetComponent<NpcIdentity>(), "code", "SHOP_STAFF");
            staff.GetComponent<NpcIdentity>().LookTarget.localPosition = new Vector3(0, 1.12f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                staff.GetComponent<NpcIdentity>().LookTarget
            );
            BuilderTools.EnsureFolder("Assets/Data/Mission");
            string settingsPath = "Assets/Data/Mission/ShopStaffSettings.asset";
            var settings = BuilderTools.LoadOrCreateAsset<PasserbySettings>(settingsPath);
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = 4;
            fields.FindProperty("offerDistanceMeters").floatValue = 3;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
            fields.FindProperty("restSeconds").floatValue = 5;
            fields.FindProperty("canDecline").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            BuilderTools.SetReference(staff.GetComponent<Passerby>(), "settings", settings);
            BuilderTools.SetReference(
                staff.GetComponent<Passerby>(),
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(
                    "Assets/Data/Npc/MockNpcLines.asset",
                    MenuPath
                )
            );
            NpcVisualBuilder.Connect(staff.GetComponent<NpcIdentity>());
        }

        private static Transform Solid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            string word
        )
        {
            Transform shape = Block(name, parent, position, size, material);
            var collider = shape.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            var hover = shape.gameObject.AddComponent<HoverableObject>();
            BuilderTools.SetReference(
                hover,
                "word",
                BuilderTools.LoadRequiredAsset<ObjectWordDefinition>(
                    ObjectWordAuthoring.WordsFolder + "/" + word + ".asset",
                    MenuPath
                )
            );
            return shape;
        }

        private static ObjectWordDefinition UmbrellaWord()
        {
            string path = ObjectWordAuthoring.WordsFolder + "/umbrella.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(path);
            if (definition != null)
                return definition;
            definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            var fields = new SerializedObject(definition);
            fields.FindProperty("details.text").stringValue = "umbrella";
            fields.FindProperty("details.meaningKo").stringValue = "우산";
            fields.FindProperty("details.example").stringValue = "Where can I buy an umbrella?";
            fields.FindProperty("details.exampleKo").stringValue = "우산은 어디에서 살 수 있나요?";
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            ObjectWordAuthoring.EnsureIdentity(definition);
            return definition;
        }
    }
}
