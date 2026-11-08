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
            yield return LoadTown();
            var trips = NpcOutings.Shared.Trips.ToArray();
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
            yield return LoadTown();
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
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
            var original = Object
                .FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(item => item.GetComponent<NpcIdentity>().Code == trip.Npc);
            original.gameObject.SetActive(false);
            var room = Object
                .FindObjectsByType<ShopInterior>(FindObjectsSortMode.None)
                .Single(item => item.PlaceCode == "BANK");
            yield return (IEnumerator)
                typeof(ShopInterior)
                    .GetMethod("SetOccupied", PrivateInstance)
                    .Invoke(room, new object[] { true });
            yield return null;
            yield return null;
            var guest = Object
                .FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.name == "ARCADE_STAFFVisitor");
            Assert.That(guest, Is.Not.Null);
            Assert.That(guest.gameObject.scene.name, Is.EqualTo(bank.Scene));
            Assert.That(guest.IsOffDuty, Is.True);
            Assert.That(
                original.gameObject.activeSelf,
                Is.False,
                "마을과 시설에 동시에 나타나지 않습니다."
            );
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

        private static IEnumerator LoadTown()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            NpcDuty.MissionNpcCode = null;
            foreach (NpcOuting trip in NpcOutings.Shared.Trips.ToArray())
                NpcOutings.Shared.Release(trip.Npc);
            SetClock(1125, reset: true);
            SceneManager.sceneLoaded += DisconnectApis;
            try
            {
                yield return SceneManager.LoadSceneAsync("TownScene");
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

        private static void SetClock(int minute, bool reset)
        {
            if (reset)
                typeof(GameClock)
                    .GetMethod("ResetShared", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
            GameClock
                .Shared.TickAsync(
                    new FixedClock { Minute = minute },
                    Time.realtimeSinceStartupAsDouble + (reset ? 1e5 : 1e6)
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
