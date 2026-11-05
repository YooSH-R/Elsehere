using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.ObjectHover;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        // NPC가 사물까지 걸어가는 시간이 시험 시간의 대부분이라 걸음과 동작만 빠르게 돌린다.
        // 게임 시계는 실제 시간으로 흐르므로(GameClockDriver) 일과 시각과 행동 길이는 그대로다.
        private const float WalkTimeScale = 4f;

        // 장면의 신호가 이 시험이 정한 시각을 덮어쓰지 않게, 시각을 맞출 때마다 다음 신호를 아주 먼 뒤로 미룬다.
        private static int clockSettings;

        private float previousTimeScale;

        [SetUp]
        public void ResetCrossSceneRecords()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = WalkTimeScale;
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            // 카페 직원은 튜토리얼의 미션 대화 안내부터 다가오므로, 튜토리얼을 이미 마친 사용자로 시작한다.
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [TearDown]
        public void RestoreFlowingClock()
        {
            // 남기면 뒤에 도는 다른 시험의 걸음·물리가 모두 빨라진다.
            Time.timeScale = previousTimeScale;
            // 다른 시험은 서버 없이 장면을 볼 때처럼 1일차 오전 8시부터 흐르는 시각과 처음 튜토리얼을 쓴다.
            SetClock(8 * 60, paused: false);
            TutorialState.Clear();
            // 하루 한 번 기록은 이 기기에 남으므로 Editor에서 플레이할 때 남지 않게 지운다.
            PasserbyOfferRecord.Clear();
        }

        [UnityTest]
        public IEnumerator CafeStaff_OnBreak_WalksToTheMuffinAndEatsIt()
        {
            // 쉬는 시간, 욕구가 모두 시작값(70)이면 배고픔을 채우는 머핀이 가장 급하다(40 > 커피 25 > 의자 15).
            yield return LoadAt("CafeScene", BreakStartMinute("CAFE_STAFF"), paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");

            yield return WaitUntil(() => mia.IsUsing);

            Assert.That(mia.Doing.Action, Is.EqualTo("EAT"));
            Assert.That(
                HorizontalDistance(mia.transform, NearestPlaced("muffin", mia.transform)),
                Is.LessThan(NearObjectMeters)
            );
            yield return WaitUntil(() => IsPlaying(mia, "PickUp"));
        }

        [UnityTest]
        public IEnumerator CafeStaff_AtWork_MakesADrinkThenKeepsMovingInsteadOfStandingStill()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: false);
            NpcRoutine mia = Routine("CAFE_STAFF");

            yield return WaitUntil(() => mia.IsUsing);
            Assert.That(mia.Doing.Action, Is.EqualTo("MAKE_DRINK"));
            Assert.That(
                HorizontalDistance(mia.transform, NearestPlaced("coffee_machine", mia.transform)),
                Is.LessThan(NearObjectMeters)
            );
            yield return WaitUntil(() => IsPlaying(mia, "Work"));

            Vector3 drinkSpot = mia.transform.position;

            // 커피 만들기(게임 3분)가 끝나면 급한 일이 없어도 한곳에 서 있지 않고 가게 안 사물들을 옮겨 다닌다.
            yield return WaitUntil(() =>
                mia.Doing == null && HorizontalDistance(mia.transform.position, drinkSpot) > 1f
            );
            Assert.That(
                IsPlaying(mia, "Locomotion"),
                Is.True,
                "걸을 때는 걷기 동작으로 돌아옵니다."
            );
            yield return WaitUntilStill(mia.transform);
            Vector3 nextSpot = mia.transform.position;
            yield return WaitUntil(() => HorizontalDistance(mia.transform.position, nextSpot) > 1f);
        }

        [UnityTest]
        public IEnumerator CafeStaff_WhenThePlayerComesNearTheCounter_GoesBackBehindItAndOffers()
        {
            // 아침에는 Mia 혼자 근무한다(Leo는 10시, Zoe는 13시 출근). 함께 근무하면 하는 일이 없는 직원이 응대한다(NpcDutyTests).
            yield return LoadAt("CafeScene", 8 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            Vector3 post = mia.transform.position;
            Bounds counter = SolidBounds(NearestPlaced("counter", mia.transform));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();
            yield return WaitUntil(() => mia.IsUsing);

            // 손님 쪽에서 카운터와 3m 떨어져 있으면 하던 일(근무 중이라 음료 만들기)을 계속한다.
            player.Teleport(
                new Vector3(counter.center.x, post.y, counter.min.z - 3f),
                Quaternion.identity
            );
            yield return new WaitForSeconds(1f);
            Assert.That(mia.IsUsing, Is.True, "카운터에서 멀면 하던 일을 계속합니다.");
            Assert.That(offer.IsShowing, Is.False);

            // 카운터 앞 2m 안으로 오면 카운터 뒤 자리로 돌아가 응대한다.
            player.Teleport(
                new Vector3(counter.center.x, post.y, counter.min.z - .8f),
                Quaternion.identity
            );
            yield return WaitUntil(() => offer.IsShowing);

            Assert.That(mia.Doing, Is.Null);
            Assert.That(
                HorizontalDistance(mia.transform.position, post),
                Is.LessThan(.3f),
                "카운터 뒤 자리에서 응대해야 합니다."
            );
            yield return WaitUntil(() => IsPlaying(mia, "Talk"));
        }

        [UnityTest]
        public IEnumerator CafeStaff_BehindTheCounter_TurnsToFaceTheCustomerBeforeOffering()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            Vector3 post = mia.transform.position;
            Bounds counter = SolidBounds(NearestPlaced("counter", mia.transform));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();

            // 카운터 끝 앞(카운터에서 0.8m)이라 응대하러 돌아오지만, 직원 자리에서는 3.5m보다 멀어 아직 제안하지 않는 곳
            player.Teleport(
                new Vector3(counter.max.x - .15f, post.y, counter.min.z - .8f),
                Quaternion.identity
            );
            yield return WaitUntil(() =>
                HorizontalDistance(mia.transform.position, post) < .3f
                && FacingDegrees(mia.transform, player.transform) < 20f
            );
        }

        [UnityTest]
        public IEnumerator HotelStaff_OnBreak_GoesAroundTheDeskAndSitsOnALobbySofa()
        {
            yield return LoadAt("HotelScene", BreakStartMinute("HOTEL_STAFF"), paused: true);
            NpcRoutine jake = Routine("HOTEL_STAFF");
            // 교대 밖의 다른 호텔 직원도 쉬러 소파로 가 자리를 먼저 차지할 수 있으므로, Jake만 지내게 해 Jake가 고른 자리를 본다.
            KeepOnlyLiving(jake);

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
            yield return WaitUntil(() => IsPlaying(jake, "Sit"));
        }

        [UnityTest]
        public IEnumerator TownPasserby_RestingAtABench_StopsWhenThePlayerTalksToHim()
        {
            // 자유 시간, 욕구가 모두 시작값이면 에너지를 채우는 벤치가 분수 구경보다 급하다(20 > 15).
            yield return LoadAt("TownScene", 10 * 60, paused: true);
            NpcRoutine ben = Routine("TOWN_PASSERBY");
            // Ben은 마을 시작 위치 가까운 집 앞에 있어 곧장 다가오므로, 플레이어를 벤치가 없는 동서 길 서쪽으로 멀리 보내 무시한 것으로 두고 쉬게 한다.
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(new Vector3(-14f, .12f, 10f), Quaternion.identity);
            yield return WaitUntil(() => ben.IsUsing);
            Assert.That(ben.Doing.Action, Is.EqualTo("REST"));
            Assert.That(
                HorizontalDistance(ben.transform, NearestPlaced("bench", ben.transform)),
                Is.LessThan(NearObjectMeters)
            );

            // 쉬는 동안에는 다가오지 않으므로 플레이어가 가까이 가서 먼저 말을 건다. 벤치의 [잠깐 앉기]와 겹치지 않게 벤치 반대쪽에 선다.
            Vector3 awayFromBench =
                ben.transform.position - NearestPlaced("bench", ben.transform).position;
            awayFromBench.y = 0f;
            player.Teleport(
                ben.transform.position + awayFromBench.normalized * 1.5f,
                Quaternion.identity
            );
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Assert.That(offer.TalkLabelText, Does.Contain("Ben"));
            offer
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "TalkButton")
                .onClick.Invoke();
            yield return null;

            Assert.That(Object.FindFirstObjectByType<DialogueController>().IsTalking, Is.True);
            Assert.That(ben.Doing, Is.Null, "말을 걸면 하던 일을 그만둡니다.");
        }

        [UnityTest]
        public IEnumerator TownPasserby_DuringTheTutorial_WaitsInFrontOfTheHouseWithoutLiving()
        {
            // 튜토리얼 중에는 Ben이 집 앞에서 정해진 흐름(길 묻기)대로만 움직이고 생활하지 않는다.
            TutorialState.Clear();
            yield return LoadAt("TownScene", 10 * 60, paused: true);
            NpcRoutine ben = Routine("TOWN_PASSERBY");
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();

            // 자유 시간이라 평소라면 바로 벤치로 가지만, 튜토리얼 중에는 집에서 나온 플레이어에게 길을 물으러 온다.
            yield return WaitUntil(() => offer.IsShowing);

            Assert.That(ben.Doing, Is.Null, "튜토리얼 중에는 생활하지 않습니다.");
            Assert.That(offer.IsDeclineShown, Is.False, "튜토리얼 대화는 거절할 수 없습니다.");
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

        // 쉬는 시간은 교대에 따라 생활 데이터(Data/Npc/NpcLife.json)가 정하므로, 시각을 시험에 적지 않고 그 NPC의 쉬는 시간이 시작하는 분을 읽는다.
        private static int BreakStartMinute(string code)
        {
            NpcLifeEntry npc = NpcLifeData
                .Parse(
                    File.ReadAllText(Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json"))
                )
                .FindNpc(code);
            return Enumerable
                .Range(0, 24 * 60)
                .First(minute =>
                    npc.ActivityAt(minute) == "BREAK" && npc.ActivityAt(minute - 1) != "BREAK"
                );
        }

        // 이 NPC 말고는 생활을 꺼 제자리에 세운다.
        private static void KeepOnlyLiving(NpcRoutine npc)
        {
            foreach (
                NpcRoutine other in Object.FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
            )
            {
                other.enabled = other == npc;
            }
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

        private static Bounds SolidBounds(Transform placed)
        {
            Collider[] colliders = placed
                .GetComponentsInChildren<Collider>()
                .Where(collider => !collider.isTrigger)
                .ToArray();
            Bounds bounds = colliders[0].bounds;
            foreach (Collider collider in colliders)
            {
                bounds.Encapsulate(collider.bounds);
            }

            return bounds;
        }

        // 모델 Animator가 그 동작 상태를 재생 중인지
        private static bool IsPlaying(NpcRoutine npc, string state)
        {
            Animator animator = npc.GetComponentsInChildren<Animator>()
                .First(candidate => candidate.isActiveAndEnabled);
            return animator.GetCurrentAnimatorStateInfo(0).IsName(state);
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

        // 0.3초 동안 거의 움직이지 않을 때까지 기다린다.
        private static IEnumerator WaitUntilStill(Transform walker)
        {
            float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
            while (true)
            {
                Vector3 before = walker.position;
                yield return new WaitForSeconds(.3f);
                if (HorizontalDistance(before, walker.position) < .02f)
                {
                    yield break;
                }

                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("멈추지 않았습니다.");
                }
            }
        }

        // 앞을 보는 방향과 상대 쪽 방향 사이의 수평 각도
        private static float FacingDegrees(Transform looker, Transform target)
        {
            Vector3 toTarget = target.position - looker.position;
            toTarget.y = 0f;
            Vector3 forward = looker.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget);
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
