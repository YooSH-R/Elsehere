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
        public IEnumerator Town_CommuterReplansAroundNewObstaclesAndResumesWhenRouteReopens() =>
            CheckNewObstacle(carves: true, height: 2f);

        [UnityTest]
        public IEnumerator Town_CommuterDetoursAroundAPhysicalObstacleMissingFromNavMesh() =>
            CheckNewObstacle(carves: false, height: 2f);

        [UnityTest]
        public IEnumerator Town_CommuterClimbsAWalkableCurbInsteadOfWalkingInPlace() =>
            CheckNewObstacle(carves: false, height: .2f);

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
            bool hurrying = false
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
                Time.captureDeltaTime = 1f / 60f;
                PlaceWalker(walker, new Vector3(497, .1f, 500), Vector3.right);
                walker.Destination = new Vector3(503, 0, 500);
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
