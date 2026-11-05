using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
