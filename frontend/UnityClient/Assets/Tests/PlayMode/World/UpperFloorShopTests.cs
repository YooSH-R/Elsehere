using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    /// <summary>
    /// 한 건물의 1층과 2층이 다른 가게면(생활용품점·찻집, 오락실·서점) 층마다 실내 씬을 따로 불러온다.
    /// 2층 가게에 들어가도 1층 직원이 켜지지 않아, 바로 아래층 직원이 2층 손님에게 말을 걸지 않는다.
    /// </summary>
    public class UpperFloorShopTests
    {
        [UnitySetUp]
        public IEnumerator LoadTown()
        {
            TutorialState.Clear();
            yield return SceneManager.LoadSceneAsync("TownScene");
            TutorialState.GetOrCreate("TOWN_PASSERBY").Skip();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeaveShops()
        {
            foreach (
                ShopInterior room in Object.FindObjectsByType<ShopInterior>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (room.IsLoaded)
                    yield return SetOccupied(room, false);
            }
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator UpperFloorDoor_LoadsOnlyTheUpperShop_WithoutGroundFloorStaff(
            [Values("HomeGoodsDoor:TeaRoomDoor", "ArcadeDoor:BookshopDoor")] string doors
        )
        {
            string[] names = doors.Split(':');
            ShopInterior ground = Door(names[0]).Room;
            ShopInterior upper = Door(names[1]).Room;
            Assert.That(upper, Is.Not.SameAs(ground), "2층 문은 다른 실내를 연다.");
            Assert.That(
                upper.SceneName,
                Is.EqualTo(ground.SceneName.Replace("Interior", "UpperInterior"))
            );

            yield return SetOccupied(upper, true);

            Assert.That(upper.IsLoaded, Is.True, upper.SceneName);
            Assert.That(ground.IsLoaded, Is.False, ground.SceneName);
            Assert.That(Staff(upper), Is.Empty);
            Assert.That(Staff(ground), Is.Not.Empty);
            Assert.That(
                Staff(ground).Any(npc => npc.activeInHierarchy),
                Is.False,
                "1층 직원이 켜졌습니다."
            );
        }

        // 층마다 그 층의 집기만 담는다. 공간 묶음(539)은 1층·2층이 따로 있다.
        [UnityTest]
        public IEnumerator EachFloorScene_HoldsOnlyItsOwnFurnishings(
            [Values(
                "HomeGoodsDoor:HomeGoodsInterior:TeaRoomInterior",
                "ArcadeDoor:ArcadeInterior:BookshopInterior"
            )]
                string spaces
        )
        {
            string[] names = spaces.Split(':');
            ShopInterior ground = Door(names[0]).Room;
            ShopInterior upper = Object
                .FindObjectsByType<ShopDoorway>(FindObjectsSortMode.None)
                .Select(door => door.Room)
                .Single(room =>
                    room.SceneName == ground.SceneName.Replace("Interior", "UpperInterior")
                );

            yield return SetOccupied(ground, true);
            Assert.That(Holds(ground, names[1]), Is.True, names[1]);
            Assert.That(Holds(ground, names[2]), Is.False, names[2]);
            Assert.That(Staff(ground).All(npc => npc.activeInHierarchy), Is.True, "1층 직원");

            yield return SetOccupied(upper, true);
            Assert.That(Holds(upper, names[2]), Is.True, names[2]);
            Assert.That(Holds(upper, names[1]), Is.False, names[1]);
        }

        private static ShopDoorway Door(string name) =>
            Object
                .FindObjectsByType<ShopDoorway>(FindObjectsSortMode.None)
                .Single(door => door.name == name);

        private static IEnumerator SetOccupied(ShopInterior room, bool occupied) =>
            (IEnumerator)
                typeof(ShopInterior)
                    .GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(room, new object[] { occupied });

        private static GameObject[] Staff(ShopInterior room) =>
            (GameObject[])
                typeof(ShopInterior)
                    .GetField("staff", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(room);

        private static bool Holds(ShopInterior room, string space) =>
            SceneManager
                .GetSceneByName(room.SceneName)
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(part => part.name == space);
    }
}
