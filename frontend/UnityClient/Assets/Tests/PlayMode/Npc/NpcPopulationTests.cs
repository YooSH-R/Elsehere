using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Player;
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
        [UnityTest]
        public IEnumerator Town_ResidencePavingSupportsFeetAtTheVisibleSurface()
        {
            SetClock(195);
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
            var paving = GameObject.Find("ResidencePaving").GetComponent<Renderer>();
            var failures = new List<string>();
            // 보이는 포장만 있고 충돌면이 없으면 NPC는 아래의 부지 바닥을 딛거나 떨어진다.
            foreach (
                Vector3 point in new[]
                {
                    new Vector3(-18, 0, 16),
                    new Vector3(-18, 0, 21),
                    new Vector3(-25, 0, 16),
                }
            )
            {
                var hits = Physics
                    .RaycastAll(
                        new Vector3(point.x, paving.bounds.max.y + .1f, point.z),
                        Vector3.down,
                        2,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    )
                    .Where(hit => hit.collider.GetComponentInParent<CharacterController>() == null)
                    .OrderBy(hit => hit.distance)
                    .ToArray();
                if (hits.Length == 0 || Mathf.Abs(hits[0].point.y - paving.bounds.max.y) > .06f)
                    failures.Add(
                        $"{point}: 포장 {paving.bounds.max.y:F3}, 충돌면 {(hits.Length == 0 ? "없음" : hits[0].point.y.ToString("F3"))}"
                    );
            }
            Assert.That(failures, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Town_ResidenceDoorWalksToTheRoadAndBackWithoutSinking()
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            var all = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            foreach (var other in all)
                other.gameObject.SetActive(false);
            var walker = all.Single(item =>
                item.GetComponent<NpcIdentity>().Code == "BARBER_STAFF"
            );
            Vector3 door = GameObject.Find("NpcOnlyEntrance").transform.position;
            Vector3 road = new Vector3(-6, .1f, 15.5f);
            PlaceWalker(walker, door, road - door);
            var skin = walker
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(item => item.sharedMesh.vertexCount)
                .First();
            float previousDelta = Time.captureDeltaTime;
#if UNITY_EDITOR
            string output = System.Environment.GetEnvironmentVariable("NPC_OBSTACLE_REVIEW_OUTPUT");
            using var review = new NpcDailyReviewCapture(output);
#endif
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                foreach (Vector3 goal in new[] { road, door })
                {
                    walker.Destination = goal;
                    for (int frame = 0; frame < 750; frame++)
                    {
                        yield return null;
#if UNITY_EDITOR
                        review.RecordScenario(
                            "residence-road-walk",
                            walker.transform,
                            new Vector3(1.5f, 3, -4),
                            GameClock.Shared.Minutes
                        );
#endif
                        if (frame > 15 && frame % 15 == 0)
                        {
                            Assert.That(
                                walker.transform.position.y,
                                Is.InRange(-.05f, .4f),
                                "숙소와 인도 사이의 몸체가 바닥으로 빠집니다."
                            );
                            var baked = new Mesh();
                            skin.BakeMesh(baked);
                            Vector3 sole = skin.transform.TransformPoint(
                                new Vector3(
                                    baked.bounds.center.x,
                                    baked.bounds.min.y,
                                    baked.bounds.center.z
                                )
                            );
                            Object.Destroy(baked);
                            Assert.That(
                                sole.y,
                                Is.GreaterThan(-.15f),
                                $"숙소 보행 중 외형 발 밑면 {sole}"
                            );
                        }
                        if (HorizontalDistance(walker.transform.position, goal) < .25f)
                            break;
                    }
                    Assert.That(
                        HorizontalDistance(walker.transform.position, goal),
                        Is.LessThan(.25f),
                        $"숙소 출입 목표 {goal}, 현재 {walker.transform.position}"
                    );
                }
                Assert.That(
                    walker.StallRecoveryCount,
                    Is.Zero,
                    "위치 복구 없이 숙소와 도로를 왕복해야 합니다."
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
#if UNITY_EDITOR
            review.Dispose();
            using var overview = new NpcDailyReviewCapture(output);
            var focus = new GameObject("ResidenceReviewFocus");
            focus.transform.position = new Vector3(-20, 3, 21);
            for (int frame = 0; frame < 6; frame++)
            {
                overview.RecordScenario(
                    "residence-overview",
                    focus.transform,
                    new Vector3(9, 10, -18),
                    GameClock.Shared.Minutes
                );
                yield return null;
            }
            Object.Destroy(focus);
#endif
        }

        [UnityTest]
        public IEnumerator Town_ScheduledSpawnKeepsTheVisualAboveTheGround()
        {
            SetClock(195);
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
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None)
                .Single(item => item.GetComponent<NpcIdentity>().Code == "BARBER_STAFF");
            var skin = walker
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .OrderByDescending(item => item.sharedMesh.vertexCount)
                .First();
            float previousDelta = Time.captureDeltaTime;
#if UNITY_EDITOR
            using var review = new NpcDailyReviewCapture(
                System.Environment.GetEnvironmentVariable("NPC_OBSTACLE_REVIEW_OUTPUT")
            );
#endif
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                GameClock.Shared.Current.Paused = false;
                for (int frame = 0; frame < 300; frame++)
                {
                    GameClock.Shared.Flow(Time.deltaTime);
                    yield return null;
#if UNITY_EDITOR
                    review.RecordScenario(
                        "residence-scheduled-spawn",
                        walker.transform,
                        new Vector3(1.5f, 3, -4),
                        GameClock.Shared.Minutes
                    );
#endif
                    if (frame % 30 != 0)
                        continue;
                    var baked = new Mesh();
                    skin.BakeMesh(baked);
                    Vector3 center = skin.transform.TransformPoint(baked.bounds.center);
                    Vector3 sole = skin.transform.TransformPoint(
                        new Vector3(
                            baked.bounds.center.x,
                            baked.bounds.min.y,
                            baked.bounds.center.z
                        )
                    );
                    Object.Destroy(baked);
                    Assert.That(
                        HorizontalDistance(center, walker.transform.position),
                        Is.LessThan(1.5f),
                        $"몸체 {walker.transform.position}, 외형 {center}"
                    );
                    Assert.That(
                        sole.y - walker.transform.position.y,
                        Is.GreaterThan(-.25f),
                        $"몸체 {walker.transform.position.y}, 발 밑면 {sole.y}"
                    );
                }
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Town_WorkerKnowsTheRealCommuteAndKeepsItAfterArrival()
        {
            SetClock(17 * 60 + 59);
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            var body = walker.GetComponent<Passerby>();
            Assert.That(body.TravelContext, Is.Not.Null);
            Assert.That(body.TravelContext.Phase, Is.EqualTo("GOING_TO_WORK"));
            Assert.That(body.TravelContext.RouteMeters, Is.GreaterThan(10));
            Assert.That(body.TravelContext.DepartureLeadMinutes, Is.GreaterThan(0));
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                PlaceWalker(walker, new Vector3(12, .1f, 22), Vector3.forward);
                for (int frame = 0; frame < 30; frame++)
                    yield return null;
                Assert.That(body.TravelContext.WalkedMeters, Is.GreaterThan(.1));
                double walked = body.TravelContext.WalkedMeters;
                Vector3 door = walker.ScheduleDoor.Value;
                GameClock
                    .Shared.TickAsync(
                        new FixedClock
                        {
                            Time = new GameClockTime
                            {
                                Day = 1,
                                MinuteOfDay = 18 * 60,
                                Paused = true,
                            },
                        },
                        Time.realtimeSinceStartupAsDouble + 1e6
                    )
                    .GetAwaiter()
                    .GetResult();
                PlaceWalker(walker, door, Vector3.forward);
                yield return null;
                yield return null;
                SceneManager.sceneLoaded += DisableWalkingTestApis;
                try
                {
                    yield return SceneManager.LoadSceneAsync("HotelScene");
                    yield return null;
                }
                finally
                {
                    SceneManager.sceneLoaded -= DisableWalkingTestApis;
                }
                var worker = Object
                    .FindFirstObjectByType<NpcPopulation>()
                    .FindWorker("HOTEL_NIGHT_MANAGER")
                    .GetComponent<Passerby>();
                Assert.That(worker.TravelContext, Is.Not.Null);
                Assert.That(worker.TravelContext.Phase, Is.EqualTo("AT_WORK"));
                Assert.That(worker.TravelContext.WalkedMeters, Is.GreaterThanOrEqualTo(walked));
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Town_StaffPassTheBankStairSupport() => CheckObservedStall(false);

        [UnityTest]
        public IEnumerator Town_CommuterLeavesTheCrosswalkAtTheSidewalkCorner() =>
            CheckObservedStall(false, corner: true);

        [UnityTest]
        public IEnumerator Town_ObservedJunctionCrowdReachesItsDestinations() =>
            CheckObservedStall(true);

        [UnityTest]
        public IEnumerator Town_DiagonalCrossingEndsOnAnActualSidewalk() =>
            CheckObservedStall(false, diagonal: true);

        [UnityTest]
        public IEnumerator Town_CommuterVisualFollowsTheWalkingBody() =>
            CheckObservedStall(false, visual: true);

        [UnityTest]
        public IEnumerator Town_StuckRecoveryFindsFreeGroundBesideAnOccupiedGoal() =>
            CheckObservedStall(false, recover: true);

        private static IEnumerator CheckObservedStall(
            bool crowd,
            bool corner = false,
            bool diagonal = false,
            bool visual = false,
            bool recover = false
        )
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            var lights = Object.FindFirstObjectByType<TrafficLights>();
            lights.ObserveWithoutPedestrianWait = !corner;
            if (corner)
            {
                lights.enabled = false;
                typeof(TrafficLights)
                    .GetField("elapsedSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(lights, 15f);
            }
            var all = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            foreach (var body in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None))
                body.gameObject.SetActive(false);
            string[] codes = crowd
                ? new[]
                {
                    "POLICE_OFFICER",
                    "ARCADE_STAFF",
                    "DINER_STAFF",
                    "BARBER_STAFF",
                    "HOTEL_BELLHOP",
                    "TOWN_EVENING_WALKER",
                }
                : new[] { "BANK_STAFF" };
            Vector3[] starts = crowd
                ? new[]
                {
                    new Vector3(2.595f, .02f, 14.183f),
                    new Vector3(1.878f, .02f, 12.920f),
                    new Vector3(2.971f, .12f, 13.136f),
                    new Vector3(4.311f, .12f, 15.588f),
                    new Vector3(3.103f, .12f, 15.568f),
                    new Vector3(3.69f, .12f, 16.51f),
                }
                : new[]
                {
                    corner ? new Vector3(-2.936f, .114f, 14.58f)
                    : diagonal ? new Vector3(3, .12f, 5.5f)
                    : new Vector3(-5.114f, .12f, 37.574f),
                };
            Vector3[] ends = crowd
                ? new[]
                {
                    new Vector3(-6, .1f, -18),
                    new Vector3(6, .1f, 30),
                    new Vector3(6, .1f, 39),
                    new Vector3(6, .1f, 57),
                    GameObject.Find("NpcOnlyEntrance").transform.position,
                    GameObject.Find("NpcOnlyEntrance").transform.position,
                }
                : new[]
                {
                    corner ? new Vector3(-6, .1f, -18)
                    : diagonal ? GameObject.Find("NpcOnlyEntrance").transform.position
                    : new Vector3(-6, .1f, 48),
                };
            var walkers = codes
                .Select(code => all.Single(item => item.GetComponent<NpcIdentity>().Code == code))
                .ToArray();
            float previousDelta = Time.captureDeltaTime;
            GameObject recoveryBlocker = null;
#if UNITY_EDITOR
            using var review = new NpcDailyReviewCapture(
                System.Environment.GetEnvironmentVariable("NPC_OBSTACLE_REVIEW_OUTPUT")
            );
#endif
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                for (int i = 0; i < walkers.Length; i++)
                {
                    PlaceWalker(walkers[i], starts[i], ends[i] - starts[i]);
                    walkers[i].Destination = ends[i];
                }
                if (recover)
                {
                    recoveryBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    recoveryBlocker.transform.position = ends[0] + Vector3.up;
                    recoveryBlocker.transform.localScale = new Vector3(1.5f, 2, 1.5f);
                    walkers[0].ScheduleDoor = null;
                    Physics.SyncTransforms();
                    bool recovered = (bool)
                        typeof(NpcTownWalker)
                            .GetMethod(
                                "TryRecoverToDestination",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            )
                            .Invoke(walkers[0], null);
                    Assert.That(recovered, Is.True);
                    Assert.That(walkers[0].StallRecoveryCount, Is.EqualTo(1));
                    Vector3 position = walkers[0].transform.position;
                    Assert.That(HorizontalDistance(position, ends[0]), Is.LessThan(3));
                    Assert.That(
                        recoveryBlocker
                            .GetComponent<Collider>()
                            .bounds.Contains(position + Vector3.up),
                        Is.False
                    );
                    Assert.That(
                        NavMesh.SamplePosition(
                            position,
                            out _,
                            .2f,
                            1 << NavMesh.GetAreaFromName("Road")
                        ),
                        Is.False
                    );
                    yield break;
                }
                if (diagonal)
                {
                    // 실제 일과에서 남쪽 횡단 입구로 진입하던 경로의 방향을 그대로 재현한다.
                    typeof(Passerby)
                        .GetMethod("WalkToward", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(
                            walkers[0].GetComponent<Passerby>(),
                            new object[] { new Vector3(-.42f, 0, .91f) * 4, 1.6f, false, null }
                        );
                }
                for (int frame = 0; frame < (crowd || corner || diagonal ? 1800 : 600); frame++)
                {
                    if (corner && frame == 180)
                        typeof(TrafficLights)
                            .GetField(
                                "elapsedSeconds",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            )
                            .SetValue(lights, 0f);
                    yield return null;
#if UNITY_EDITOR
                    if (visual && walkers[0].gameObject.activeSelf)
                        review.RecordScenario(
                            "bank-obstacle",
                            walkers[0].transform,
                            new Vector3(5, 3, -3),
                            GameClock.Shared.Minutes
                        );
#endif
                    if (visual && frame % 30 == 0)
                    {
                        var model = walkers[0]
                            .GetComponentsInChildren<SkinnedMeshRenderer>()
                            .OrderByDescending(skin => skin.sharedMesh.vertexCount)
                            .First();
                        var baked = new Mesh();
                        model.BakeMesh(baked);
                        Bounds meshBounds = baked.bounds;
                        Vector3 center = model.transform.TransformPoint(meshBounds.center);
                        Vector3 sole = model.transform.TransformPoint(
                            new Vector3(meshBounds.center.x, meshBounds.min.y, meshBounds.center.z)
                        );
                        Object.Destroy(baked);
                        Vector3 bodyPosition = walkers[0].transform.position;
                        Assert.That(
                            HorizontalDistance(center, bodyPosition),
                            Is.LessThan(1.5f),
                            $"이동 몸체 {bodyPosition}와 실제 외형 {center}가 떨어져 있습니다."
                        );
                        Assert.That(
                            sole.y - bodyPosition.y,
                            Is.GreaterThan(-.25f),
                            $"발 밑면 {sole.y}이 몸체 발 위치 {bodyPosition.y} 아래로 묻힙니다."
                        );
                    }
                    // 같은 출입구를 쓰는 두 사람도 먼저 도착한 사람이 문 안으로 들어가면 이어서 진입한다.
                    foreach (var walker in walkers)
                        if (
                            walker.gameObject.activeSelf
                            && HorizontalDistance(walker.transform.position, walker.Destination)
                                < .4f
                        )
                            walker.gameObject.SetActive(false);
                }
                var failures = new List<string>();
                foreach (var walker in walkers)
                {
#if UNITY_EDITOR
                    if (walker.gameObject.activeSelf)
                        Debug.Log(
                            NpcDailyObservationRunner.DescribeStall(
                                walker,
                                walkers,
                                GameClock.Shared.Minutes,
                                false
                            )
                        );
#endif
                    if (walker.gameObject.activeSelf)
                        failures.Add(
                            walker.GetComponent<NpcIdentity>().Code
                                + " / "
                                + walker.transform.position
                        );
                }
                Assert.That(failures, Is.Empty, string.Join("; ", failures));
            }
            finally
            {
                if (recoveryBlocker != null)
                    Object.Destroy(recoveryBlocker);
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator Town_CommutersPassOnANarrowSidewalk()
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            var all = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            var first = all.Single(item =>
                item.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER"
            );
            var second = all.Single(item =>
                item.GetComponent<NpcIdentity>().Code == "CAFE_BARISTA"
            );
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(500, -.1f, 500);
            floor.transform.localScale = new Vector3(2.2f, .2f, 10);
            NavMeshData nav = BuildWalkingFloor(floor);
            var instance = NavMesh.AddNavMeshData(nav);
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                PlaceWalker(first, new Vector3(500, .1f, 497), Vector3.forward);
                PlaceWalker(second, new Vector3(500, .1f, 503), Vector3.back);
                first.Destination = new Vector3(500, .1f, 503);
                second.Destination = new Vector3(500, .1f, 497);
                for (int frame = 0; frame < 900; frame++)
                {
                    yield return null;
                    Assert.That(
                        HorizontalDistance(first.transform.position, second.transform.position),
                        Is.GreaterThanOrEqualTo(.95f)
                    );
                }
                Assert.That(
                    HorizontalDistance(first.transform.position, first.Destination),
                    Is.LessThan(.2f)
                );
                Assert.That(
                    HorizontalDistance(second.transform.position, second.Destination),
                    Is.LessThan(.2f)
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
                instance.Remove();
                Object.Destroy(nav);
                Object.Destroy(floor);
            }
        }

        [UnityTest]
        public IEnumerator Town_LateCommuterRunsOnAnEmptyPathAndWalksNearThePlayer() =>
            CheckHurryingNpc(click: false);

        [UnityTest]
        public IEnumerator Town_ClickingAHurryingNpcShowsAnEventAndNeverStartsDialogue() =>
            CheckHurryingNpc(click: true);

        [UnityTest]
        public IEnumerator Town_HurryingHomeStillAllowsConversation() =>
            CheckHurryingNpc(click: true, schedule: NpcDayPhase.GoingHome);

        [UnityTest]
        public IEnumerator Town_BeforeWorkStillAllowsConversation() =>
            CheckHurryingNpc(click: true, schedule: NpcDayPhase.GoingToWork);

        [UnityTest]
        public IEnumerator Town_ReturningToRestStillAllowsConversation() =>
            CheckHurryingNpc(click: true, schedule: NpcDayPhase.Resting);

        [UnityTest]
        public IEnumerator Town_WorkScheduleEnablesRefusalOnlyAfterTheShiftStarts()
        {
            SetClock(17 * 60 + 59);
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None)
                .Single(item => item.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            PlaceWalker(walker, new Vector3(12, .1f, 22), Vector3.forward);
            yield return null;
            Assert.That(walker.SchedulePhase, Is.EqualTo(NpcDayPhase.GoingToWork));
            Assert.That(walker.DeclinesConversation, Is.False);
            var clock = new FixedClock
            {
                Time = new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = 18 * 60,
                    Paused = true,
                },
            };
            GameClock
                .Shared.TickAsync(clock, Time.realtimeSinceStartupAsDouble + 1e6)
                .GetAwaiter()
                .GetResult();
            yield return null;
            yield return null;
            Assert.That(walker.gameObject.activeSelf, Is.True);
            Assert.That(walker.SchedulePhase, Is.EqualTo(NpcDayPhase.Working));
            Assert.That(walker.IsStaff, Is.True);
            Assert.That(
                walker.DeclinesConversation,
                Is.True,
                "근무 시작 뒤 일터에 도착하지 못한 직원만 거절합니다."
            );
        }

        private static IEnumerator CheckHurryingNpc(
            bool click,
            NpcDayPhase schedule = NpcDayPhase.Working
        )
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            var player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(new Vector3(497, .1f, 505), Quaternion.identity);
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(500, -.1f, 500);
            floor.transform.localScale = new Vector3(16, .2f, 12);
            NavMeshData navData = BuildWalkingFloor(floor);
            var instance = NavMesh.AddNavMeshData(navData);
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                PlaceWalker(walker, new Vector3(497, .1f, 500), Vector3.right);
                walker.Destination = new Vector3(503, .1f, 500);
                walker.HasScheduleDeadline = true;
                walker.SchedulePhase = schedule;
                walker.IsStaff = true;
                walker.ScheduleDoor = walker.Destination;
                for (int frame = 0; frame < 8; frame++)
                    yield return null;
                Vector3 before = walker.transform.position;
                if (click)
                {
                    var body = walker.GetComponent<Passerby>();
                    var offer = Object.FindFirstObjectByType<PasserbyOfferView>();
                    var dialogue = Object.FindFirstObjectByType<DialogueController>();
                    Assert.That(body.TalkByClick(NpcClickTalk.ReachMeters), Is.True);
                    yield return RecordConversationReview(walker, schedule);
                    if (schedule != NpcDayPhase.Working)
                    {
                        Assert.That(offer.IsShowing, Is.True);
                        Assert.That(
                            offer.IsEventShowing,
                            Is.False,
                            "귀가·휴식·출근 전 이동은 대화를 거절하지 않습니다."
                        );
                        Assert.That(body.Phase, Is.Not.EqualTo(PasserbyPhase.Talking));
                        offer
                            .GetComponentsInChildren<UnityEngine.UI.Button>(true)
                            .Single(button => button.name == "AcceptButton")
                            .onClick.Invoke();
                        yield return null;
                        Assert.That(
                            dialogue.IsTalking,
                            Is.True,
                            "대화 확인을 수락하면 실제 대화가 시작됩니다."
                        );
                        yield break;
                    }
                    Assert.That(offer.IsEventShowing, Is.True);
                    Assert.That(dialogue.IsTalking, Is.False);
                    Assert.That(player.IsMovementLocked, Is.False);
                    for (int frame = 0; frame < 30; frame++)
                        yield return null;
                    Assert.That(
                        HorizontalDistance(before, walker.transform.position),
                        Is.GreaterThan(.4f)
                    );
                    walker.gameObject.SetActive(false);
                    Assert.That(
                        offer.IsEventShowing,
                        Is.True,
                        "NPC가 문으로 들어가도 사용자가 거절 메시지를 읽을 시간은 남겨야 합니다."
                    );
                    offer
                        .GetComponentsInChildren<UnityEngine.UI.Button>(true)
                        .Single(button => button.name == "AcceptButton")
                        .onClick.Invoke();
                    Assert.That(dialogue.IsTalking, Is.False);
                    Assert.That(offer.IsShowing, Is.False);
                    Assert.That(body.Phase, Is.Not.EqualTo(PasserbyPhase.Talking));
                }
                else
                {
                    Assert.That(walker.IsHurrying, Is.True);
                    Assert.That(walker.IsRunning, Is.True);
                    for (int frame = 0; frame < 30; frame++)
                        yield return null;
                    Assert.That(
                        HorizontalDistance(before, walker.transform.position),
                        Is.InRange(1.1f, 1.32f)
                    );
                    player.Teleport(
                        walker.transform.position + Vector3.forward * 1.5f,
                        Quaternion.identity
                    );
                    before = walker.transform.position;
                    for (int frame = 0; frame < 30; frame++)
                        yield return null;
                    Assert.That(
                        walker.IsHurrying,
                        Is.True,
                        "가까운 사람을 배려해 걸어도 늦은 상태는 이어집니다."
                    );
                    Assert.That(walker.IsRunning, Is.False);
                    Assert.That(
                        HorizontalDistance(before, walker.transform.position),
                        Is.InRange(.65f, .82f)
                    );
                    walker
                        .GetComponent<Passerby>()
                        .WaitAt(new Pose(walker.transform.position, walker.transform.rotation));
                    yield return null;
                    Assert.That(
                        walker.IsHurrying,
                        Is.False,
                        "퀘스트를 기다릴 때는 서두르지 않습니다."
                    );
                    Assert.That(walker.IsRunning, Is.False);
                }
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
                instance.Remove();
                Object.Destroy(navData);
                Object.Destroy(floor);
            }
        }

        // 환경변수를 지정한 검수 실행에서만 실제 Play 프레임을 저장한다. 일정은 위 사례의 고정값이다.
        private static IEnumerator RecordConversationReview(
            NpcTownWalker walker,
            NpcDayPhase schedule
        )
        {
            string output = System.Environment.GetEnvironmentVariable("NPC_CONVERSATION_REVIEW");
            if (string.IsNullOrEmpty(output))
                yield break;
            string folder = Path.Combine(output, schedule.ToString());
            Directory.CreateDirectory(folder);
            var camera = new GameObject("ConversationReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 45;
            camera.nearClipPlane = .03f;
            camera.transform.position = walker.transform.position + new Vector3(-1, 2.6f, -5.5f);
            camera.transform.LookAt(walker.transform.position + new Vector3(1, .8f, 0));
            foreach (Camera other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (other != camera)
                    other.enabled = false;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .2f;
                }
            var target = new RenderTexture(1200, 750, 24);
            var pixels = new Texture2D(1200, 750, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int frame = 0; frame < 180; frame++)
                {
                    yield return null;
                    if (frame % 4 != 0)
                        continue;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(
                        Path.Combine(folder, $"{frame / 4:0000}.png"),
                        pixels.EncodeToPNG()
                    );
                    RenderTexture.active = null;
                }
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.Destroy(pixels);
                Object.Destroy(target);
                Object.Destroy(camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Town_RestingTimeDoesNotHideACommuterBeforeReachingTheDoor()
        {
            SetClock(20 * 60 + 29);
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == "CAFE_SERVER");
            Assert.That(walker.gameObject.activeSelf, Is.True, "준비: 귀가 중인 NPC입니다.");
            PlaceWalker(walker, new Vector3(12, .1f, 22), Vector3.forward);
            Vector3 before = walker.transform.position;
            var clockApi = new FixedClock
            {
                Time = new GameClockTime
                {
                    Day = 1,
                    MinuteOfDay = 20 * 60 + 30,
                    Paused = true,
                },
            };
            GameClock
                .Shared.TickAsync(clockApi, Time.realtimeSinceStartupAsDouble + 1e6)
                .GetAwaiter()
                .GetResult();
            yield return null;
            yield return null;

            Assert.That(
                walker.gameObject.activeSelf,
                Is.True,
                "휴식 시각이 돼도 길 위에서 사라지면 안 됩니다."
            );
            Assert.That(HorizontalDistance(before, walker.transform.position), Is.LessThan(.5f));
            Vector3 door = walker.Destination;
            Assert.That(HorizontalDistance(door, before), Is.GreaterThan(2f));
            PlaceWalker(walker, door, Vector3.forward);
            yield return null;
            yield return null;
            Assert.That(
                walker.gameObject.activeSelf,
                Is.False,
                "실제 출입문에 도착한 뒤 실내로 전환합니다."
            );
        }

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
        public IEnumerator ReviewVisit_OffDutyStaffAppearOnlyForTheVisit()
        {
            SetClock(21 * 60);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            foreach (string code in new[] { "FLOWER_SHOP_STAFF", "BARBER_STAFF" })
            {
                var staff = Object
                    .FindObjectsByType<NpcWorkPresence>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                    .Single(item => item.GetComponent<NpcIdentity>().Code == code);
                Assert.That(staff.gameObject.activeSelf, Is.False, code);
                Assert.That(
                    Object.FindFirstObjectByType<NpcPopulation>().FindWorker(code),
                    Is.EqualTo(staff.GetComponent<NpcIdentity>()),
                    "거리의 같은 이름 대신 매장에 연결된 직원을 찾아야 합니다."
                );
                var room = Object
                    .FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                    .Single(item => item.ContainsStaff(staff.transform));
                using (var visit = new NpcReviewPresence(code))
                {
                    Assert.That(
                        staff.ShouldBeAtWork,
                        Is.True,
                        "복습 요청이 실내 입장에도 적용되어야 합니다."
                    );
                    var door = room.GetComponentsInChildren<ShopDoorway>()
                        .First(item => item.Room == room);
                    Assert.That(
                        door.TryEnter(Object.FindFirstObjectByType<PlayerMover>()),
                        Is.True
                    );
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (
                        (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                        && Time.realtimeSinceStartup < deadline
                    )
                        yield return null;
                    yield return null;
                    Assert.That(room.IsLoaded, Is.True);
                    Assert.That(staff.gameObject.activeInHierarchy, Is.True, code);
                    Assert.That(NpcDuty.MissionNpcCode, Is.Null);
                    Assert.That(GameClock.Shared.Minutes, Is.EqualTo(21 * 60));
                }
                yield return null;
                yield return null;
                Assert.That(
                    staff.gameObject.activeSelf,
                    Is.False,
                    "복습 요청을 해제하면 퇴근 상태로 돌아갑니다."
                );
            }
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
                Does.Contain("TOWN_SHOPPER")
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
            var walkers = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
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
                Vector3 stopped = first.transform.position;
                for (int frame = 0; frame < 60; frame++)
                {
                    yield return null;
                    Assert.That(
                        HorizontalDistance(first.transform.position, stopped),
                        Is.LessThan(.001f),
                        "보행면 자체가 없으면 장애물 우회 경로도 없으므로 길 밖으로 움직이지 않습니다."
                    );
                }
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

        [UnityTest]
        public IEnumerator Town_DiagonalCafeCrossingFollowsTheMainRoadSignalWithoutTurningBack()
        {
            // 카페 앞 횡단보도를 남서쪽 모서리에서 북동쪽으로 비스듬히 건넌다. 연석 모서리까지의 짧은 첫 구간은 남북이지만 건너는 길은 본길이다.
            // 본길 보행 신호만 켜진 동안 출발했으면 가로길 신호를 이유로 되돌아가지 않는다.
            yield return CheckCrossingAtFixedSignal(
                new Vector3(-3.02f, .12f, -13.23f),
                new Vector3(4.4f, .15f, -9.63f),
                signalSeconds: 15.5f,
                expectArrival: true
            );
        }

        [UnityTest]
        public IEnumerator Town_WalkerAtTheJunctionCornerWaitsOnTheSidewalkForTheSignal()
        {
            // 교차로 북서쪽 모서리 인도는 횡단보도에 바로 붙어 있다. 본길 차량 신호가 초록인 동안 인도에서 기다리고 들어갔다 되돌아가지 않는다.
            yield return CheckCrossingAtFixedSignal(
                new Vector3(-3.31f, .12f, 13.19f),
                new Vector3(3.6f, .15f, 14f),
                signalSeconds: 5f,
                expectArrival: false
            );
        }

        [UnityTest]
        public IEnumerator Town_WalkerAtACornerWithTwoCrossingsCommitsToOneAndCrosses()
        {
            // 남동쪽 모서리에서 숙소로 가는 길은 남쪽 본길 횡단과 동쪽 가로길 횡단 둘 다 비슷하다.
            // 몇 cm 움직일 때마다 기다릴 신호를 바꾸면 두 신호의 출발 가능 시간이 겹치지 않아 영영 건너지 못한다.
            yield return LoadTownWithStoppedSignals();
            Vector3 start = new Vector3(3.49f, .12f, 6.46f);
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                var walker = PlaceSergeant(start, new Vector3(-20, .1f, 16.5f));
                bool leftTheCorner = false;
                // 신호 두 주기(50초) 안에 한 번은 건넌다.
                for (int frame = 0; frame < 1500 && !leftTheCorner; frame++)
                {
                    SetSignalSeconds(frame / 30f);
                    yield return null;
                    leftTheCorner = HorizontalDistance(walker.transform.position, start) > 3;
                }
                Assert.That(leftTheCorner, Is.True, walker.transform.position.ToString());
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        private static IEnumerator CheckCrossingAtFixedSignal(
            Vector3 start,
            Vector3 destination,
            float signalSeconds,
            bool expectArrival
        )
        {
            yield return LoadTownWithStoppedSignals();
            SetSignalSeconds(signalSeconds);
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 30f;
                var walker = PlaceSergeant(start, destination);
                var body = walker.GetComponent<Passerby>();
                object crossing = typeof(Passerby)
                    .GetField("crossing", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(body);
                FieldInfo returning = crossing
                    .GetType()
                    .GetField("returning", BindingFlags.Instance | BindingFlags.NonPublic);
                bool arrived = false;
                for (int frame = 0; frame < 300 && !arrived; frame++)
                {
                    yield return null;
                    Assert.That(
                        (bool)returning.GetValue(crossing),
                        Is.False,
                        $"{frame}프레임에 {walker.transform.position}에서 횡단을 되돌아갑니다."
                    );
                    if (!expectArrival)
                        Assert.That(
                            NpcWalkRoute.IsOnSidewalk(walker.transform.position),
                            Is.True,
                            $"{frame}프레임에 신호를 기다리지 않고 {walker.transform.position}의 횡단보도로 들어갑니다."
                        );
                    arrived = HorizontalDistance(walker.transform.position, destination) < .4f;
                }
                if (expectArrival)
                    Assert.That(arrived, Is.True, walker.transform.position.ToString());
                else
                    Assert.That(
                        body.IsWaitingAtSignal,
                        Is.True,
                        walker.transform.position.ToString()
                    );
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        // 마을의 다른 사람과 출퇴근을 멈추고 신호도 멈춘다. 신호 시각은 테스트가 정한다.
        private static IEnumerator LoadTownWithStoppedSignals()
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
            Object.FindFirstObjectByType<TrafficLights>().enabled = false;
            foreach (var body in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None))
                body.gameObject.SetActive(false);
        }

        private static void SetSignalSeconds(float seconds) =>
            typeof(TrafficLights)
                .GetField("elapsedSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Object.FindFirstObjectByType<TrafficLights>(), seconds);

        private static NpcTownWalker PlaceSergeant(Vector3 start, Vector3 destination)
        {
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == "POLICE_SERGEANT");
            PlaceWalker(walker, start, destination - start);
            walker.Destination = destination;
            return walker;
        }

        [UnityTest]
        public IEnumerator Town_CommuterReplansAroundNewObstaclesAndResumesWhenRouteReopens() =>
            CheckNewObstacle(carves: true, height: 2f);

        [UnityTest]
        public IEnumerator Town_CommuterDetoursAroundAPhysicalObstacleMissingFromNavMesh() =>
            CheckNewObstacle(carves: false, height: 2f);

        [UnityTest]
        public IEnumerator Town_CommuterClimbsAWalkableCurbInsteadOfWalkingInPlace() =>
            CheckNewObstacle(carves: false, height: .2f);

        [UnityTest]
        public IEnumerator Town_CommuterPassesAStreetLampThatTheWalkingMapAlreadyAvoids()
        {
            // 길 지도는 카페에서 서쪽 가게로 가는 인도의 가로등(-3.45, 15)을 이미 비켜 가는데,
            // 장애물 우회가 같은 가로등을 길 지도보다 넓게 막혔다고 보고 돌아갈 길을 찾지 못해 그 자리에 섰다.
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(w => w.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            float previousDeltaTime = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                PlaceWalker(walker, new Vector3(-3.03f, .15f, 14.6f), Vector3.forward);
                walker.Destination = new Vector3(-3f, .15f, 15.3f);
                for (int frame = 0; frame < 120; frame++)
                    yield return null;
                Assert.That(
                    HorizontalDistance(walker.transform.position, walker.Destination),
                    Is.LessThan(.12f),
                    "가로등 옆에서 멈추지 않고 지나가야 합니다."
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDeltaTime;
            }
        }

        private static IEnumerator CheckNewObstacle(bool carves, float height)
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(w => w.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(500, -.1f, 500);
            floor.transform.localScale = new Vector3(10, .2f, 8);
            NavMeshData navData = BuildWalkingFloor(floor);
            var navInstance = NavMesh.AddNavMeshData(navData);
            GameObject wall = null;
            float previousDeltaTime = Time.captureDeltaTime;
            const float frameSeconds = 1f / 60f;
            try
            {
                Time.captureDeltaTime = frameSeconds;
                PlaceWalker(walker, new Vector3(497, .1f, 500), Vector3.right);
                walker.Destination = new Vector3(503, 0, 500);
                for (int frame = 0; frame < 12; frame++)
                    yield return null;
                Assert.That(walker.transform.position.x, Is.GreaterThan(497.1f));

                // 이미 걷기 시작한 직선 경로를 막아 새 우회 경로가 필요한 상황을 만든다.
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(500, height / 2, 500);
                wall.transform.localScale = new Vector3(1, height, 3);
                if (carves)
                {
                    var obstacle = wall.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = Vector3.one;
                    obstacle.carveOnlyStationary = false;
                    obstacle.carving = true;
                }
                Physics.SyncTransforms();
                float widestDetour = 0;
                for (int frame = 0; frame < 480; frame++)
                {
                    Vector3 before = walker.transform.position;
                    yield return null;
                    widestDetour = Mathf.Max(
                        widestDetour,
                        Mathf.Abs(walker.transform.position.z - 500)
                    );
                    Assert.That(
                        HorizontalDistance(before, walker.transform.position),
                        Is.LessThanOrEqualTo(1.6f * frameSeconds + .001f),
                        "우회 중에도 순간이동하지 않습니다."
                    );
                    Vector3 position = walker.transform.position;
                    if (height > .3f)
                        Assert.That(
                            Mathf.Abs(position.x - 500) < .5f && Mathf.Abs(position.z - 500) < 1.5f,
                            Is.False,
                            "장애물 내부를 통과하면 안 됩니다."
                        );
                }
                if (height > .3f)
                    Assert.That(
                        widestDetour,
                        Is.GreaterThan(1.5f),
                        "새 장애물을 옆으로 돌아가야 합니다."
                    );
                else
                    Assert.That(
                        widestDetour,
                        Is.LessThan(.2f),
                        "낮은 보도 턱은 돌아가지 않고 올라섭니다."
                    );
                Assert.That(
                    HorizontalDistance(walker.transform.position, walker.Destination),
                    Is.LessThan(.12f)
                );

                if (height < .3f)
                    yield break;

                // 우회로까지 완전히 막힌 동안만 기다리고, 다시 열리면 같은 목적지로 재탐색한다.
                PlaceWalker(walker, new Vector3(497, .1f, 500), Vector3.right);
                wall.transform.localScale = new Vector3(1, 2, 12);
                Physics.SyncTransforms();
                for (int frame = 0; frame < 12; frame++)
                    yield return null;
                walker.Destination = new Vector3(503, 0, 500);
                if (!carves)
                    for (int frame = 0; frame < 120; frame++)
                        yield return null;
                Vector3 waiting = walker.transform.position;
                for (int frame = 0; frame < 60; frame++)
                {
                    yield return null;
                    Assert.That(
                        HorizontalDistance(waiting, walker.transform.position),
                        Is.LessThan(.001f)
                    );
                    Assert.That(
                        walker.GetComponentInChildren<Animator>().GetFloat("Speed"),
                        Is.LessThan(.01f)
                    );
                }
                wall.transform.localScale = new Vector3(1, 2, 3);
                Physics.SyncTransforms();
                for (int frame = 0; frame < 480; frame++)
                    yield return null;
                Assert.That(
                    HorizontalDistance(walker.transform.position, walker.Destination),
                    Is.LessThan(.12f),
                    "통로가 다시 열리면 멈춘 채 남지 않고 우회해 도착해야 합니다."
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDeltaTime;
                navInstance.Remove();
                Object.Destroy(navData);
                Object.Destroy(wall);
                Object.Destroy(floor);
            }
        }

        [UnityTest]
        public IEnumerator Town_CommuterWaitsForTrafficAndFinishesCrossingBeforeTalking() =>
            CheckCrossingSignalChange(.3f, false);

        [UnityTest]
        public IEnumerator Town_CommuterReturnsToDepartureSideWhenSignalChangesEarly() =>
            CheckCrossingSignalChange(.1f, true);

        [UnityTest]
        public IEnumerator Town_CommuterWaitsForThePedestrianHeadWithoutAVehicleHeadNearby() =>
            CheckCrossingSignalChange(.3f, false, pedestrianOnly: true);

        [UnityTest]
        public IEnumerator Town_HurriedCommuterStillWaitsForTheSignalAndWalksAcross() =>
            CheckCrossingSignalChange(.3f, false, hurrying: true);

        [UnityTest]
        public IEnumerator Town_LowFrameRateStillFinishesCrossing() =>
            CheckCrossingSignalChange(.3f, false, frameSeconds: .1f);

        [UnityTest]
        public IEnumerator Town_OpposingCommutersPassWhileCrossing() =>
            CheckCrossingSignalChange(.3f, false, opposing: true);

        [UnityTest]
        public IEnumerator Town_TwoCommutersPassBesideTheSharedSignalPole()
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
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            Object
                .FindFirstObjectByType<A305.Player.PlayerMover>()
                .Teleport(new Vector3(450, 20, 450), Quaternion.identity);
            var walkers = Object.FindObjectsByType<NpcTownWalker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            var first = walkers.Single(w =>
                w.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER"
            );
            var second = walkers.Single(w => w.GetComponent<NpcIdentity>().Code == "CAFE_STAFF");
            foreach (Passerby body in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None))
                if (
                    body != first.GetComponent<Passerby>()
                    && body != second.GetComponent<Passerby>()
                )
                    body.gameObject.SetActive(false);
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                PlaceWalker(first, new Vector3(-4.1f, .1f, 3.5f), Vector3.forward);
                PlaceWalker(second, new Vector3(-4.1f, .1f, 6.1f), Vector3.back);
                for (int frame = 0; frame < 8; frame++)
                    yield return null;
                first.Destination = new Vector3(-4.1f, .1f, 6.1f);
                second.Destination = new Vector3(-4.1f, .1f, 3.5f);
                for (int frame = 0; frame < 420; frame++)
                {
                    yield return null;
                    Assert.That(
                        HorizontalDistance(first.transform.position, second.transform.position),
                        Is.GreaterThan(.95f),
                        "신호 기둥 옆에서도 얼굴을 겹치지 않고 비켜가야 합니다."
                    );
                }
                Assert.That(
                    HorizontalDistance(first.transform.position, first.Destination),
                    Is.LessThan(.15f)
                );
                Assert.That(
                    HorizontalDistance(second.transform.position, second.Destination),
                    Is.LessThan(.15f)
                );
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        private static IEnumerator CheckCrossingSignalChange(
            float changeAtProgress,
            bool returns,
            bool pedestrianOnly = false,
            bool hurrying = false,
            float frameSeconds = 1f / 60f,
            bool opposing = false
        )
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
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(w => w.GetComponent<NpcIdentity>().Code == "HOTEL_NIGHT_MANAGER");
            var lights = Object.FindFirstObjectByType<TrafficLights>();
            NpcTownWalker other = null;
            if (opposing)
            {
                other = Object
                    .FindObjectsByType<NpcTownWalker>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                    .Single(item => item.GetComponent<NpcIdentity>().Code == "CAFE_STAFF");
                foreach (
                    Passerby body in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                )
                    body.gameObject.SetActive(false);
            }
            lights.enabled = false;
            var signal = lights.Heads.First(head => head != null);
            Vector3 signalPosition = signal.transform.position;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var pedestrian = lights.PedestrianHeads.First(head => head.CrossesMainRoad);
            Vector3 pedestrianCrossing = pedestrian.CrossingPoint;
            if (pedestrianOnly)
                typeof(PedestrianSignalHead)
                    .GetField("crossingPoint", flags)
                    .SetValue(pedestrian, new Vector3(500, 0, 500));
            else
                signal.transform.position = new Vector3(500, 0, 502);
            var phase = typeof(TrafficLights).GetField("elapsedSeconds", flags);
            phase.SetValue(lights, 0f);
            Assert.That(lights.CanCross(true, 8), Is.False);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(500, -.1f, 500);
            floor.transform.localScale = new Vector3(10, .2f, 6);
            NavMeshData navData = BuildWalkingFloor(
                floor,
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.Translate(new Vector3(500, 0, 500)),
                    size = new Vector3(2, 4, 6),
                    area = NavMesh.GetAreaFromName("Crosswalk"),
                }
            );
            var navInstance = NavMesh.AddNavMeshData(navData);
            int crosswalk = 1 << NavMesh.GetAreaFromName("Crosswalk");
            Assert.That(
                NavMesh.SamplePosition(
                    new Vector3(500, 0, 500),
                    out NavMeshHit middle,
                    .5f,
                    crosswalk
                ),
                Is.True
            );
            Assert.That(
                NavMesh.Raycast(
                    middle.position,
                    new Vector3(497, 0, 500),
                    out NavMeshHit entrance,
                    crosswalk
                ),
                Is.True
            );
            Assert.That(
                NavMesh.Raycast(
                    middle.position,
                    new Vector3(503, 0, 500),
                    out NavMeshHit exit,
                    crosswalk
                ),
                Is.True
            );
            float previousDeltaTime = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = frameSeconds;
                PlaceWalker(walker, new Vector3(497, .1f, 500), Vector3.right);
                walker.Destination = new Vector3(503, 0, 500);
                if (other != null)
                {
                    PlaceWalker(other, new Vector3(503, .1f, 500), Vector3.left);
                    other.Destination = new Vector3(497, 0, 500);
                }
                walker.HasScheduleDeadline = hurrying;
                walker.ScheduleDoor = walker.Destination;
                for (int frame = 0; frame < 120; frame++)
                    yield return null;
                Assert.That(
                    walker.transform.position.x,
                    Is.InRange(497.2f, 498.9f),
                    "차량이 진행하는 동안 횡단보도 앞에서 기다려야 합니다."
                );
                Vector3 waiting = walker.transform.position;
                for (int frame = 0; frame < 30; frame++)
                {
                    yield return null;
                    Assert.That(
                        HorizontalDistance(waiting, walker.transform.position),
                        Is.LessThan(.001f)
                    );
                }

                phase.SetValue(lights, 15f);
                Assert.That(lights.CanCross(true, 8), Is.True);
                bool crossed = false;
                bool changed = false;
                for (int frame = 0; frame < 300; frame++)
                {
                    yield return null;
                    if (other != null)
                        Assert.That(
                            HorizontalDistance(walker.transform.position, other.transform.position),
                            Is.GreaterThan(.95f),
                            "횡단 중에도 서로 겹치지 않고 지나가야 합니다."
                        );
                    if (!walker.IsCrossing)
                        continue;
                    crossed = true;
                    Assert.That(walker.IsRunning, Is.False, "급해도 횡단보도에서는 걷습니다.");
                    Assert.That(
                        walker.GetComponent<Passerby>().CanTalkByClick(10000),
                        Is.False,
                        "횡단 중에는 대화로 멈추면 안 됩니다."
                    );
                    float progress =
                        (walker.transform.position.x - entrance.position.x)
                        / (exit.position.x - entrance.position.x);
                    if (!changed && progress >= changeAtProgress)
                    {
                        phase.SetValue(lights, 0f);
                        changed = true;
                    }
                }
                Assert.That(crossed, Is.True);
                Assert.That(changed, Is.True);
                if (returns)
                {
                    Assert.That(
                        walker.transform.position.x,
                        Is.InRange(entrance.position.x - 1f, entrance.position.x - .2f),
                        "20% 이하에서 신호가 바뀌면 출발한 인도로 돌아가 기다려야 합니다."
                    );
                    Assert.That(walker.IsCrossing, Is.False);
                }
                else
                    Assert.That(
                        HorizontalDistance(walker.transform.position, walker.Destination),
                        Is.LessThan(.12f),
                        "20%를 넘었으면 횡단 중 신호가 바뀌어도 반대편까지 건너야 합니다."
                    );
                if (other != null)
                    Assert.That(
                        HorizontalDistance(other.transform.position, other.Destination),
                        Is.LessThan(.12f),
                        "반대 방향에서 출발한 사람도 인도에 도착해야 합니다."
                    );
            }
            finally
            {
                Time.captureDeltaTime = previousDeltaTime;
                signal.transform.position = signalPosition;
                typeof(PedestrianSignalHead)
                    .GetField("crossingPoint", flags)
                    .SetValue(pedestrian, pedestrianCrossing);
                lights.enabled = true;
                navInstance.Remove();
                Object.Destroy(navData);
                Object.Destroy(floor);
            }
        }

        private static NavMeshData BuildWalkingFloor(
            GameObject floor,
            params NavMeshBuildSource[] areas
        )
        {
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(
                    floor.transform.position,
                    Quaternion.identity,
                    Vector3.one
                ),
                size = floor.transform.localScale,
                area = 0,
            };
            var sources = new List<NavMeshBuildSource> { source };
            sources.AddRange(areas);
            return NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByIndex(0),
                sources,
                new Bounds(
                    floor.transform.position,
                    floor.transform.localScale + new Vector3(2, 4, 2)
                ),
                Vector3.zero,
                Quaternion.identity
            );
        }

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
            // 시각표와 무관한 수동 보행 준비다. 달리기 검사는 목적지를 정한 뒤 별도로 일정을 켠다.
            walker.HasScheduleDeadline = false;
            walker.ScheduleDoor = null;
            capsule.enabled = true;
            walker.gameObject.SetActive(true);
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
            typeof(DialogueController)
                .GetField("followUpApi", flags)
                .SetValue(Object.FindFirstObjectByType<DialogueController>(), null);
            typeof(SpaceArrival)
                .GetField("apiSettings", flags)
                .SetValue(Object.FindFirstObjectByType<SpaceArrival>(), null);
        }

        [UnityTest]
        public IEnumerator Town_CommuterWalksContinuouslyWithoutBurstingToCatchUp()
        {
            SetClock(18 * 60 + 5);
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
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "CAFE_SERVER");
            var animator = walker.GetComponentInChildren<Animator>();
            var body = walker.GetComponent<CharacterController>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetPositionAndRotation(
                new Vector3(500f, -.5f, 500f),
                Quaternion.identity
            );
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            NavMeshData navData = BuildWalkingFloor(floor);
            var navInstance = NavMesh.AddNavMeshData(navData);
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
                navInstance.Remove();
                Object.Destroy(navData);
                Object.Destroy(floor);
            }
        }

        [UnityTest]
        public IEnumerator Town_OffDutyStaffWalksOutsideAndRestsInsideTheClosedResidence()
        {
            SetClock(18 * 60 + 5);
            yield return SceneManager.LoadSceneAsync("TownScene");
            yield return null;
            var leo = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(npc => npc.GetComponent<NpcIdentity>().Code == "CAFE_SERVER");
            Vector3 before = leo.transform.position;
            Assert.That(leo.IsStaff, Is.True);
            SetClock(18 * 60 + 10);
            for (int i = 0; i < 10; i++)
                yield return null;
            Assert.That(Vector3.Distance(before, leo.transform.position), Is.GreaterThan(.01f));
            SetClock(21 * 60);
            yield return null;
            yield return null;
            Assert.That(leo.gameObject.activeSelf, Is.False);
            Transform residence = GameObject.Find("NpcResidence").transform;
            Assert.That(residence.GetComponentsInChildren<SpaceDoorway>(true), Is.Empty);
            Assert.That(
                residence.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger),
                Is.True
            );
        }

        [UnityTest]
        public IEnumerator ResidentStaff_RemainInTheirWorkplaceAcrossMidnightWithoutTownDuplicates()
        {
            SceneManager.sceneLoaded += DisableWalkingTestApis;
            try
            {
                string[] scenes = { "CafeScene", "HotelScene", "ShopScene" };
                string[] codes = { "CAFE_STAFF", "HOTEL_STAFF", "SHOP_STAFF" };
                for (int index = 0; index < scenes.Length; index++)
                {
                    SetClock(23 * 60 + 59);
                    yield return SceneManager.LoadSceneAsync(scenes[index]);
                    yield return null;
                    foreach (int minute in new[] { 1439, 0, 180, 360, 900, 1080 })
                    {
                        SetClock(minute);
                        yield return null;
                        yield return null;
                        Assert.That(
                            Object
                                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                                .Count(npc => npc.Code == codes[index]),
                            Is.EqualTo(1),
                            codes[index] + " / " + minute
                        );
                    }
                }
                yield return SceneManager.LoadSceneAsync("TownScene");
                yield return null;
                foreach (int minute in new[] { 0, 300, 420, 900, 1080, 1439 })
                {
                    SetClock(minute);
                    yield return null;
                    yield return null;
                    Assert.That(
                        Object
                            .FindObjectsByType<NpcTownWalker>(FindObjectsSortMode.None)
                            .Any(npc => codes.Contains(npc.GetComponent<NpcIdentity>().Code)),
                        Is.False,
                        "상주 직원의 출퇴근 사본이 타운에 중복 등장하면 안 됩니다."
                    );
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= DisableWalkingTestApis;
            }
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
                    NpcOuting visit = NpcOutings.Shared.Find(code);
                    bool isInside =
                        visit != null
                        && visit.Arrived
                        && !visit.Venue.IsOutdoor
                        && !visit.ReadyToLeave;
                    Assert.That(
                        walker.gameObject.activeSelf,
                        Is.EqualTo(
                            NpcDayPlan
                                .At(
                                    data.FindNpc(code),
                                    minutes,
                                    false,
                                    Object
                                        .FindFirstObjectByType<NpcPopulation>()
                                        .CommuteMinutesFor(code)
                                )
                                .IsInTown && !isInside
                        ),
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
