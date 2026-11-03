using System.Collections;
using A305.Player;
using A305.Tutorial;
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

        [UnityTest]
        public IEnumerator Destination_LightsTheDoorAndLaysTheRouteFromThePlayer()
        {
            yield return LoadTown();

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
        }

        [UnityTest]
        public IEnumerator ReachingTheDoor_RemovesTheRouteAndTheGlow()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            Transform entrance = Entrance("HOTEL");
            Player().Teleport(entrance.position, entrance.rotation);
            yield return null;
            yield return null;

            Assert.That(Route().enabled, Is.False);
            Assert.That(Glow().activeSelf, Is.False);
        }

        // 도착한 뒤 문에 들어가지 않고 돌아서도 같은 목적지를 다시 안내하지 않는다.
        [UnityTest]
        public IEnumerator WalkingAwayAfterArriving_StaysHidden()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return null;
            Transform entrance = Entrance("HOTEL");
            Player().Teleport(entrance.position, entrance.rotation);
            yield return null;

            Player().Teleport(entrance.position + entrance.forward * 8f, entrance.rotation);
            yield return new WaitForSeconds(LaidSeconds);

            Assert.That(Route().enabled, Is.False);
            Assert.That(Glow().activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator NewDestinationAfterArriving_GuidesAgain()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return null;
            Transform hotel = Entrance("HOTEL");
            Player().Teleport(hotel.position, hotel.rotation);
            yield return null;

            GuideDestination.Shared.Show("CAFE");
            yield return new WaitForSeconds(LaidSeconds);

            Assert.That(Route().enabled, Is.True);
            Assert.That(Glow().activeSelf, Is.True);
            Assert.That(
                Flat(Glow().transform.position, Entrance("CAFE").position),
                Is.LessThan(.01f)
            );
        }

        [UnityTest]
        public IEnumerator NewDestination_MovesTheGlowAndTheRoute()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            GuideDestination.Shared.Show("CAFE");
            yield return new WaitForSeconds(LaidSeconds);

            Transform cafe = Entrance("CAFE");
            LineRenderer route = Route();
            Assert.That(Flat(Glow().transform.position, cafe.position), Is.LessThan(.01f));
            Assert.That(
                Flat(route.GetPosition(route.positionCount - 1), cafe.position),
                Is.LessThan(.5f)
            );
        }

        [UnityTest]
        public IEnumerator ClearedDestination_RemovesTheRouteAndTheGlow()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            GuideDestination.Shared.Clear();
            yield return null;

            Assert.That(Route().enabled, Is.False);
            Assert.That(Glow().activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RouteTurnedOff_KeepsOnlyTheGlow()
        {
            yield return LoadTown();
            GuideDestination.Shared.Show("HOTEL");
            yield return new WaitForSeconds(LaidSeconds);

            GuideRoutePreference.ShowsRoute = false;
            yield return null;

            Assert.That(Route().enabled, Is.False);
            Assert.That(Glow().activeSelf, Is.True);
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
