using System.Linq;
using A305.EditorTools.Common;
using A305.Npc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>
    /// 마을 매장 실내는 카페·경찰서처럼 따로 장면이다. 마을의 매장 문에 닿으면 마을을 내리고 그 매장 장면으로 옮기며,
    /// 마을에는 매장 안 구조와 직원이 남지 않는다. 대표로 우체국과 오락실 2층 서점 장면을 연다.
    /// </summary>
    public sealed class TownShopScenesTests
    {
        private const string TownPath = "Assets/Scenes/Production/TownScene.unity";
        private const string ShopFolder = "Assets/Scenes/ShopInteriors/";

        private static readonly string[] ShopScenes =
        {
            "PostOfficeInterior",
            "MapleDinerInterior",
            "TownBankInterior",
            "SudsLaundromatInterior",
            "BloomFlowersInterior",
            "MainStreetBarberInterior",
            "MapleBakeryInterior",
            "LittleArcadeInterior",
            "LittleArcadeUpperInterior",
            "HomeLivingInterior",
            "HomeLivingUpperInterior",
        };

        private Scene town;

        [OneTimeSetUp]
        public void OpenTown()
        {
            town = EditorSceneManager.OpenScene(TownPath);
        }

        [OneTimeTearDown]
        public void CloseScenes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // 매장 문 이름은 매장 장면 이름의 Interior를 Door로 바꾼 것이다(예: PostOfficeDoor).
        [Test]
        public void EveryTownShopDoor_LeadsToItsShopSceneTownDoor()
        {
            SpaceDoorway[] townDoors = BuilderTools.FindAllInScene<SpaceDoorway>(town);
            foreach (string shop in ShopScenes)
            {
                SpaceDoorway[] doors = townDoors
                    .Where(door => door.DestinationScene == shop)
                    .ToArray();
                Assert.That(doors, Has.Length.EqualTo(1), shop);
                Assert.That(doors[0].DoorwayId, Is.EqualTo(shop.Replace("Interior", "Door")));
                Assert.That(doors[0].DestinationDoorwayId, Is.EqualTo("TownDoor"), shop);
            }
        }

        // 손님이 걷는 마을에는 매장 안 바닥·천장·2층 바닥이 없다. 오락실 공용 계단실과 생활용품점 바깥 계단은 마을 통로로 남는다.
        [Test]
        public void TownShopBuildings_KeepNoShopInteriors()
        {
            string[] shopParts = BuilderTools
                .FindAllInScene<Transform>(town)
                .Where(part =>
                    part.name is "FloorSurface" or "UpperFloor" or "FullUpperFloor"
                    && part.parent != null
                    && part.parent.name is "Interior" or "TenantEntrances"
                )
                .Select(part => part.parent.parent.name + "/" + part.name)
                .ToArray();

            Assert.That(shopParts, Is.Empty);
            Assert.That(
                BuildingParts("PostOffice", "Interior"),
                Is.Empty,
                "거리 매장 건물에는 실내가 남지 않습니다."
            );
            Assert.That(
                BuildingParts("LittleArcade", "Interior"),
                Is.SupersetOf(new[] { "Stairs", "UpperLanding" }),
                "공용 계단실로 2층 서점 문까지 올라갑니다."
            );
            Assert.That(
                BuildingParts("HomeLiving", "TenantEntrances"),
                Does.Contain("ExteriorStairs"),
                "바깥 계단으로 2층 찻집 문까지 올라갑니다."
            );
        }

        // 매장 직원은 근무 중에 매장 장면 안에만 있고, 마을에는 출퇴근하는 모습만 나온다.
        [Test]
        public void Town_HasNoShopStaffAtWork()
        {
            NpcLifeData life = NpcLifeData.Parse(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Npc/NpcLife.json").text
            );
            string[] staff = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Where(npc => npc.GetComponent<NpcTownWalker>() == null)
                .Where(npc =>
                    ShopScenes.Contains(PlaceScenes.SceneOf(life.FindNpc(npc.Code)?.Workplace))
                )
                .Select(npc => npc.Code)
                .ToArray();

            Assert.That(staff, Is.Empty);
        }

        [Test]
        public void PostOfficeScene_HasItsPlayerStaffAndDoorBackToTown()
        {
            Scene shop = Open("PostOfficeInterior");

            SpaceDoorway exit = BuilderTools
                .FindAllInScene<SpaceDoorway>(shop)
                .Single(door => door.DoorwayId == "TownDoor");
            Assert.That(exit.DestinationScene, Is.EqualTo("TownScene"));
            Assert.That(exit.DestinationDoorwayId, Is.EqualTo("PostOfficeDoor"));
            Assert.That(BuilderTools.FindSingleInScene<SpaceArrival>(shop), Is.Not.Null);
            Assert.That(
                BuilderTools.FindAllInScene<A305.Player.PlayerMover>(shop),
                Has.Length.EqualTo(1)
            );
            Assert.That(
                BuilderTools.FindSingleInScene<OpeningHoursController>(shop),
                Is.Not.Null,
                "영업이 끝나면 마을로 내보냅니다."
            );
            NpcPopulation population = BuilderTools.FindSingleInScene<NpcPopulation>(shop);
            Assert.That(population, Is.Not.Null, "직원은 일과에 맞춰 매장 안에 나타납니다.");
            Assert.That(population.FindWorker("POST_OFFICE_STAFF"), Is.Not.Null);
            Assert.That(
                BuilderTools.FindAllInScene<Transform>(shop).Any(part => part.name == "PoBoxWall"),
                Is.True,
                "우체국 집기가 있어야 합니다."
            );
        }

        // 2층 가게 장면에는 그 층 집기만 있고, 1층 직원은 없다.
        [Test]
        public void UpperFloorShopScene_HasOnlyItsFloorFurnishingsAndNoStaff()
        {
            Scene shop = Open("LittleArcadeUpperInterior");
            string[] names = BuilderTools
                .FindAllInScene<Transform>(shop)
                .Select(part => part.name)
                .ToArray();

            Assert.That(names, Does.Contain("BookshopInterior"));
            Assert.That(names, Does.Not.Contain("ArcadeInterior"));
            Assert.That(BuilderTools.FindAllInScene<NpcIdentity>(shop), Is.Empty);
            Assert.That(
                BuilderTools
                    .FindAllInScene<SpaceDoorway>(shop)
                    .Single(door => door.DoorwayId == "TownDoor")
                    .DestinationDoorwayId,
                Is.EqualTo("LittleArcadeUpperDoor")
            );
        }

        // 매장 장면은 홈·카페·호텔·경찰서 장면처럼 그 매장 공간만 원점에 두고 문을 여닫지 않는다.
        // 2층 가게는 그 층 바닥이 높이 0이고, 마을에 속한 계단실은 두지 않는다. 1층 매장과 2층 서점으로 확인한다.
        [TestCase("PostOfficeInterior", "PostOffice")]
        [TestCase("LittleArcadeUpperInterior", "LittleArcade")]
        public void ShopScene_KeepsOnlyItsRoomAtTheOriginLikeOtherPlaceScenes(
            string sceneName,
            string building
        )
        {
            Scene shop = Open(sceneName);
            Transform root = BuilderTools
                .FindAllInScene<Transform>(shop)
                .Single(part => part.name == building && part.Find("Interior") != null);
            Vector3 arrival = BuilderTools
                .FindAllInScene<SpaceDoorway>(shop)
                .Single(door => door.DoorwayId == "TownDoor")
                .ArrivalPoint.position;

            Assert.That(new Vector2(root.position.x, root.position.z), Is.EqualTo(Vector2.zero));
            Assert.That(root.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(arrival.y, Is.EqualTo(0).Within(.15f), "매장 바닥이 높이 0에 온다.");
            Assert.That(
                BuilderTools.FindAllInScene<ShopSwingDoor>(shop).Where(door => door.enabled),
                Is.Empty,
                "문에 닿으면 장면을 옮기므로 문을 여닫지 않는다."
            );
            Assert.That(
                BuilderTools
                    .FindAllInScene<Transform>(shop)
                    .Select(part => part.name)
                    .Intersect(new[] { "Stairs", "UpperLanding", "ExteriorStairs" }),
                Is.Empty,
                "계단실·바깥 계단은 마을 공간이다."
            );
        }

        // 오락실 1층·2층 서점은 계단실 칸막이 없이 건물 폭(8m) 전체를 쓴다. 계단실 자리(x 0~3.8)도 그 층 바닥이 있고
        // 칸막이에 붙어 있던 소품은 오른쪽 벽으로 옮긴다. 2층 서점 문은 매장 앞벽에 있다.
        [TestCase("LittleArcadeInterior")]
        [TestCase("LittleArcadeUpperInterior")]
        public void ArcadeShopScene_UsesTheWholeFloorWithoutTheStairHallWall(string sceneName)
        {
            Scene shop = Open(sceneName);
            Transform[] parts = BuilderTools.FindAllInScene<Transform>(shop);

            Assert.That(
                parts
                    .Select(part => part.name)
                    .Intersect(new[] { "StairHallWall", "TenantDoorSign" }),
                Is.Empty,
                "계단실 칸막이와 계단실 안내는 마을 것이다."
            );
            Assert.That(
                BuilderTools
                    .FindAllInScene<Renderer>(shop)
                    .Where(floor => floor.enabled && floor.gameObject.activeInHierarchy)
                    .Any(floor => floor.bounds.Contains(new Vector3(2, -.05f, -4))),
                Is.True,
                "예전 계단실 자리에도 그 층 바닥이 있다."
            );
            Assert.That(
                parts
                    .Where(part => part.name is "ChangeMachine" or "WallShelfLongDecorated")
                    .Where(part => part.gameObject.activeInHierarchy)
                    .Select(part => part.position.x)
                    .Where(x => x > -.5f && x < .5f),
                Is.Empty,
                "칸막이 자리(x=0)에 걸린 소품이 없다."
            );
        }

        // 마을에서 서점 문은 서점 오른쪽 벽 앞쪽(계단참 쪽)에 있어 서쪽으로 들어간다. 매장 장면에서도 같은 자리·같은 방향이다.
        [Test]
        public void BookshopScene_DoorIsOnTheRightWallNearTheFrontLikeInTown()
        {
            Scene shop = Open("LittleArcadeUpperInterior");
            SpaceDoorway exit = BuilderTools
                .FindAllInScene<SpaceDoorway>(shop)
                .Single(door => door.DoorwayId == "TownDoor");

            Assert.That(exit.transform.position.x, Is.GreaterThan(3.3f), "오른쪽 벽(x≈3.8) 문턱");
            Assert.That(exit.transform.position.z, Is.InRange(-2.2f, -.2f), "앞벽 가까이");
            Assert.That(
                exit.ArrivalPoint.position.x,
                Is.LessThan(exit.transform.position.x - 1.5f),
                "들어오면 오른쪽 벽에서 안쪽(서쪽)으로 2m 들어와 선다."
            );
        }

        // 넓힌 층 사이 판(2층 바닥이자 1층 천장)과 뒷벽 마감은 한 장이라 가운데 이음선이 없다.
        [TestCase("LittleArcadeInterior", 3.9f)]
        [TestCase("LittleArcadeUpperInterior", -.1f)]
        public void ArcadeShopScene_FloorAndBackWallFinishAreOnePiece(
            string sceneName,
            float slabHeight
        )
        {
            Scene shop = Open(sceneName);
            Renderer[] visible = BuilderTools
                .FindAllInScene<Renderer>(shop)
                .Where(part => part.enabled && part.gameObject.activeInHierarchy)
                .ToArray();

            Assert.That(
                visible.Any(slab =>
                    slab.bounds.Contains(new Vector3(-2, slabHeight, -4))
                    && slab.bounds.Contains(new Vector3(2, slabHeight, -4))
                ),
                Is.True,
                "층 사이 판"
            );
            Assert.That(
                visible.Any(wall =>
                    wall.bounds.Contains(new Vector3(-2, 1.5f, -7.785f))
                    && wall.bounds.Contains(new Vector3(2, 1.5f, -7.785f))
                ),
                Is.True,
                "뒷벽 마감"
            );
        }

        // 마을의 매장 문도 장면을 옮기는 문이라 다가가도 열리지 않는다(카페·호텔·경찰서 문과 같다).
        [Test]
        public void TownShopDoors_StayShut()
        {
            string[] swinging = BuilderTools
                .FindAllInScene<ShopSwingDoor>(town)
                .Where(door => door.enabled || door.GetComponent<BoxCollider>().enabled)
                .Select(door => door.name)
                .ToArray();

            Assert.That(swinging, Is.Empty);
        }

        // 오락실 문은 건물 앞면(오락실 쪽)에 있어 인도에서 바로 드나든다. 공용 계단실은 2층 서점으로만 이어진다.
        [Test]
        public void ArcadeDoor_OpensOntoTheSidewalkInFrontOfTheArcade()
        {
            Transform arcade = town.GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/BackgroundBuildings/LittleArcade");
            Vector3 arrival = arcade.InverseTransformPoint(
                BuilderTools
                    .FindAllInScene<SpaceDoorway>(town)
                    .Single(door => door.DestinationScene == "LittleArcadeInterior")
                    .ArrivalPoint.position
            );

            Assert.That(arrival.z, Is.GreaterThan(0), "오락실에서 나오면 건물 앞 인도에 선다.");
            Assert.That(arrival.x, Is.LessThan(0), "계단실 입구(+x)가 아니라 오락실 쪽 앞면이다.");
            Assert.That(
                BuildingParts("LittleArcade", "TenantEntrances"),
                Has.Exactly(1).EqualTo("TenantDoorSign"),
                "계단실 벽에는 2층 서점 문 간판만 남는다."
            );
        }

        // 출근·여가 방문 NPC는 마을에서 실제 매장 문 앞(씬 전환 도착 지점)까지 걸어가 들어가고,
        // 매장 장면에서는 마을 문 안쪽 도착 지점에서 나타나 그곳으로 나간다. 오락실로 확인한다.
        [Test]
        public void ShopVisitsAndCommutesGoThroughTheShopDoor()
        {
            SpaceDoorway townDoor = BuilderTools
                .FindAllInScene<SpaceDoorway>(town)
                .Single(door => door.DestinationScene == "LittleArcadeInterior");
            NpcOutingVenue arcade = AssetDatabase
                .LoadAssetAtPath<NpcOutingCatalog>("Assets/Resources/NpcOutings.asset")
                .Venues.Single(venue => venue.Code == "ARCADE");
            Assert.That(
                Vector3.Distance(arcade.Entrance, townDoor.ArrivalPoint.position),
                Is.LessThan(.05f),
                "여가 방문객은 오락실 문 앞에서 들어간다."
            );
            Assert.That(
                Vector3.Distance(CommuteDoor(town, "ARCADE_STAFF"), townDoor.ArrivalPoint.position),
                Is.LessThan(.05f),
                "출근하는 직원도 오락실 문 앞에서 들어간다."
            );

            Scene shop = Open("LittleArcadeInterior");
            Vector3 inside = BuilderTools
                .FindAllInScene<SpaceDoorway>(shop)
                .Single(door => door.DoorwayId == "TownDoor")
                .ArrivalPoint.position;
            Assert.That(Vector3.Distance(arcade.Inside, inside), Is.LessThan(.05f));
        }

        private static Vector3 CommuteDoor(Scene scene, string npcCode)
        {
            SerializedProperty residents = new SerializedObject(
                BuilderTools.FindSingleInScene<NpcPopulation>(scene)
            ).FindProperty("residents");
            for (int index = 0; index < residents.arraySize; index++)
            {
                SerializedProperty resident = residents.GetArrayElementAtIndex(index);
                var prefab = (GameObject)
                    resident.FindPropertyRelative("TownPrefab").objectReferenceValue;
                if (prefab != null && prefab.GetComponent<NpcIdentity>().Code == npcCode)
                    return resident.FindPropertyRelative("WorkplaceDoor").vector3Value;
            }
            throw new AssertionException(npcCode + "의 출퇴근 연결이 없습니다.");
        }

        private string[] BuildingParts(string building, string group)
        {
            Transform found = town.GetRootGameObjects()
                .Single(root => root.name == "Environment")
                .transform.Find("TownMainstreet/BackgroundBuildings/" + building)
                .Find(group);
            return found == null
                ? new string[0]
                : found.Cast<Transform>().Select(part => part.name).ToArray();
        }

        private static Scene Open(string shop) =>
            EditorSceneManager.OpenScene(ShopFolder + shop + ".unity", OpenSceneMode.Additive);
    }
}
