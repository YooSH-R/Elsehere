using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 마을의 들어갈 수 있는 건물 9곳(1층 매장 6곳·북쪽 블록 3곳)에 직원 NPC를 한 명씩 두고, 생활하며 자기 건물 1층 안에서만 지내게 한다.
    /// 외형과 배색은 외형 표(<see cref="NpcCast"/>)를 따르고, 색을 바꾼 재질 사본은 <see cref="NpcCastDresser"/>가 입힌다.
    /// 같은 NPC가 이미 있으면 위치는 그대로 두고 연결·색·구역만 맞춘다.
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
        };

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

        /// <summary>직원을 건물 안에 두고 외형·색·대화를 잇는다. 생활(구역)은 <see cref="Build"/>가 이어서 맞춘다.</summary>
        public static void PlaceStaff()
        {
            BuilderTools.ThrowIfPlaying();
            NpcBuilder.PrepareMockLines();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            foreach (Staff staff in Staffs)
            {
                Place(town, staff);
            }
            NpcBuilder.ConnectToScene(
                BuilderTools.EnsureGroups(town),
                BuilderTools.FindSingleInScene<PlayerMover>(town),
                BuilderTools.FindSingleInScene<DialogueController>(town)
            );
            BuilderTools.SaveScene(town, TownScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>생활이 붙은 직원마다 자기 건물 1층 구역 안에서만 사물·자리를 고르게 한다.</summary>
        public static void LimitStaffToBuildings()
        {
            BuilderTools.ThrowIfPlaying();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            foreach (Staff staff in Staffs)
            {
                LimitToGroundFloor(town, staff.Code, staff.Building);
            }
            BuilderTools.SaveScene(town, TownScenePath);
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
            public Staff(string code, string objectName, string building, Vector3 stand)
            {
                Code = code;
                ObjectName = objectName;
                Building = building;
                Stand = stand;
            }

            public string Code { get; }
            public string ObjectName { get; }
            public string Building { get; }

            /// <summary>건물 기준 서 있을 자리. 가구와 겹치면 가까운 빈자리로 옮긴다.</summary>
            public Vector3 Stand { get; }
        }
    }
}
