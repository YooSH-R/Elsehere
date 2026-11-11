using System.Collections;
using System.Linq;
using A305.Player;
using A305.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    /// <summary>
    /// 배포용 씬에서 영업시간이 아닌 장소의 문이 열리지 않는지, 머무는 장소의 영업이 끝나면 남은 시간을 알린 뒤 마을로 옮기는지 확인한다.
    /// 게임 시각은 멈춘 채로 맞추고, 남은 시간이 줄어드는 것은 시각을 다시 맞춰 확인한다.
    /// </summary>
    public class OpeningHoursTests
    {
        private const float SceneArrivalTimeoutSeconds = 30f;

        // 문 감지 영역에 닿고 거절 안내가 뜨기까지 넉넉히 기다리는 물리 갱신 수
        private const int DoorTouchFixedUpdates = 10;

        // 이동을 막는 튜토리얼 잠금과 구분하려고 튜토리얼을 마친 사용자로 한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [TearDown]
        public void RestoreClockAndTutorial()
        {
            TestGameClock.Restore();
            TutorialState.Clear();
            OpeningHours.Use(PlaceHours.AlwaysOpen);
        }

        [UnityTest]
        public IEnumerator ClosedCafe_TurnsOffItsLights_AndLightsAgainWhenOpen()
        {
            TestGameClock.Set(22 * 60);
            yield return LoadSpace("TownScene");
            Transform building = FindDoorway("CafeDoor").transform.parent;
            Light[] lights = building
                .GetComponentsInChildren<Light>(true)
                .Where(light => light.type != LightType.Directional)
                .ToArray();
            Assert.That(lights, Is.Not.Empty, "카페 외관의 실제 조명을 확인합니다.");
            yield return null;
            Assert.That(lights.All(light => !light.enabled || light.intensity == 0), Is.True);

            TestGameClock.Set(6 * 60);
            yield return null;
            yield return null;
            Assert.That(lights.All(light => !light.enabled || light.intensity == 0), Is.True);

            TestGameClock.Set(20 * 60);
            yield return null;
            yield return null;
            Assert.That(lights.Any(light => light.enabled && light.intensity > 0), Is.True);
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheClosedCafe_IsRefusedAndKeepsThePlayerInTown()
        {
            TestGameClock.Set(22 * 60);
            yield return LoadSpace("TownScene");
            Transform cafeDoor = FindDoorway("CafeDoor").transform;
            PlayerMover player = Single<PlayerMover>();

            // 문 앞 감지 영역 안에 세우면 다음 물리 갱신에서 문에 닿는다.
            player.Teleport(
                cafeDoor.position + cafeDoor.forward * 0.4f,
                Quaternion.LookRotation(-cafeDoor.forward)
            );
            yield return WaitForFixedUpdates(DoorTouchFixedUpdates);

            Assert.That(SpaceTravel.IsTraveling, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TownScene"));
            Assert.That(player.IsMovementLocked, Is.False);
            HudNoticeView notice = Single<HudNoticeView>();
            Assert.That(notice.IsShowing, Is.True);
            Assert.That(
                notice.Message,
                Is.EqualTo("카페는 지금 영업시간이 아니에요 · 07:00–21:00")
            );
        }

        [UnityTest]
        public IEnumerator StayingInTheCafeAtClosingTime_CountsDownThenMovesThePlayerToTown()
        {
            TestGameClock.Set(20 * 60 + 50);
            yield return LoadSpace("CafeScene");
            HudNoticeView notice = Single<HudNoticeView>();
            Assert.That(notice.IsShowing, Is.False, "영업 중에는 안내가 없습니다.");

            TestGameClock.Set(21 * 60);
            yield return null;
            Assert.That(notice.IsShowing, Is.True);
            Assert.That(notice.Message, Is.EqualTo("영업이 끝났어요. 30분 후 마을로 이동합니다"));

            TestGameClock.Set(21 * 60 + 10);
            yield return null;
            Assert.That(notice.Message, Is.EqualTo("영업이 끝났어요. 20분 후 마을로 이동합니다"));
            Assert.That(SpaceTravel.IsTraveling, Is.False);

            TestGameClock.Set(21 * 60 + 30);
            yield return WaitForArrival("TownScene");

            Transform cafeDoor = FindDoorway("CafeDoor").ArrivalPoint;
            Vector3 offset = Single<PlayerMover>().transform.position - cafeDoor.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(0.05f));
            Assert.That(Single<HudNoticeView>().IsShowing, Is.False);
        }

        // 마을 매장도 카페처럼 따로 장면이다. 닫힌 매장 문에 닿으면 들어가지 않고 마을에 남는다.
        [UnityTest]
        public IEnumerator WalkingIntoTheClosedPostOffice_IsRefusedAndKeepsThePlayerInTown()
        {
            TestGameClock.Set(8 * 60);
            yield return LoadSpace("TownScene");
            Transform door = FindDoorway("PostOfficeDoor").transform;
            PlayerMover player = Single<PlayerMover>();

            player.Teleport(
                door.position + door.forward * 0.4f,
                Quaternion.LookRotation(-door.forward)
            );
            yield return WaitForFixedUpdates(DoorTouchFixedUpdates);

            Assert.That(SpaceTravel.IsTraveling, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TownScene"));
            Assert.That(SceneManager.GetSceneByName("PostOfficeInterior").isLoaded, Is.False);
            HudNoticeView notice = Single<HudNoticeView>();
            Assert.That(notice.IsShowing, Is.True);
            Assert.That(
                notice.Message,
                Is.EqualTo("우체국은 지금 영업시간이 아니에요 · 09:00–17:00")
            );
        }

        [UnityTest]
        public IEnumerator StayingInThePostOfficeAtClosingTime_MovesThePlayerOutOfItsTownDoor()
        {
            TestGameClock.Set(16 * 60 + 50);
            yield return LoadSpace("PostOfficeInterior");
            HudNoticeView notice = Single<HudNoticeView>();
            Assert.That(notice.IsShowing, Is.False, "영업 중에는 안내가 없습니다.");

            TestGameClock.Set(17 * 60);
            yield return null;
            Assert.That(notice.Message, Is.EqualTo("영업이 끝났어요. 30분 후 마을로 이동합니다"));

            TestGameClock.Set(17 * 60 + 30);
            yield return WaitForArrival("TownScene");

            Transform townDoor = FindDoorway("PostOfficeDoor").ArrivalPoint;
            Vector3 offset = Single<PlayerMover>().transform.position - townDoor.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(0.05f));
            Assert.That(Single<HudNoticeView>().IsShowing, Is.False);
        }

        private static IEnumerator LoadSpace(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // 새 씬의 Start가 실행되도록 한 프레임 넘긴다.
            yield return null;
            // 운영 데이터는 24시간 영업이다. 닫힘·마감 처리는 별도의 시간표로 검증한다.
            OpeningHours.Use(
                PlaceHours.FromJson(
                    "{\"places\":["
                        + "{\"code\":\"CAFE\",\"alwaysOpen\":false,\"from\":\"07:00\",\"to\":\"21:00\"},"
                        + "{\"code\":\"POST_OFFICE\",\"alwaysOpen\":false,\"from\":\"09:00\",\"to\":\"17:00\"}]}"
                )
            );
        }

        private static IEnumerator WaitForFixedUpdates(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            yield return null;
        }

        private static IEnumerator WaitForArrival(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + SceneArrivalTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName || SpaceTravel.IsTraveling)
            {
                Assert.That(
                    Time.realtimeSinceStartup,
                    Is.LessThan(deadline),
                    $"{sceneName}에 도착하지 못했습니다."
                );
                yield return null;
            }
            yield return null;
        }

        private static SpaceDoorway FindDoorway(string doorwayId)
        {
            foreach (
                SpaceDoorway doorway in Object.FindObjectsByType<SpaceDoorway>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (doorway.DoorwayId == doorwayId)
                {
                    return doorway;
                }
            }

            Assert.Fail($"{doorwayId} 출입구가 없습니다.");
            return null;
        }

        private static T Single<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name);
            return found[0];
        }
    }
}
