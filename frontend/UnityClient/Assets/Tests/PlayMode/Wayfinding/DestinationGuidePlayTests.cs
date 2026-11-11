using System.Collections;
using System.Linq;
using System.Reflection;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Wayfinding.PlayModeTests
{
    /// <summary>
    /// 마을을 실제로 불러와 목적지를 알리면 출입문 앞이 빛나고 플레이어 발밑에서 출입문까지 길이 깔리는지,
    /// 도착하거나 목적지가 사라지면 길과 빛이 거둬지고 사용자가 길을 끄면 길만 거둬지는지 본다.
    /// </summary>
    public sealed class DestinationGuidePlayTests
    {
        // 새 길이 다 깔리기(0.6초)를 넉넉히 기다린다.
        private const float LaidSeconds = 1f;

        private bool savedShowsRoute;

        // 튜토리얼 동안에는 첫날 미션 화면이 꺼져 목적지를 지우므로 튜토리얼을 마친 사용자로 한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            savedShowsRoute = GuideRoutePreference.ShowsRoute;
            GuideRoutePreference.ShowsRoute = true;
        }

        [TearDown]
        public void Restore()
        {
            TutorialState.Clear();
            GuideDestination.Shared.Clear();
            GuideRoutePreference.ShowsRoute = savedShowsRoute;
        }

        [UnityTearDown]
        public IEnumerator LeaveShop()
        {
            if (ShopInterior.Occupied != null)
                yield return SetOccupied(ShopInterior.Occupied, false);
        }

        [UnityTest]
        public IEnumerator OccupiedShop_HidesOutdoorGuide_AndRestoresPendingDestinationOnExit()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);
            Assert.That(Route().enabled, Is.True);

            ShopInterior diner = Object
                .FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                .Single(room => room.PlaceCode == "DINER");
            yield return SetOccupied(diner, true);
            yield return null;
            Assert.That(
                Route().enabled,
                Is.False,
                "목적지가 다른 건물이어도 실내에서는 길을 숨깁니다."
            );
            Assert.That(Glow().activeSelf, Is.False);

            GuideDestination.Shared.Show("CAFE");
            yield return new WaitForSeconds(LaidSeconds);
            Assert.That(
                Route().enabled,
                Is.False,
                "실내에서 퀘스트 목적지가 바뀌어도 길을 숨깁니다."
            );

            yield return SetOccupied(diner, false);
            yield return new WaitForSeconds(LaidSeconds);
            Assert.That(
                Route().enabled,
                Is.True,
                "밖으로 나오면 현재 목적지까지의 길을 다시 표시합니다."
            );
            Assert.That(Glow().activeSelf, Is.True);
            Assert.That(
                Flat(Glow().transform.position, Entrance("CAFE").position),
                Is.LessThan(.01f)
            );

            GuideRoutePreference.ShowsRoute = false;
            yield return SetOccupied(diner, true);
            yield return SetOccupied(diner, false);
            yield return new WaitForSeconds(LaidSeconds);
            Assert.That(
                Route().enabled,
                Is.False,
                "사용자가 꺼 둔 안내선을 입퇴장으로 다시 켜지 않습니다."
            );
        }

        private static IEnumerator SetOccupied(ShopInterior room, bool occupied) =>
            (IEnumerator)
                typeof(ShopInterior)
                    .GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(room, new object[] { occupied });

        // 마을을 불러오는 데 대부분의 시간이 들어, 목적지를 바꿔 가며 한 번 불러온 마을에서 이어서 본다.
        [UnityTest]
        public IEnumerator Destination_LaysTheRoute_MovesWithANewOne_AndClears()
        {
            yield return LoadTown();

            foreach (
                string shop in new[]
                {
                    "DINER",
                    "LAUNDROMAT",
                    "BANK",
                    "BARBER",
                    "FLOWER_SHOP",
                    "BAKERY",
                    "HOME_STORE",
                    "ARCADE",
                    "POST_OFFICE",
                }
            )
                Assert.That(Entrance(shop), Is.Not.Null, shop + " 출입문을 찾지 못했습니다.");

            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            Transform entrance = Entrance("HOTEL");
            LineRenderer route = Route();
            Assert.That(route.enabled, Is.True, "길이 보이지 않습니다.");
            Assert.That(route.positionCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                Flat(route.GetPosition(0), Player().transform.position),
                Is.LessThan(1f),
                "길이 플레이어 발밑에서 시작하지 않습니다."
            );
            Assert.That(
                Flat(route.GetPosition(route.positionCount - 1), entrance.position),
                Is.LessThan(.5f),
                "길이 출입문 앞까지 이어지지 않습니다."
            );
            Assert.That(Glow().activeSelf, Is.True, "출입문 앞이 빛나지 않습니다.");
            Assert.That(Flat(Glow().transform.position, entrance.position), Is.LessThan(.01f));

            GuideDestination.Shared.Show("CAFE");
            yield return new WaitForSeconds(LaidSeconds);

            Transform cafe = Entrance("CAFE");
            Assert.That(
                Flat(Glow().transform.position, cafe.position),
                Is.LessThan(.01f),
                "새 목적지로 빛이 옮겨 가지 않습니다."
            );
            Assert.That(
                Flat(route.GetPosition(route.positionCount - 1), cafe.position),
                Is.LessThan(.5f),
                "새 목적지로 길이 옮겨 가지 않습니다."
            );

            foreach (string shop in new[] { "DINER", "LAUNDROMAT" })
            {
                GuideDestination.Shared.Show(shop);
                yield return new WaitForSeconds(LaidSeconds);
                Assert.That(Glow().activeSelf, Is.True, shop);
                Assert.That(route.enabled, Is.True, shop);
                Assert.That(
                    Flat(Glow().transform.position, Entrance(shop).position),
                    Is.LessThan(.01f),
                    shop
                );
                Assert.That(
                    Flat(route.GetPosition(route.positionCount - 1), Entrance(shop).position),
                    Is.LessThan(.5f),
                    shop
                );
            }

            GuideRoutePreference.ShowsRoute = false;
            yield return null;

            Assert.That(route.enabled, Is.False, "길을 끄면 길은 거둬야 합니다.");
            Assert.That(Glow().activeSelf, Is.True, "길을 꺼도 출입문 빛은 남아야 합니다.");

            GuideDestination.Shared.Clear();
            yield return null;

            Assert.That(route.enabled, Is.False);
            Assert.That(Glow().activeSelf, Is.False, "목적지가 사라지면 빛도 거둬야 합니다.");
        }

        // 도착한 뒤 문에 들어가지 않고 돌아서도 같은 목적지를 다시 안내하지 않고, 새 목적지가 생기면 다시 안내한다.
        [UnityTest]
        public IEnumerator ReachingTheDoor_HidesTheGuide_UntilANewDestination()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            Transform hotel = Entrance("HOTEL");
            Player().Teleport(hotel.position, hotel.rotation);
            yield return null;
            yield return null;

            Assert.That(Route().enabled, Is.False, "도착하면 길을 거둬야 합니다.");
            Assert.That(Glow().activeSelf, Is.False, "도착하면 빛을 거둬야 합니다.");

            Player().Teleport(hotel.position + hotel.forward * 8f, hotel.rotation);
            yield return new WaitForSeconds(LaidSeconds);

            Assert.That(Route().enabled, Is.False, "도착한 목적지를 다시 안내하면 안 됩니다.");
            Assert.That(Glow().activeSelf, Is.False);

            GuideDestination.Shared.Show("CAFE");
            yield return new WaitForSeconds(LaidSeconds);

            Assert.That(Route().enabled, Is.True, "새 목적지는 다시 안내해야 합니다.");
            Assert.That(Glow().activeSelf, Is.True);
            Assert.That(
                Flat(Glow().transform.position, Entrance("CAFE").position),
                Is.LessThan(.01f)
            );
        }

        private static IEnumerator LoadTown()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            // 새 씬의 Start가 실행되도록 한 프레임 넘긴다.
            yield return null;
        }

        private static DestinationGuide Guide() => Object.FindFirstObjectByType<DestinationGuide>();

        private static LineRenderer Route() =>
            Guide().transform.Find("Route").GetComponent<LineRenderer>();

        private static GameObject Glow() => Guide().transform.Find("EntranceGlow").gameObject;

        private static PlayerMover Player() => Object.FindFirstObjectByType<PlayerMover>();

        private static Transform Entrance(string placeCode) =>
            DestinationGuide.FindEntrance(SceneManager.GetActiveScene(), placeCode);

        private static float Flat(Vector3 from, Vector3 to)
        {
            from.y = to.y = 0f;
            return Vector3.Distance(from, to);
        }
    }
}
