using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 실제 장면에서 NPC가 게임 시각의 일과와 욕구에 따라 자기 공간의 사물로 걸어가 이용하는지 확인한다.
    /// 서버가 알려 준 게임 시각으로 시작하며, 시간이 흐르지 않게 멈춘 시각(대화 중과 같음)을 주면 고른 행동이 끝나지 않는다.
    /// </summary>
    public class NpcRoutineTests
    {
        private const float WaitTimeoutSeconds = 20f;
        private const float NearObjectMeters = 1.3f;

        // 장면의 신호가 이 시험이 정한 시각을 덮어쓰지 않게, 시각을 맞출 때마다 다음 신호를 아주 먼 뒤로 미룬다.
        private static int clockSettings;

        [SetUp]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
        }

        [TearDown]
        public void RestoreFlowingClock()
        {
            // 다른 시험은 서버 없이 장면을 볼 때처럼 1일차 오전 8시부터 흐르는 시각을 쓴다.
            SetClock(8 * 60, paused: false);
        }

        [UnityTest]
        public IEnumerator CafeStaff_OnBreak_WalksToTheMuffinAndEatsIt()
        {
            // 쉬는 시간, 욕구가 모두 시작값(70)이면 배고픔을 채우는 머핀이 가장 급하다(40 > 커피 25 > 의자 15).
            yield return LoadAt("CafeScene", 12 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");

            yield return WaitUntil(() => mia.IsUsing);

            Assert.That(mia.Doing.Action, Is.EqualTo("EAT"));
            Assert.That(
                HorizontalDistance(mia.transform, NearestPlaced("muffin", mia.transform)),
                Is.LessThan(NearObjectMeters)
            );
        }

        [UnityTest]
        public IEnumerator CafeStaff_AtWork_MakesADrinkAtTheMachineThenGoesBackBehindTheCounter()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: false);
            NpcRoutine mia = Routine("CAFE_STAFF");
            Vector3 post = mia.transform.position;

            yield return WaitUntil(() => mia.IsUsing);
            Assert.That(mia.Doing.Action, Is.EqualTo("MAKE_DRINK"));
            Assert.That(
                HorizontalDistance(mia.transform, NearestPlaced("coffee_machine", mia.transform)),
                Is.LessThan(NearObjectMeters)
            );

            // 커피 만들기(게임 3분)가 끝나면 근무 중 할 일이 없어 자리로 돌아간다.
            yield return WaitUntil(() =>
                mia.Doing == null && HorizontalDistance(mia.transform.position, post) < .3f
            );
        }

        [UnityTest]
        public IEnumerator HotelStaff_OnBreak_GoesAroundTheDeskAndSitsOnALobbySofa()
        {
            yield return LoadAt("HotelScene", 12 * 60, paused: true);
            NpcRoutine jake = Routine("HOTEL_STAFF");

            yield return WaitUntil(() => jake.IsSeated);

            Assert.That(jake.Doing.Action, Is.EqualTo("REST"));
            INpcSeat seat = Seats()
                .OrderBy(candidate => HorizontalDistance(jake.transform, candidate.SeatPoint))
                .First();
            Assert.That(
                HorizontalDistance(jake.transform, seat.SeatPoint),
                Is.LessThan(.1f),
                "소파 자리에 앉아야 합니다."
            );
            Assert.That(seat.IsTaken, Is.True, "NPC가 앉은 자리에는 플레이어가 앉을 수 없습니다.");
        }

        [UnityTest]
        public IEnumerator TownPasserby_RestingAtABench_StopsAndComesOverWhenThePlayerArrives()
        {
            // 자유 시간, 욕구가 모두 시작값이면 에너지를 채우는 벤치가 분수 구경보다 급하다(20 > 15).
            // 마을 행인 모델은 뼈대가 없어 앉지 못하고 벤치 앞에 서서 쉰다.
            yield return LoadAt("TownScene", 10 * 60, paused: true);
            NpcRoutine ben = Routine("TOWN_PASSERBY");
            yield return WaitUntil(() => ben.IsUsing);
            Assert.That(ben.Doing.Action, Is.EqualTo("REST"));
            Assert.That(
                HorizontalDistance(ben.transform, NearestPlaced("bench", ben.transform)),
                Is.LessThan(NearObjectMeters)
            );

            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(
                ben.transform.position + ben.transform.forward * 4f,
                Quaternion.identity
            );
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsShowing);

            Assert.That(ben.Doing, Is.Null, "말을 걸러 오면 하던 일을 그만둡니다.");
        }

        /// <summary>서버가 알려 준 게임 시각으로 맞춘 뒤 장면을 불러온다. 장면의 신호는 이 시험 동안 다시 보내지 않는다.</summary>
        private static IEnumerator LoadAt(string scene, int minuteOfDay, bool paused)
        {
            SetClock(minuteOfDay, paused);
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
        }

        private static void SetClock(int minuteOfDay, bool paused)
        {
            var time = new GameClockTime
            {
                Day = 1,
                MinuteOfDay = minuteOfDay,
                Paused = paused,
            };
            clockSettings++;
            Task<GameClockBeatStatus> beat = GameClock.Shared.TickAsync(
                new FixedGameClockApi(time),
                Time.realtimeSinceStartupAsDouble + 1e6 * clockSettings
            );
            Assert.That(beat.Result, Is.EqualTo(GameClockBeatStatus.Updated));
        }

        private static NpcRoutine Routine(string code)
        {
            return Object
                .FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
                .Single(routine => routine.GetComponent<NpcIdentity>().Code == code);
        }

        private static Transform NearestPlaced(string objectKey, Transform from)
        {
            return Object
                .FindObjectsByType<HoverableObject>(FindObjectsSortMode.None)
                .Where(hoverable => hoverable.UnityKey == objectKey)
                .Select(hoverable => hoverable.transform)
                .OrderBy(placed => HorizontalDistance(placed, from))
                .First();
        }

        private static INpcSeat[] Seats()
        {
            return Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<INpcSeat>()
                .ToArray();
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("기다리는 상태가 되지 않았습니다.");
                }

                yield return null;
            }
        }

        private static float HorizontalDistance(Transform first, Transform second)
        {
            return HorizontalDistance(first.position, second.position);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            Vector3 offset = first - second;
            return new Vector2(offset.x, offset.z).magnitude;
        }

        private sealed class FixedGameClockApi : IGameClockApi
        {
            private readonly GameClockTime time;

            public FixedGameClockApi(GameClockTime time)
            {
                this.time = time;
            }

            public Task<GameClockTime> BeatAsync()
            {
                return Task.FromResult(time);
            }
        }
    }
}
