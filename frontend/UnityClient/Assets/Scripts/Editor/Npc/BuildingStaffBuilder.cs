using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Npc;
using A305.Player;
using A305.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 마을의 들어갈 수 있는 건물 9곳(1층 매장 6곳·북쪽 블록 3곳)에 주·야간 직원 NPC를 두고, 생활하며 자기 건물 1층 안에서만 지내게 한다.
    /// 외형과 배색은 외형 표(<see cref="NpcCast"/>)를 따르고, 색을 바꾼 재질 사본은 <see cref="NpcCastDresser"/>가 입힌다.
    /// 기존 직원의 위치는 유지하고, 전직한 인물은 새 일터 안으로 옮겨 연결·색·구역을 맞춘다.
    /// </summary>
    public static class BuildingStaffBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Build Building Staff";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string SettingsPath = "Assets/Data/Npc/BuildingStaffSettings.asset";

        // 건물 기준 좌표: 앞면(문) z=0, 뒷벽 z=-7.8, 벽 x=±3.8, 1층 바닥 y=0, 2층 바닥 y=4. 1층 실내만 감싼다.
        internal static readonly Bounds GroundFloor = new Bounds(
            new Vector3(0, 1.75f, -3.9f),
            new Vector3(7.6f, 3.3f, 7.6f)
        );

        private static readonly Staff[] Staffs =
        {
            new Staff(
                "POST_OFFICE_STAFF",
                "PostOfficeStaff",
                "PostOffice",
                new Vector3(-1.5f, 0, -4.5f)
            ),
            new Staff("DINER_STAFF", "DinerStaff", "MapleDiner", new Vector3(.4f, 0, -6.4f)),
            new Staff("BANK_STAFF", "BankStaff", "TownBank", new Vector3(2.5f, 0, -4.6f)),
            new Staff(
                "LAUNDROMAT_STAFF",
                "LaundromatStaff",
                "SudsLaundromat",
                new Vector3(.2f, 0, -5f)
            ),
            new Staff(
                "FLOWER_SHOP_STAFF",
                "FlowerShopStaff",
                "BloomFlowers",
                new Vector3(.9f, 0, -6.4f)
            ),
            new Staff(
                "BARBER_STAFF",
                "BarberStaff",
                "MainStreetBarber",
                new Vector3(-1.6f, 0, -4.1f)
            ),
            new Staff("BAKERY_STAFF", "BakeryStaff", "MapleBakery", new Vector3(-1.6f, 0, -4.4f)),
            new Staff("ARCADE_STAFF", "ArcadeStaff", "LittleArcade", new Vector3(-1.4f, 0, -4.6f)),
            new Staff(
                "HOME_STORE_STAFF",
                "HomeStoreStaff",
                "HomeLiving",
                new Vector3(-.3f, 0, -6.6f)
            ),
            new Staff(
                "TOWN_EVENING_WALKER",
                "BankEveningStaff",
                "TownBank",
                new Vector3(2.5f, 0, -4.6f),
                true
            ),
            new Staff(
                "TOWN_SHOPPER",
                "BankNightStaff",
                "TownBank",
                new Vector3(2.5f, 0, -4.6f),
                true
            ),
            new Staff(
                "TOWN_RETIREE",
                "PostOfficeNightStaff",
                "PostOffice",
                new Vector3(-1.5f, 0, -4.5f),
                true
            ),
            new Staff(
                "TOWN_STUDENT",
                "ArcadeNightStaff",
                "LittleArcade",
                new Vector3(-1.4f, 0, -4.6f),
                true
            ),
            new Staff(
                "TOWN_HANDYMAN",
                "HomeStoreNightStaff",
                "HomeLiving",
                new Vector3(-.3f, 0, -6.6f),
                true
            ),
            new Staff(
                "FIRE_CAPTAIN",
                "DinerNightStaff",
                "MapleDiner",
                new Vector3(.4f, 0, -6.4f),
                true
            ),
            new Staff(
                "FIRE_FIGHTER",
                "LaundromatNightStaff",
                "SudsLaundromat",
                new Vector3(.2f, 0, -5f),
                true
            ),
            new Staff(
                "FIRE_ROOKIE",
                "BarberNightStaff",
                "MainStreetBarber",
                new Vector3(-1.6f, 0, -4.1f),
                true
            ),
            new Staff(
                "HOTEL_CONCIERGE",
                "FlowerShopNightStaff",
                "BloomFlowers",
                new Vector3(.9f, 0, -6.4f),
                true,
                "HotelScene"
            ),
            new Staff(
                "POLICE_PATROL_OFFICER",
                "BakeryNightStaff",
                "MapleBakery",
                new Vector3(-1.6f, 0, -4.4f),
                true,
                "PoliceStationScene"
            ),
        };

        internal static bool IsReassigned(string code)
        {
            return Staffs.Any(staff => staff.Code == code && staff.Reassigned);
        }

        [MenuItem("Tools/A305/Npc/Apply 24 Hour Roster")]
        public static void Apply24HourRoster()
        {
            BuilderTools.ThrowIfPlaying();
            Build();
            UpdateReassignedScripts();
            ApplyReassignedVisuals();
            NpcPopulationBuilder.Build();
            PoliceStationBuilder.ConnectDispatch();
        }

        public static void ApplyReassignedVisuals()
        {
            BuilderTools.ThrowIfPlaying();
            NpcCastMember member = NpcCast.Find("POLICE_PATROL_OFFICER");
            string shopPath = ShopScenePath(Staffs.Single(staff => staff.Code == member.Code));
            Scene shop = EditorSceneManager.OpenScene(shopPath);
            NpcIdentity worker = BuilderTools
                .FindAllInScene<NpcIdentity>(shop)
                .Single(npc => npc.Code == member.Code);
            NpcVisualBuilder.Connect(worker, member.Look.PrefabPath, true);
            NpcCastDresser.Dress(worker.gameObject, member);
            RefreshTutorialPassersby(shop);
            BuilderTools.SaveScene(shop, shopPath);
            // 재배치한 Kevin는 제복 대신 현재 외형 표의 사복을 출퇴근 프리팹에도 반영한다.
            string path = "Assets/Prefabs/Npc/Commuters/POLICE_PATROL_OFFICER.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                NpcVisualBuilder.Connect(
                    prefab.GetComponent<NpcIdentity>(),
                    member.Look.PrefabPath,
                    true
                );
                NpcCastDresser.Dress(prefab, member);
                Object.DestroyImmediate(prefab.GetComponent<PoliceReportDesk>());
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            AssetDatabase.SaveAssets();
            foreach (
                string sceneName in Staffs
                    .Where(staff => staff.SourceScene != null)
                    .Select(staff => staff.SourceScene)
                    .Distinct()
            )
            {
                Scene source = EditorSceneManager.OpenScene(
                    "Assets/Scenes/Production/" + sceneName + ".unity",
                    OpenSceneMode.Additive
                );
                RefreshTutorialPassersby(source);
                BuilderTools.SaveScene(source, source.path);
                EditorSceneManager.CloseScene(source, true);
            }
        }

        /// <summary>장면의 튜토리얼 진행이 그 장면에 놓인 행인을 모두 알게 한다. 장면 저장은 부르는 쪽이 한다.</summary>
        internal static void RefreshTutorialPassersby(Scene scene)
        {
            TutorialDirector tutorial = BuilderTools.FindSingleInScene<TutorialDirector>(scene);
            if (tutorial == null)
                return;
            Passerby[] bodies = BuilderTools.FindAllInScene<Passerby>(scene);
            var fields = new SerializedObject(tutorial);
            SerializedProperty list = fields.FindProperty("passersby");
            list.arraySize = bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            PlaceStaff();
            // 생활 데이터가 있는 NPC에 생활을 붙이고 바닥을 다시 굽는다. 그 뒤 직원마다 구역을 정한다.
            NpcLifeBuilder.Build();
            LimitStaffToBuildings();
            Debug.Log("마을 건물 9곳에 직원 NPC를 두고 건물 안에서만 지내게 했습니다.");
        }

        /// <summary>
        /// 직원을 자기 건물의 1층 매장 장면 안에 두고 외형·색·대화를 잇는다. 매장 직원은 근무 중에 매장 장면에만 있고 마을에는 출퇴근하는 모습만 나온다.
        /// 생활(구역)은 <see cref="Build"/>가 이어서 맞춘다.
        /// </summary>
        public static void PlaceStaff()
        {
            BuilderTools.ThrowIfPlaying();
            NpcBuilder.PrepareMockLines();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            RestorePoliceRookie(town);
            BuilderTools.SaveScene(town, TownScenePath);
            foreach (IGrouping<string, Staff> building in Staffs.GroupBy(staff => staff.Building))
            {
                string path = ShopScenePath(building.First());
                Scene shop = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (Staff staff in building.Where(item => item.SourceScene != null))
                    TransferFromScene(shop, staff);
                foreach (Staff staff in building)
                    Place(shop, staff);
                NpcBuilder.ConnectToScene(
                    BuilderTools.EnsureGroups(shop),
                    BuilderTools.FindSingleInScene<PlayerMover>(shop),
                    BuilderTools.FindSingleInScene<DialogueController>(shop)
                );
                RefreshTutorialPassersby(shop);
                BuilderTools.SaveScene(shop, path);
            }
            AssetDatabase.SaveAssets();
        }

        private static string ShopScenePath(Staff staff) =>
            TownShopSceneBuilder.GroundFloorScenePath(staff.Building);

        private static void TransferFromScene(Scene shop, Staff staff)
        {
            if (BuilderTools.FindAllInScene<NpcIdentity>(shop).Any(npc => npc.Code == staff.Code))
                return;
            Scene source = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/" + staff.SourceScene + ".unity",
                OpenSceneMode.Additive
            );
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(source)
                .Single(candidate => candidate.Code == staff.Code);
            npc.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(npc.gameObject, shop);
            npc.transform.SetParent(BuilderTools.EnsureGroups(shop).Characters, true);
            // 외부 장면으로 옮긴 NPC에 대한 생활 관리 참조도 함께 제거한다.
            NpcPopulation population = BuilderTools.FindSingleInScene<NpcPopulation>(source);
            if (population != null)
            {
                var fields = new SerializedObject(population);
                SerializedProperty residents = fields.FindProperty("residents");
                for (int i = residents.arraySize - 1; i >= 0; i--)
                    if (
                        residents
                            .GetArrayElementAtIndex(i)
                            .FindPropertyRelative("Worker")
                            .objectReferenceValue == npc.gameObject
                    )
                        residents.DeleteArrayElementAtIndex(i);
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            RefreshTutorialPassersby(source);
            BuilderTools.SaveScene(source, source.path);
            EditorSceneManager.CloseScene(source, true);
        }

        // 이전 재배치 결과에서도 여성 경찰은 경찰서로 복원하고, 남성 Kevin만 빵집으로 옮긴다.
        private static void RestorePoliceRookie(Scene town)
        {
            var officer = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .SingleOrDefault(npc => npc.Code == "POLICE_ROOKIE");
            if (officer == null)
                return;
            Scene station = EditorSceneManager.OpenScene(
                PoliceStationBuilder.StationScenePath,
                OpenSceneMode.Additive
            );
            if (
                BuilderTools
                    .FindAllInScene<NpcIdentity>(station)
                    .Any(npc => npc.Code == officer.Code)
            )
                throw new System.InvalidOperationException(
                    "여성 경찰이 두 씬에 중복 배치되어 있습니다."
                );
            Transform building = Building(station, "PoliceStation");
            var population = BuilderTools.FindSingleInScene<NpcPopulation>(town);
            if (population != null)
            {
                var fields = new SerializedObject(population);
                SerializedProperty residents = fields.FindProperty("residents");
                for (int i = residents.arraySize - 1; i >= 0; i--)
                    if (
                        residents
                            .GetArrayElementAtIndex(i)
                            .FindPropertyRelative("Worker")
                            .objectReferenceValue == officer.gameObject
                    )
                        residents.DeleteArrayElementAtIndex(i);
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            officer.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(officer.gameObject, station);
            officer.transform.SetParent(BuilderTools.EnsureGroups(station).Characters, true);
            officer.transform.SetPositionAndRotation(
                building.TransformPoint(new Vector3(-1.6f, 0, -7.2f)),
                building.rotation
            );
            officer.name = "PoliceRookie";
            PoliceStationBuilder.PlaceOfficers(station);
            PoliceStationBuilder.KeepOfficersInStation(station);
            RefreshTutorialPassersby(station);
            BuilderTools.SaveScene(station, station.path);
            EditorSceneManager.CloseScene(station, true);
        }

        private static void UpdateReassignedScripts()
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            var defaults = ScriptableObject.CreateInstance<MockDialogueScriptSource>();
            try
            {
                var fields = new SerializedObject(scripts);
                SerializedProperty entries = fields.FindProperty("entries");
                SerializedProperty templates = new SerializedObject(defaults).FindProperty(
                    "entries"
                );
                foreach (
                    string code in Staffs
                        .Where(item => item.Reassigned)
                        .Select(item => item.Code)
                        .Append("POLICE_ROOKIE")
                )
                {
                    for (int i = 0; i < templates.arraySize; i++)
                    {
                        SerializedProperty template = templates.GetArrayElementAtIndex(i);
                        if (template.FindPropertyRelative("npcCode").stringValue != code)
                            continue;
                        for (int j = 0; j < entries.arraySize; j++)
                            if (
                                entries
                                    .GetArrayElementAtIndex(j)
                                    .FindPropertyRelative("npcCode")
                                    .stringValue == code
                            )
                                entries.GetArrayElementAtIndex(j).boxedValue = template.boxedValue;
                    }
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(scripts);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        /// <summary>생활이 붙은 직원마다 자기 건물 1층 구역 안에서만 사물·자리를 고르게 한다.</summary>
        public static void LimitStaffToBuildings()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (IGrouping<string, Staff> building in Staffs.GroupBy(staff => staff.Building))
            {
                string path = ShopScenePath(building.First());
                Scene shop = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (Staff staff in building)
                    LimitToGroundFloor(shop, staff.Code, staff.Building);
                BuilderTools.SaveScene(shop, path);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>생활이 붙은 NPC가 그 건물 1층 구역 안에서만 사물·자리를 고르게 한다. 장면 저장은 부르는 쪽이 한다.</summary>
        internal static void LimitToGroundFloor(Scene scene, string npcCode, string buildingName)
        {
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .Single(candidate => candidate.Code == npcCode);
            var routine = npc.GetComponent<NpcRoutine>();
            if (routine == null)
            {
                throw new System.InvalidOperationException(
                    npcCode
                        + "에 생활(NpcRoutine)이 없습니다. ai/tools/npc_life_export.py로 생활 데이터(Data/Npc/NpcLife.json)를 먼저 내보내세요."
                );
            }

            var fields = new SerializedObject(routine);
            fields.FindProperty("stayInArea").boolValue = true;
            fields.FindProperty("area").boundsValue = WorldBounds(
                Building(scene, buildingName),
                GroundFloor
            );
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Transform Building(Scene scene, string name)
        {
            Transform found = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == name);
            if (found == null)
            {
                throw new System.InvalidOperationException("마을 씬에 건물이 없습니다: " + name);
            }
            return found;
        }

        private static void Place(Scene scene, Staff staff)
        {
            NpcCastMember member = NpcCast.Find(staff.Code);
            Transform building = Building(scene, staff.Building);
            NpcIdentity npc = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(candidate => candidate.Code == staff.Code);
            if (npc == null)
            {
                GameObject placed = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        NpcBuilder.PasserbyPrefabPath,
                        MenuPath
                    ),
                    BuilderTools.EnsureGroups(scene).Characters,
                    NpcPlacement.FreeSpot(building, staff.Stand, GroundFloor),
                    building.rotation
                );
                placed.name = staff.ObjectName;
                npc = placed.GetComponent<NpcIdentity>();
                BuilderTools.SetText(npc, "code", staff.Code);
            }

            Passerby passerby = npc.GetComponent<Passerby>();
            if (staff.Reassigned)
            {
                // 같은 인물을 옮기고 이전 직장의 신고 기능은 제거한다. ID와 목소리는 유지한다.
                Object.DestroyImmediate(npc.GetComponent<PoliceReportDesk>());
                if (!GroundFloor.Contains(building.InverseTransformPoint(npc.transform.position)))
                    npc.transform.SetPositionAndRotation(
                        NpcPlacement.FreeSpot(building, staff.Stand, GroundFloor),
                        building.rotation
                    );
                npc.gameObject.name = staff.ObjectName;
            }
            BuilderTools.SetReference(passerby, "settings", Settings());
            BuilderTools.SetReference(
                passerby,
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(
                    NpcBuilder.MockLinesPath,
                    MenuPath
                )
            );
            NpcVisualBuilder.Connect(npc, member.Look.PrefabPath, true);
            NpcCastDresser.Dress(npc.gameObject, member);
        }

        // 건물은 90도 단위로 돌아 있어 모서리 8개를 감싼 축 정렬 상자가 실내와 같다.
        private static Bounds WorldBounds(Transform building, Bounds local)
        {
            var world = new Bounds(building.TransformPoint(local.center), Vector3.zero);
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                world.Encapsulate(
                    building.TransformPoint(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))
                    )
                );
            }
            return world;
        }

        // 카페 직원처럼 손님에게 걸어가지 않고, 가까이 오면 대화를 제안한다.
        private static PasserbySettings Settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PasserbySettings>(SettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<PasserbySettings>();
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = 3.5f;
            fields.FindProperty("offerDistanceMeters").floatValue = 2.5f;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
            fields.FindProperty("restSeconds").floatValue = 20;
            fields.FindProperty("canDecline").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private sealed class Staff
        {
            public Staff(
                string code,
                string objectName,
                string building,
                Vector3 stand,
                bool reassigned = false,
                string sourceScene = null
            )
            {
                Code = code;
                ObjectName = objectName;
                Building = building;
                Stand = stand;
                Reassigned = reassigned;
                SourceScene = sourceScene;
            }

            public string Code { get; }
            public string ObjectName { get; }
            public string Building { get; }

            /// <summary>건물 기준 서 있을 자리. 가구와 겹치면 가까운 빈자리로 옮긴다.</summary>
            public Vector3 Stand { get; }
            public bool Reassigned { get; }
            public string SourceScene { get; }
        }
    }
}
