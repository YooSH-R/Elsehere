using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
        public IEnumerator Town_CommutersPassWhenThereIsSpaceAndWaitWhenBlocked()
        {
            SetClock(21 * 60);
            SceneManager.sceneLoaded += DisableWalkingTestApis;
            try
            {
                yield return SceneManager.LoadSceneAsync("TownScene");
                yield return null;
            }
            finally
            {
                SceneManager.sceneLoaded -= DisableWalkingTestApis;
            }
            foreach (
                var population in Object.FindObjectsByType<NpcPopulation>(FindObjectsSortMode.None)
            )
                population.enabled = false;
            Object
                .FindFirstObjectByType<A305.Player.PlayerMover>()
                .Teleport(new Vector3(450, 20, 450), Quaternion.identity);
            var walkers = Object.FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None);
            var first = walkers.Single(w =>
                w.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER"
            );
            var second = walkers.Single(w =>
                w.GetComponent<NpcIdentity>().Code == "POLICE_SERGEANT"
            );
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(500, -.1f, 500);
            floor.transform.localScale = new Vector3(10, .2f, 6);
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(
                        floor.transform.position,
                        Quaternion.identity,
                        Vector3.one
                    ),
                    size = floor.transform.localScale,
                    area = 0,
                },
            };
            var settings = NavMesh.GetSettingsByIndex(0);
            var navData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                new Bounds(new Vector3(500, 0, 500), new Vector3(12, 4, 8)),
                Vector3.zero,
                Quaternion.identity
            );
            Assert.That(navData, Is.Not.Null);
            var navInstance = NavMesh.AddNavMeshData(navData);
            float previousDeltaTime = Time.captureDeltaTime;
            const float frameSeconds = 1f / 60f;
            try
            {
                Time.captureDeltaTime = frameSeconds;
                PlaceWalker(first, new Vector3(497, .1f, 500), Vector3.right);
                PlaceWalker(second, new Vector3(503, .1f, 500), Vector3.left);
                for (int frame = 0; frame < 8; frame++)
                    yield return null;
                first.Destination = new Vector3(503, 0, 500);
                second.Destination = new Vector3(497, 0, 500);
                for (int frame = 0; frame < 360; frame++)
                {
                    Vector3 beforeFirst = first.transform.position;
                    Vector3 beforeSecond = second.transform.position;
                    yield return null;
                    Assert.That(
                        HorizontalDistance(first.transform.position, second.transform.position),
                        Is.GreaterThanOrEqualTo(.95f),
                        "마주 지나갈 때 얼굴이 겹치면 안 됩니다."
                    );
                    Assert.That(
                        HorizontalDistance(beforeFirst, first.transform.position),
                        Is.LessThanOrEqualTo(1.6f * frameSeconds + .001f),
                        "순간이동으로 상대를 통과하면 안 됩니다."
                    );
                    Assert.That(
                        HorizontalDistance(beforeSecond, second.transform.position),
                        Is.LessThanOrEqualTo(1.6f * frameSeconds + .001f)
                    );
                }
                Assert.That(
                    HorizontalDistance(first.transform.position, first.Destination),
                    Is.LessThan(.12f),
                    "넓은 길에서는 서로 마주 보고 영구 정지하지 않고 목적지에 도착해야 합니다."
                );
                Assert.That(
                    HorizontalDistance(second.transform.position, second.Destination),
                    Is.LessThan(.12f)
                );

                // 목적지에 서 있는 사람의 주위를 계속 맴돌지 않고 기다린다.
                PlaceWalker(first, new Vector3(497, .1f, 500), Vector3.right);
                PlaceWalker(second, new Vector3(500, .1f, 500), Vector3.left);
                first.Destination = second.transform.position;
                yield return CheckWaiting(first, second);

                // 우회할 NavMesh가 없으면 바닥이 보여도 임의로 길 밖을 걷지 않는다.
                navInstance.Remove();
                navInstance = default;
                PlaceWalker(first, new Vector3(497, .1f, 500), Vector3.right);
                first.Destination = new Vector3(503, 0, 500);
                yield return CheckWaiting(first, second);
            }
            finally
            {
                Time.captureDeltaTime = previousDeltaTime;
                navInstance.Remove();
                Object.Destroy(navData);
                Object.Destroy(floor);
            }
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second) =>
            Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;

        private static IEnumerator CheckWaiting(NpcTownWalker walker, NpcTownWalker obstacle)
        {
            Vector3 settled = Vector3.zero;
            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                Assert.That(
                    HorizontalDistance(walker.transform.position, obstacle.transform.position),
                    Is.GreaterThanOrEqualTo(.95f),
                    "기다리는 동안에도 얼굴 간격을 유지해야 합니다."
                );
                if (frame == 120)
                    settled = walker.transform.position;
                if (frame > 120)
                    Assert.That(
                        HorizontalDistance(walker.transform.position, settled),
                        Is.LessThan(.001f),
                        "목적지나 우회 통로가 막히면 맴돌거나 떨지 않고 기다려야 합니다."
                    );
            }
            Assert.That(walker.transform.position.x, Is.InRange(498f, 499.1f));
            Assert.That(walker.transform.position.z, Is.EqualTo(500f).Within(.001f));
        }

        private static void PlaceWalker(NpcTownWalker walker, Vector3 position, Vector3 forward)
        {
            var capsule = walker.GetComponent<CharacterController>();
            capsule.enabled = false;
            walker.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            walker.Destination = position;
            capsule.enabled = true;
            Physics.SyncTransforms();
        }

        private static void DisableWalkingTestApis(Scene scene, LoadSceneMode mode)
        {
            foreach (
                var clock in Object.FindObjectsByType<GameClockDriver>(FindObjectsSortMode.None)
            )
                clock.enabled = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DialogueController)
                .GetField("conversationApi", flags)
                .SetValue(Object.FindFirstObjectByType<DialogueController>(), null);
            typeof(SpaceArrival)
                .GetField("apiSettings", flags)
                .SetValue(Object.FindFirstObjectByType<SpaceArrival>(), null);
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
