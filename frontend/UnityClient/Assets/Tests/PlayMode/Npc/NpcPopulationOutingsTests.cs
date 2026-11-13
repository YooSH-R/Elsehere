using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Npc.PlayModeTests
{
    public class NpcPopulationOutingsTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator OffDutyResidentsReserveDifferentFacilitiesAndStreetWalks()
        {
            yield return LoadTown(6 * 60 + 45);
            var trips = NpcOutings.Shared.Trips.ToArray();
            // 24시간 상주 직원 대신 밤 근무를 마친 교대 직원들의 여가를 확인한다.
            Assert.That(
                trips.Any(trip => trip.Npc == "BARBER_STAFF" || trip.Npc == "POST_OFFICE_STAFF"),
                Is.False
            );
            Assert.That(trips.Length, Is.GreaterThan(4));
            Assert.That(
                trips.Select(trip => trip.Venue.Code).Distinct().Count(),
                Is.GreaterThanOrEqualTo(4)
            );
            Assert.That(trips.Any(trip => trip.Venue.IsOutdoor), Is.True);
            Assert.That(trips.Any(trip => !trip.Venue.IsOutdoor), Is.True);
            foreach (var group in trips.GroupBy(trip => trip.Venue.Code))
                Assert.That(group.Count(), Is.LessThanOrEqualTo(2), group.Key);
        }

        [UnityTest]
        public IEnumerator AResidentEntersOnlyAfterReachingTheFacilityDoor()
        {
            yield return LoadTown();
            NpcOuting trip = NpcOutings.Shared.Trips.FirstOrDefault(item => !item.Venue.IsOutdoor);
            Assert.That(trip, Is.Not.Null);
            var walker = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == trip.Npc);
            trip.Arrived = false;
            Place(walker, new Vector3(4.4f, .1f, -5));
            yield return null;
            Assert.That(
                walker.gameObject.activeSelf,
                Is.True,
                "도착 예정 시각만으로 길 위에서 사라지면 안 됩니다."
            );
            Assert.That(trip.Arrived, Is.False);
            Place(walker, trip.Venue.Entrance);
            yield return null;
            yield return null;
            Assert.That(trip.Arrived, Is.True);
            Assert.That(walker.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ABankGuestUsesTheRoomAsACustomerAndWalksToTheExit()
        {
            // 은행은 마을 매장 실내 장면이다. 마을이 내려간 동안에도 들어와 있는 방문객이 은행 안에 있다.
            yield return LoadScene("TownBankInterior");
            foreach (NpcOuting existing in NpcOutings.Shared.Trips.ToArray())
                NpcOutings.Shared.Release(existing.Npc);
            var catalog = Resources.Load<NpcOutingCatalog>("NpcOutings");
            NpcOutingVenue bank = catalog.Venues.Single(venue => venue.Code == "BANK");
            var trip = NpcOutings.Shared.Reserve(
                "ARCADE_STAFF",
                "ARCADE",
                1125,
                1200,
                new[] { bank },
                _ => true
            );
            trip.Arrived = true;
            Transform room = GameObject.Find("TownBank").transform;
            yield return null;
            yield return null;
            var guest = Object
                .FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.name == "ARCADE_STAFFVisitor");
            Assert.That(guest, Is.Not.Null);
            Assert.That(guest.gameObject.scene.name, Is.EqualTo(bank.Scene));
            Assert.That(guest.IsOffDuty, Is.True);
            Vector3 before = guest.transform.position;
            float previousDelta = Time.captureDeltaTime;
#if UNITY_EDITOR
            using var review = new NpcDailyReviewCapture(
                System.Environment.GetEnvironmentVariable("NPC_FACILITY_REVIEW_OUTPUT")
            );
#endif
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                for (int frame = 0; frame < 180; frame++)
                {
                    yield return null;
#if UNITY_EDITOR
                    review.RecordScenario(
                        "facility-interior",
                        guest.transform,
                        room.transform.TransformDirection(new Vector3(0, 2, -3)),
                        GameClock.Shared.Minutes
                    );
#endif
                }
                Assert.That(
                    Vector3.Distance(before, guest.transform.position) > .3f || guest.IsSeated,
                    Is.True,
                    "실내 사물을 둘러보거나 자리를 이용해야 합니다."
                );
                SetClock(1201, reset: false);
                for (int frame = 0; frame < 600 && !trip.ReadyToLeave; frame++)
                {
                    yield return null;
#if UNITY_EDITOR
                    if (guest != null)
                        review.RecordScenario(
                            "facility-interior",
                            guest.transform,
                            room.transform.TransformDirection(new Vector3(0, 2, -3)),
                            GameClock.Shared.Minutes
                        );
#endif
                }
                Assert.That(trip.ReadyToLeave, Is.True, "문으로 걸어 나온 뒤 거리로 돌아갑니다.");
                yield return null;
                Assert.That(guest == null, Is.True);
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
        }

        [UnityTest]
        public IEnumerator APasserbyLeavesTheirSpotForAShopAndComesBackToIt()
        {
            // 할아버지의 자유 시간은 06~13시: 07:00~07:45는 가게에 들르고, 07:45~08:00에 제자리로 돌아온다.
            const string code = "TOWN_GRANDFATHER";
            yield return LoadTown(418);
            GameObject worker = Object
                .FindFirstObjectByType<NpcPopulation>()
                .FindWorker(code)
                .gameObject;
            NpcTownWalker walker = TownBodyOf(code);
            Assert.That(worker.activeSelf, Is.True, "07시 전에는 제자리 근처를 걷습니다.");
            Assert.That(walker.gameObject.activeSelf, Is.False);

            SetClock(420, reset: false);
            yield return null;
            yield return null;
            Vector3 spot = worker.transform.position;
            NpcOuting trip = NpcOutings.Shared.Find(code);
            Assert.That(worker.activeSelf, Is.False);
            Assert.That(walker.gameObject.activeSelf, Is.True);
            Assert.That(
                Flat(walker.transform.position - spot),
                Is.LessThan(1.5f),
                "사라진 그 자리에서 나섭니다."
            );
            Assert.That(trip, Is.Not.Null);
            Assert.That(trip.Venue.IsOutdoor, Is.False, "들를 때는 가게만 고릅니다.");

            Place(walker, trip.Venue.Entrance);
            yield return null;
            yield return null;
            Assert.That(trip.Arrived, Is.True);
            Assert.That(walker.gameObject.activeSelf, Is.False);

            yield return AdvanceClock(420, 465);
            Assert.That(walker.gameObject.activeSelf, Is.True, "07:45에 가게에서 나옵니다.");
            Assert.That(Flat(walker.transform.position - trip.Venue.Entrance), Is.LessThan(1f));
            Assert.That(NpcOutings.Shared.Find(code), Is.Null);

            yield return AdvanceClock(465, 480);
            Assert.That(
                Flat(walker.Destination - spot),
                Is.LessThan(.5f),
                "나섰던 자리로 돌아갑니다."
            );
            Place(walker, spot);
            yield return null;
            yield return null;
            Assert.That(walker.gameObject.activeSelf, Is.False);
            Assert.That(worker.activeSelf, Is.True);
            Assert.That(Flat(worker.transform.position - spot), Is.LessThan(.5f));
        }

        [UnityTest]
        public IEnumerator APasserbyInConversation_HeadsOutOnlyAfterTheTalkEnds()
        {
            const string code = "TOWN_GRANDFATHER";
            yield return LoadTown(418);
            GameObject worker = Object
                .FindFirstObjectByType<NpcPopulation>()
                .FindWorker(code)
                .gameObject;
            NpcTownWalker walker = TownBodyOf(code);
            Assert.That(TalkTo(worker), Is.True);

            SetClock(420, reset: false);
            yield return null;
            yield return null;
            Assert.That(worker.activeSelf, Is.True, "대화 중에는 제자리에 남습니다.");
            Assert.That(
                walker.gameObject.activeSelf,
                Is.False,
                "같은 사람이 둘로 보이지 않습니다."
            );

            EndTalk(worker);
            yield return null;
            yield return null;
            Assert.That(worker.activeSelf, Is.False);
            Assert.That(walker.gameObject.activeSelf, Is.True);
            Assert.That(
                Flat(walker.transform.position - worker.transform.position),
                Is.LessThan(1.5f)
            );
        }

        [UnityTest]
        public IEnumerator APasserbyTalkingOnTheWayBack_DoesNotReappearAtTheirSpotMeanwhile()
        {
            const string code = "TOWN_GRANDFATHER";
            yield return LoadTown(418);
            GameObject worker = Object
                .FindFirstObjectByType<NpcPopulation>()
                .FindWorker(code)
                .gameObject;
            NpcTownWalker walker = TownBodyOf(code);
            SetClock(420, reset: false);
            yield return null;
            yield return null;
            Vector3 shopDoor = NpcOutings.Shared.Find(code).Venue.Entrance;
            yield return AdvanceClock(420, 470);
            Assert.That(walker.gameObject.activeSelf, Is.True);
            // 제자리에서 떨어진 가게 문 앞에서 말을 건다.
            Place(walker, shopDoor);
            Assert.That(TalkTo(walker.gameObject), Is.True);

            yield return AdvanceClock(470, 482);
            Assert.That(walker.gameObject.activeSelf, Is.True);
            Assert.That(
                worker.activeSelf,
                Is.False,
                "길에서 대화하는 동안 제자리에 다시 나타나지 않습니다."
            );

            EndTalk(walker.gameObject);
            yield return AdvanceClock(482, 484);
            Assert.That(
                walker.gameObject.activeSelf,
                Is.True,
                "대화가 끝나면 제자리로 걸어갑니다."
            );
            Assert.That(worker.activeSelf, Is.False);
        }

        // 실제 대화 화면 없이 행인을 대화 중 상태로 둔다.
        private static bool TalkTo(GameObject npc) => ApproachOf(npc).TryTalkByClick(0, 8);

        private static void EndTalk(GameObject npc) =>
            ApproachOf(npc).EndTalk(Time.realtimeSinceStartupAsDouble);

        private static PasserbyApproach ApproachOf(GameObject npc) =>
            (PasserbyApproach)
                typeof(Passerby)
                    .GetField("approach", PrivateInstance)
                    .GetValue(npc.GetComponent<Passerby>());

        [UnityTest]
        public IEnumerator AfterStayingAWhile_StaffLeaveTheShopAndTryAnotherOne()
        {
            // 06:05에는 06시에 일을 마친 직원들이 여가(08:00까지)로 나선다.
            const int start = 365;
            yield return LoadTown(start);
            NpcLifeData life = NpcLifeData.Parse(
                Resources.Load<NpcOutingCatalog>("NpcOutings").LifeData.text
            );
            NpcOuting trip = NpcOutings.Shared.Trips.FirstOrDefault(item =>
                !item.Venue.IsOutdoor
                && NpcOutings.UntilOutingEnds(
                    NpcDayPlan.At(life.FindNpc(item.Npc), start, false),
                    start
                )
                    >= start + 100
            );
            Assert.That(trip, Is.Not.Null);
            string code = trip.Npc;
            string first = trip.Venue.Code;
            NpcTownWalker walker = TownBodyOf(code);

            Place(walker, trip.Venue.Entrance);
            yield return null;
            yield return null;
            Assert.That(trip.Arrived, Is.True);

            yield return AdvanceClock(start, start + 40);
            Assert.That(walker.gameObject.activeSelf, Is.False, "45분 동안은 안에 머뭅니다.");
            yield return AdvanceClock(start + 40, start + 46);
            Assert.That(walker.gameObject.activeSelf, Is.True, "45분이 지나면 가게에서 나옵니다.");
            yield return null;
            NpcOuting next = NpcOutings.Shared.Find(code);
            Assert.That(next, Is.Not.Null, "여가가 남아 다른 곳으로 갑니다.");
            Assert.That(next.Venue.Code, Is.Not.EqualTo(first));
        }

        private static NpcTownWalker TownBodyOf(string code) =>
            Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == code);

        // 시계를 4분씩 옮겨 이어서 흐른 시각으로 처리하게 한다.
        private static IEnumerator AdvanceClock(int from, int to)
        {
            for (int minute = from + 4; minute < to; minute += 4)
            {
                SetClock(minute, reset: false);
                yield return null;
            }
            SetClock(to, reset: false);
            yield return null;
            yield return null;
        }

        private static float Flat(Vector3 offset) =>
            Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;

        private static IEnumerator LoadTown(int minute = 1125) => LoadScene("TownScene", minute);

        private static IEnumerator LoadScene(string sceneName, int minute = 1125)
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            NpcDuty.MissionNpcCode = null;
            foreach (NpcOuting trip in NpcOutings.Shared.Trips.ToArray())
                NpcOutings.Shared.Release(trip.Npc);
            SetClock(minute, reset: true);
            SceneManager.sceneLoaded += DisconnectApis;
            try
            {
                yield return SceneManager.LoadSceneAsync(sceneName);
                yield return null;
                yield return null;
            }
            finally
            {
                SceneManager.sceneLoaded -= DisconnectApis;
            }
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
        }

        private static void Place(NpcTownWalker walker, Vector3 position)
        {
            var capsule = walker.GetComponent<CharacterController>();
            capsule.enabled = false;
            walker.transform.position = position;
            capsule.enabled = true;
            walker.gameObject.SetActive(true);
            Physics.SyncTransforms();
        }

        private static void DisconnectApis(Scene scene, LoadSceneMode mode)
        {
            foreach (
                GameClockDriver driver in Object.FindObjectsByType<GameClockDriver>(
                    FindObjectsSortMode.None
                )
            )
                driver.enabled = false;
            var dialogue = Object.FindFirstObjectByType<DialogueController>();
            typeof(DialogueController)
                .GetField("conversationApi", PrivateInstance)
                .SetValue(dialogue, null);
            typeof(DialogueController)
                .GetField("followUpApi", PrivateInstance)
                .SetValue(dialogue, null);
            var arrival = Object.FindFirstObjectByType<SpaceArrival>();
            if (arrival != null)
                typeof(SpaceArrival)
                    .GetField("apiSettings", PrivateInstance)
                    .SetValue(arrival, null);
        }

        // 시계는 신호 간격(30초)이 지나야 새 시각을 받으므로, 부를 때마다 더 늦은 신호 시각을 보낸다.
        private static double beatOffset;

        private static void SetClock(int minute, bool reset)
        {
            if (reset)
                typeof(GameClock)
                    .GetMethod("ResetShared", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
            beatOffset += 1e3;
            GameClock
                .Shared.TickAsync(
                    new FixedClock { Minute = minute },
                    Time.realtimeSinceStartupAsDouble + (reset ? 1e5 : 1e6) + beatOffset
                )
                .GetAwaiter()
                .GetResult();
        }

        private sealed class FixedClock : IGameClockApi
        {
            public int Minute;

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) =>
                Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = Minute,
                        Paused = true,
                    }
                );
        }
    }
}
