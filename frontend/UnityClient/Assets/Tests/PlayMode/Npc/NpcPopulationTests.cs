using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Npc.PlayModeTests
{
    public class NpcPopulationTests
    {
        [SetUp]
        public void SetUp()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            NpcDuty.MissionNpcCode = null;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialState.Clear();
            NpcDuty.MissionNpcCode = null;
            SetClock(8 * 60);
        }

        [UnityTest]
        public IEnumerator DialogueDestroyedDuringSceneTeardown_DoesNotWakeUnconnectedCommuters()
        {
            SetClock(12 * 60 + 15);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            var officer = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "POLICE_OFFICER");
            Assert.That(
                officer.gameObject.activeSelf,
                Is.False,
                "근무 중인 경찰관의 출퇴근 모습은 아직 켜지지 않습니다."
            );

            Object.DestroyImmediate(Object.FindFirstObjectByType<DialogueController>());
            SetClock(15 * 60);
            yield return null;
            yield return null;

            Assert.That(
                officer.gameObject.activeSelf,
                Is.False,
                "장면 종료 중에는 연결이 끊긴 NPC를 새로 깨우지 않습니다."
            );
        }

        [UnityTest]
        public IEnumerator OfferViewDestroyedDuringSceneTeardown_DoesNotWakeUnconnectedCommuters()
        {
            SetClock(12 * 60 + 15);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            var officer = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "POLICE_OFFICER");
            Assert.That(officer.gameObject.activeSelf, Is.False);

            Object.DestroyImmediate(Object.FindFirstObjectByType<PasserbyOfferView>());
            SetClock(15 * 60);
            yield return null;
            yield return null;

            Assert.That(officer.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Hotel_OnlyTheCurrentShiftIsVisible()
        {
            SetClock(17 * 60);
            yield return SceneManager.LoadSceneAsync("HotelScene");
            yield return null;
            var data = NpcLifeData.Parse(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json"))
            );
            var present = Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .Where(npc => npc.Code.StartsWith("HOTEL_"))
                .Select(npc => npc.Code)
                .ToArray();
            var expected = data
                .Npcs.Where(npc => npc.Workplace == "HOTEL" && npc.ActivityAt(17 * 60) == "WORK")
                .Select(npc => npc.NpcId)
                .ToArray();
            Assert.That(expected, Has.Length.EqualTo(3));
            Assert.That(present, Is.EquivalentTo(expected));
        }

        [UnityTest]
        public IEnumerator Town_ShiftStartKeepsCommutersApart()
        {
            SetClock(21 * 60);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            foreach (
                var clock in Object.FindObjectsByType<GameClockDriver>(FindObjectsSortMode.None)
            )
                clock.enabled = false;
            var walkers = Object.FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None);
            Assert.That(
                walkers.Select(w => w.GetComponent<NpcIdentity>().Code),
                Does.Contain("HOTEL_NIGHT_MANAGER")
            );
            Assert.That(
                walkers.Select(w => w.GetComponent<NpcIdentity>().Code),
                Does.Contain("POLICE_SERGEANT")
            );
            for (int frame = 0; frame < 30; frame++)
            {
                foreach (var first in walkers)
                foreach (var second in walkers)
                {
                    if (first.GetInstanceID() >= second.GetInstanceID())
                        continue;
                    float distance = Vector3
                        .ProjectOnPlane(
                            first.transform.position - second.transform.position,
                            Vector3.up
                        )
                        .magnitude;
                    Assert.That(
                        distance,
                        Is.GreaterThanOrEqualTo(.95f),
                        $"출근 위치에서 얼굴과 몸이 겹치면 안 됩니다: {first.GetComponent<NpcIdentity>().Code} / {second.GetComponent<NpcIdentity>().Code}, frame {frame}"
                    );
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Town_CommuterWalksContinuouslyWithoutBurstingToCatchUp()
        {
            SetClock(15 * 60 + 5);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            foreach (
                var population in Object.FindObjectsByType<NpcPopulation>(FindObjectsSortMode.None)
            )
                population.enabled = false;
            foreach (
                var clock in Object.FindObjectsByType<GameClockDriver>(FindObjectsSortMode.None)
            )
                clock.enabled = false;
            Object
                .FindFirstObjectByType<A305.Player.PlayerMover>()
                .Teleport(new Vector3(450f, 20f, 450f), Quaternion.identity);
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None)
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "CAFE_STAFF");
            var animator = walker.GetComponentInChildren<Animator>();
            var body = walker.GetComponent<CharacterController>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(
                new Vector3(500f, -.5f, 500f),
                Quaternion.identity
            );
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            float captureDeltaTime = Time.captureDeltaTime;
            const float frameSeconds = 1f / 60f;
            const float stepMeters = .01f;
            const float maximumWalkSpeedMetersPerSecond = 1.6f;
            try
            {
                Time.captureDeltaTime = frameSeconds;
                body.enabled = false;
                walker.transform.SetPositionAndRotation(
                    new Vector3(500f, .1f, 500f),
                    Quaternion.Euler(0f, 90f, 0f)
                );
                body.enabled = true;
                walker.Destination = walker.transform.position;
                Physics.SyncTransforms();
                for (int frame = 0; frame < 8; frame++)
                    yield return null;

                Vector3 destination = walker.transform.position;
                for (int frame = 0; frame < 24; frame++)
                {
                    Vector3 before = walker.transform.position;
                    destination += Vector3.right * stepMeters;
                    walker.Destination = destination;
                    yield return null;
                    float moved = Vector3
                        .ProjectOnPlane(walker.transform.position - before, Vector3.up)
                        .magnitude;
                    Assert.That(
                        moved,
                        Is.InRange(stepMeters * .7f, stepMeters * 1.3f),
                        $"연속 경로에서 멈췄다가 몰아서 움직이면 안 됩니다. 프레임 {frame}, 이동 {moved}"
                    );
                    Assert.That(
                        animator.GetFloat("Speed"),
                        Is.InRange(.4f, .8f),
                        "일정하게 걷는 동안 Idle과 빠른 이동을 번갈아 전달하면 안 됩니다."
                    );
                }

                // 시계 동기화 등으로 목표가 멀어져도 한 프레임에 순간이동하지 않는다.
                Vector3 beforeCatchUp = walker.transform.position;
                walker.Destination = beforeCatchUp + Vector3.right * 5f;
                yield return null;
                float catchUpStep = Vector3
                    .ProjectOnPlane(walker.transform.position - beforeCatchUp, Vector3.up)
                    .magnitude;
                Assert.That(catchUpStep, Is.GreaterThan(.001f));
                Assert.That(
                    catchUpStep,
                    Is.LessThanOrEqualTo(maximumWalkSpeedMetersPerSecond * frameSeconds + .001f)
                );
                Assert.That(
                    animator.GetFloat("Speed"),
                    Is.LessThanOrEqualTo(maximumWalkSpeedMetersPerSecond)
                );

                walker.Destination = walker.transform.position;
                yield return null;
                Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f));
            }
            finally
            {
                Time.captureDeltaTime = captureDeltaTime;
                Object.Destroy(floor);
            }
        }

        [UnityTest]
        public IEnumerator Town_OffDutyStaffWalksOutsideAndRestsInsideTheClosedResidence()
        {
            SetClock(15 * 60 + 5);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            var mia = Object
                .FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None)
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "CAFE_STAFF");
            Vector3 before = mia.transform.position;
            Assert.That(mia.IsStaff, Is.True);
            SetClock(15 * 60 + 10);
            for (int i = 0; i < 10; i++)
                yield return null;
            Assert.That(Vector3.Distance(before, mia.transform.position), Is.GreaterThan(.01f));
            SetClock(21 * 60);
            yield return null;
            yield return null;
            Assert.That(mia.gameObject.activeSelf, Is.False);
            Transform residence = GameObject.Find("NpcResidence").transform;
            Assert.That(residence.GetComponentsInChildren<SpaceDoorway>(true), Is.Empty);
            Assert.That(
                residence.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger),
                Is.True
            );
        }

        [UnityTest]
        public IEnumerator Town_EveryScheduledCommuterHasAReachableRouteAcrossTheDay()
        {
            SetClock(0);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            Object
                .FindFirstObjectByType<A305.Player.PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            var data = NpcLifeData.Parse(
                File.ReadAllText(Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json"))
            );
            var walkers = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            Assert.That(walkers, Has.Length.EqualTo(data.Npcs.Count - 1));
            for (int minutes = 0; minutes < 1440; minutes += 30)
            {
                SetClock(minutes);
                yield return null;
                yield return null;
                foreach (var walker in walkers)
                {
                    string code = walker.GetComponent<NpcIdentity>().Code;
                    Assert.That(
                        walker.gameObject.activeSelf,
                        Is.EqualTo(NpcDayPlan.At(data.FindNpc(code), minutes, false).IsInTown),
                        code + " / " + minutes
                    );
                }
            }
        }

        private static void SetClock(int minutes)
        {
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            var api = new FixedClock
            {
                Time = new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = minutes,
                    Paused = true,
                },
            };
            Assert.That(
                GameClock
                    .Shared.TickAsync(api, UnityEngine.Time.realtimeSinceStartupAsDouble + 1e5)
                    .Result,
                Is.EqualTo(GameClockBeatStatus.Updated)
            );
        }

        private sealed class FixedClock : IGameClockApi
        {
            public GameClockTime Time;

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) => Task.FromResult(Time);
        }
    }
}
