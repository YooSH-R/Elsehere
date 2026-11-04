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
        private const float TimeoutSeconds = 5f;

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

        [UnityTest]
        public IEnumerator WalkingIntoTheClosedPostOffice_IsRefusedAndKeepsItsInteriorUnloaded()
        {
            TestGameClock.Set(8 * 60);
            yield return LoadSpace("TownScene");
            Transform door = GameObject.Find("PostOfficeDoor").transform;
            ShopInterior room = door.GetComponentInParent<ShopInterior>();

            yield return StepIntoThreshold(door, Outward(door));

            Assert.That(SpaceTravel.IsTraveling, Is.False);
            Assert.That(room.IsLoaded, Is.False);
            HudNoticeView notice = Single<HudNoticeView>();
            Assert.That(notice.IsShowing, Is.True);
            Assert.That(
                notice.Message,
                Is.EqualTo("우체국은 지금 영업시간이 아니에요 · 09:00–17:00")
            );
        }

        [UnityTest]
        public IEnumerator StayingInThePostOfficeAtClosingTime_MovesThePlayerOutOfItsDoor()
        {
            TestGameClock.Set(16 * 60 + 50);
            yield return LoadSpace("TownScene");
            Transform door = GameObject.Find("PostOfficeDoor").transform;
            ShopInterior room = door.GetComponentInParent<ShopInterior>();
            Vector3 outward = Outward(door);
            yield return StepIntoThreshold(door, outward);
            yield return WaitUntilTravelEnds();
            Assert.That(room.IsLoaded, Is.True, "영업 중에는 매장에 들어갑니다.");
            HudNoticeView notice = Single<HudNoticeView>();

            TestGameClock.Set(17 * 60);
            yield return null;
            Assert.That(notice.Message, Is.EqualTo("영업이 끝났어요. 30분 후 마을로 이동합니다"));

            TestGameClock.Set(17 * 60 + 30);
            yield return WaitUntilTravelStarts();
            yield return WaitUntilTravelEnds();

            Assert.That(room.IsLoaded, Is.False);
            Vector3 fromThreshold = Single<PlayerMover>().transform.position - Threshold(door);
            Assert.That(Vector3.Dot(fromThreshold, outward), Is.GreaterThan(0.4f));
            Assert.That(notice.IsShowing, Is.False);
        }

        // 매장 문은 문턱 앞뒤로 감지 영역이 있다. 문턱 바깥쪽 감지 영역 안에 세우면 문에 닿는다.
        // 앞선 이동의 화면 덮개가 걷히는 중에는 매장 문이 반응하지 않으므로 다 걷힌 뒤에 세운다.
        private static IEnumerator StepIntoThreshold(Transform door, Vector3 outward)
        {
            yield return WaitUntilTravelEnds();
            Vector3 position = Threshold(door) + outward * 0.45f;
            position.y = door.position.y;
            Single<PlayerMover>().Teleport(position, Quaternion.LookRotation(-outward));
            yield return WaitForFixedUpdates(DoorTouchFixedUpdates);
        }

        private static Vector3 Threshold(Transform door)
        {
            Vector3 center = door.TransformPoint(door.GetComponent<BoxCollider>().center);
            center.y = door.position.y;
            return center;
        }

        // 우체국 문은 문의 앞쪽(+z)이 거리다.
        private static Vector3 Outward(Transform door) => door.forward;

        private static IEnumerator LoadSpace(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // 새 씬의 Start가 실행되도록 한 프레임 넘긴다.
            yield return null;
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
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
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

        private static IEnumerator WaitUntilTravelStarts()
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!SpaceTravel.IsTraveling)
            {
                Assert.That(
                    Time.realtimeSinceStartup,
                    Is.LessThan(deadline),
                    "영업이 끝난 매장에서 나가지 않았습니다."
                );
                yield return null;
            }
        }

        private static IEnumerator WaitUntilTravelEnds()
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (SpaceTravel.IsTraveling || CurtainOpacity() > 0f)
            {
                Assert.That(
                    Time.realtimeSinceStartup,
                    Is.LessThan(deadline),
                    "이동이 끝나지 않았습니다."
                );
                yield return null;
            }
        }

        private static float CurtainOpacity()
        {
            var curtain = Object.FindFirstObjectByType<SpaceTravelCurtain>();
            return curtain != null ? curtain.Opacity : 0f;
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
