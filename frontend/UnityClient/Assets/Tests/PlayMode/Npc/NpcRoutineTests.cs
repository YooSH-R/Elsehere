using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Learning;
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
            SceneManager.sceneLoaded += UseLocalScene;
            previousTimeScale = Time.timeScale;
            Time.timeScale = WalkTimeScale;
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            NpcDuty.MissionNpcCode = null;
            // 카페 직원은 튜토리얼의 미션 대화 안내부터 다가오므로, 튜토리얼을 이미 마친 사용자로 시작한다.
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [TearDown]
        public void RestoreFlowingClock()
        {
            SceneManager.sceneLoaded -= UseLocalScene;
            // 남기면 뒤에 도는 다른 시험의 걸음·물리가 모두 빨라진다.
            Time.timeScale = previousTimeScale;
            // 다른 시험은 서버 없이 장면을 볼 때처럼 1일차 오전 8시부터 흐르는 시각과 처음 튜토리얼을 쓴다.
            SetClock(8 * 60, paused: false);
            TutorialState.Clear();
            // 하루 한 번 기록은 이 기기에 남으므로 Editor에서 플레이할 때 남지 않게 지운다.
            PasserbyOfferRecord.Clear();
        }

        private static void UseLocalScene(Scene scene, LoadSceneMode mode)
        {
            // 대화·생활 검사는 서버 세션 상태에 영향을 받지 않는 고정 대본으로 진행한다.
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                    continue;
                if (
                    component is A305.Learning.LearningController
                    || component.GetType().Name == "SessionExpiredView"
                )
                    component.enabled = false;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                component.GetType().GetField("useServer", flags)?.SetValue(component, false);
                component.GetType().GetField("apiSettings", flags)?.SetValue(component, null);
                if (component is DialogueController)
                {
                    foreach (
                        string field in new[]
                        {
                            "conversationApi",
                            "aiStatusApi",
                            "followUpApi",
                            "voiceConsentAgreement",
                        }
                    )
                        component.GetType().GetField(field, flags)?.SetValue(component, null);
                }
            }
        }

        [UnityTest]
        public IEnumerator HotelStaff_CollidersBlockEachOther_AfterBeingEnabledAgain()
        {
            yield return LoadAt("HotelScene", 12 * 60, paused: true);
            CharacterController[] bodies = Object
                .FindObjectsByType<Passerby>(FindObjectsSortMode.None)
                .Select(npc => npc.GetComponent<CharacterController>())
                .Take(2)
                .ToArray();
            Assert.That(bodies, Has.Length.EqualTo(2));
            Assert.That(
                Physics.GetIgnoreCollision(bodies[0], bodies[1]),
                Is.False,
                "NPC끼리 충돌을 무시하면 서로 통과합니다."
            );
            foreach (Passerby npc in Object.FindObjectsByType<Passerby>(FindObjectsSortMode.None))
                npc.enabled = false;
            foreach (
                NpcRoutine routine in Object.FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
            )
                routine.enabled = false;
            // 가구·바닥과 분리된 곳에서 실제 이동 충돌을 확인한다.
            for (int index = 0; index < bodies.Length; index++)
            {
                bodies[index].enabled = false;
                bodies[index].transform.position = new Vector3(index * 2, 100, 0);
                bodies[index].enabled = true;
            }
            Physics.SyncTransforms();
            bodies[0].Move(Vector3.right * 4);
            Assert.That(
                bodies[0].transform.position.x,
                Is.LessThan(bodies[1].transform.position.x),
                "다른 NPC를 관통해 뒤로 지나갈 수 없어야 합니다."
            );
            Assert.That(
                HorizontalDistance(bodies[0].transform, bodies[1].transform),
                Is.GreaterThanOrEqualTo(
                    bodies[0].radius + bodies[1].radius - bodies[0].skinWidth - bodies[1].skinWidth
                )
            );
        }

        [UnityTest]
        public IEnumerator BurgerShopStaff_StayAtTheirPostDuringWork()
        {
            yield return BurgerStaffStaysAtPost("DINER_STAFF", 9 * 60);
        }

        [UnityTest]
        public IEnumerator BurgerShopQuestStaff_KeepsTheCounterAtNight()
        {
            // 이야기 퀘스트의 Rosa는 24시간 상주해 밤에도 카운터 자리를 지킨다.
            yield return BurgerStaffStaysAtPost("DINER_STAFF", 21 * 60);
        }

        [UnityTest]
        public IEnumerator BurgerShopNightStaff_StaysAtOwnSpotAwayFromTheCounter()
        {
            // 밤 교대 Ray는 카운터를 Rosa에게 맡기고, 카운터에서 떨어진 가게 안 자기 자리를 지킨다. 둘이 같은 자리에 겹쳐 서지 않는다.
            yield return LoadAt(PlaceScenes.SceneOf("DINER"), 21 * 60, paused: true);

            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, .12f, -2f), Quaternion.identity);
            NpcRoutine ray = Routine("FIRE_CAPTAIN");
            NpcRoutine rosa = Routine("DINER_STAFF");
            // 주문대의 상판(Worktop)도 "counter"지만 단어 표시 영역만 있어, 충돌체가 있는 주문대 전체로 잰다.
            Bounds counterBounds = SolidBounds(GameObject.Find("OrderCounter").transform);
            Vector3 startingPosition = ray.transform.position;
            yield return new WaitForSeconds(30);

            Assert.That(
                HorizontalDistance(ray.transform.position, startingPosition),
                Is.LessThan(.2f),
                "밤 교대 직원은 자기 자리를 지켜야 합니다."
            );
            Vector3 nearestCounterPoint = counterBounds.ClosestPoint(
                new Vector3(
                    ray.transform.position.x,
                    counterBounds.center.y,
                    ray.transform.position.z
                )
            );
            Assert.That(
                HorizontalDistance(ray.transform.position, nearestCounterPoint),
                Is.GreaterThan(1.5f),
                "밤 교대 직원은 카운터로 가지 않아야 합니다."
            );
            Assert.That(
                HorizontalDistance(ray.transform, rosa.transform),
                Is.GreaterThan(1f),
                "밤 교대 직원과 상주 직원이 겹쳐 서지 않아야 합니다."
            );
        }

        private static IEnumerator BurgerStaffStaysAtPost(string npcCode, int minuteOfDay)
        {
            yield return LoadAt(PlaceScenes.SceneOf("DINER"), minuteOfDay, paused: true);

            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(new Vector3(0, .12f, -2f), Quaternion.identity);
            NpcRoutine staff = Routine(npcCode);
            KeepOnlyNpc(staff);
            Vector3 startingPosition = staff.transform.position;
            GameObject counter = GameObject.Find("OrderCounter");
            Assert.That(counter, Is.Not.Null, "햄버거 가게 실내에 주문 카운터가 있어야 합니다.");
            Vector3 counterOffset = counter.transform.InverseTransformPoint(startingPosition);
            Assert.That(Mathf.Abs(counterOffset.x), Is.LessThan(.3f));
            Assert.That(counterOffset.z, Is.InRange(-1.3f, -.5f));
            Assert.That(
                counterOffset.y,
                Is.GreaterThan(.3f),
                "직원은 카운터 뒤 발판에 서야 합니다."
            );
            yield return new WaitForSeconds(30);

            Assert.That(
                HorizontalDistance(staff.transform.position, startingPosition),
                Is.LessThan(.2f),
                "근무 중인 버거 가게 직원은 손님이 없어도 카운터 자리를 지켜야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator HotelJake_StaysAtTheCenterOfTheReceptionDesk()
        {
            yield return LoadAt("HotelScene", 12 * 60, paused: true);
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, .12f, -20), Quaternion.identity);
            NpcRoutine jake = Routine("HOTEL_STAFF");
            Bounds desk = SolidBounds(NearestPlaced("reception_desk", jake.transform));
            Vector3 startingPosition = jake.transform.position;

            yield return WaitUntil(() => jake.IsUsing);
            SetClock(13 * 60, paused: true);
            yield return new WaitForSeconds(20);

            Assert.That(
                Mathf.Abs(jake.transform.position.x - desk.center.x),
                Is.LessThan(.3f),
                "Jake는 프런트 데스크 가운데에 있어야 합니다."
            );
            Assert.That(
                HorizontalDistance(jake.transform.position, startingPosition),
                Is.LessThan(.1f),
                "Jake는 프런트 데스크에서 돌아다니지 않아야 합니다."
            );

            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(
                new Vector3(desk.center.x, player.transform.position.y, desk.min.z - .8f),
                Quaternion.identity
            );
            yield return new WaitForSeconds(8);
            Assert.That(
                HorizontalDistance(jake.transform.position, startingPosition),
                Is.LessThan(.1f),
                "손님을 응대할 때도 Jake는 자기 자리를 지켜야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator HotelStaff_UsingTheReceptionDesk_KeepSeparateStandingPlaces()
        {
            yield return LoadAt("HotelScene", 12 * 60, paused: true);
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(new Vector3(0, .12f, -20), Quaternion.identity);
            NpcRoutine[] staff = Object
                .FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
                .Where(npc => npc.GetComponent<NpcIdentity>().Code.StartsWith("HOTEL_"))
                .ToArray();
            yield return WaitUntil(() =>
                staff.Count(npc => npc.IsUsing && npc.Doing.ObjectKey == "reception_desk") >= 2
            );
            yield return new WaitForSeconds(8);
            NpcRoutine[] serving = staff
                .Where(npc => npc.IsUsing && npc.Doing.ObjectKey == "reception_desk")
                .ToArray();
            for (int first = 0; first < serving.Length; first++)
            {
                for (int second = first + 1; second < serving.Length; second++)
                {
                    Assert.That(
                        HorizontalDistance(
                            serving[first].transform.position,
                            serving[second].transform.position
                        ),
                        Is.GreaterThanOrEqualTo(1.2f),
                        "같은 데스크에서 일하는 NPC의 머리가 겹치지 않아야 합니다."
                    );
                }
            }
        }

        [UnityTest]
        public IEnumerator HotelStaff_WhenServingACustomer_KeepTheirHeadsApart()
        {
            yield return LoadAt("HotelScene", 12 * 60, paused: true);
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(new Vector3(0, .12f, -20), Quaternion.identity);
            NpcRoutine jake = Routine("HOTEL_STAFF");
            Bounds counter = SolidBounds(NearestPlaced("reception_desk", jake.transform));
            yield return new WaitForSeconds(8);
            player.Teleport(
                new Vector3(counter.center.x, player.transform.position.y, counter.min.z - .8f),
                Quaternion.identity
            );
            yield return new WaitForSeconds(12);
            NpcRoutine[] staff = Object
                .FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
                .Where(npc =>
                    npc.GetComponent<NpcIdentity>().Code.StartsWith("HOTEL_") && !npc.IsOffDuty
                )
                .ToArray();
            for (int first = 0; first < staff.Length; first++)
            for (int second = first + 1; second < staff.Length; second++)
                Assert.That(
                    HorizontalDistance(
                        staff[first].transform.position,
                        staff[second].transform.position
                    ),
                    Is.GreaterThanOrEqualTo(1.2f),
                    $"호텔 응대 중 다른 직원과 머리가 겹칩니다: "
                        + $"{staff[first].GetComponent<NpcIdentity>().Code} {staff[first].transform.position} {staff[first].Doing?.ObjectKey}, "
                        + $"{staff[second].GetComponent<NpcIdentity>().Code} {staff[second].transform.position} {staff[second].Doing?.ObjectKey}"
                );
        }

        [UnityTest]
        public IEnumerator HotelStaff_ArrivingFromTheSameSide_ChooseSeparateWorkPlaces()
        {
            // 이동하다 같은 쪽에 모인 직원들도 가장 가까운 한 자리로 겹쳐 들어가지 않아야 한다.
            void BringWorkersTogether(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "HotelScene")
                    return;
                NpcRoutine jake = Routine("HOTEL_STAFF");
                Vector3 start = jake.transform.position;
                foreach (string code in new[] { "HOTEL_STAFF", "HOTEL_RECEPTIONIST" })
                {
                    NpcRoutine worker = Routine(code);
                    CharacterController body = worker.GetComponent<CharacterController>();
                    body.enabled = false;
                    worker.transform.position = start;
                    body.enabled = true;
                }
                Object
                    .FindFirstObjectByType<PlayerMover>()
                    .Teleport(new Vector3(0, .12f, -20), Quaternion.identity);
            }
            SceneManager.sceneLoaded += BringWorkersTogether;
            try
            {
                yield return LoadAt("HotelScene", 12 * 60, paused: true);
            }
            finally
            {
                SceneManager.sceneLoaded -= BringWorkersTogether;
            }
            NpcRoutine first = Routine("HOTEL_STAFF");
            NpcRoutine second = Routine("HOTEL_RECEPTIONIST");
            yield return WaitUntil(() => first.IsUsing && second.IsUsing);
            Assert.That(
                HorizontalDistance(first.transform, second.transform),
                Is.GreaterThanOrEqualTo(1.2f),
                "같은 쪽에서 도착해도 작업 자리는 나누어야 합니다."
            );
        }

        [UnityTest]
        public IEnumerator CafeStaff_OnBreak_LeavesTheWorkplace()
        {
            yield return LoadAt("CafeScene", BreakStartMinute("CAFE_BARISTA"), paused: true);
            yield return null;
            Assert.That(NpcIdentity.Active.Any(npc => npc.Code == "CAFE_BARISTA"), Is.False);
        }

        [UnityTest]
        public IEnumerator CafeStaff_AtWork_MakesADrinkThenKeepsMovingInsteadOfStandingStill()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: false);
            NpcRoutine mia = Routine("CAFE_STAFF");
            KeepOnlyNpc(mia);

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
        public IEnumerator CafeStaff_TwoCharacters_CanPassBehindTheCounter()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            // 공간의 충돌 검사 중에는 교대 일정이 두 번째 직원을 숨기지 않게 한다.
            Object.FindFirstObjectByType<NpcPopulation>().enabled = false;
            var staff = Object.FindObjectsByType<NpcIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            foreach (var npc in staff)
            {
                npc.GetComponent<NpcRoutine>().enabled = false;
                npc.GetComponent<Passerby>().enabled = false;
            }
            var first = staff
                .Single(npc => npc.Code == "CAFE_STAFF")
                .GetComponent<CharacterController>();
            var second = staff
                .Single(npc => npc.Code == "CAFE_BARISTA")
                .GetComponent<CharacterController>();
            Bounds counter = SolidBounds(NearestPlaced("counter", first.transform));
            float frontLane = counter.max.z + .6f;
            float backLane = frontLane + 1.2f;
            first.enabled = false;
            second.enabled = false;
            first.transform.position = new Vector3(-3f, .05f, frontLane);
            second.transform.position = new Vector3(3f, .05f, backLane);
            first.gameObject.SetActive(true);
            second.gameObject.SetActive(true);
            first.enabled = true;
            second.enabled = true;
            Physics.SyncTransforms();

            // 일과 선택 대신 실제 캐릭터의 충돌 몸체를 반대 방향으로 움직여 가구·상대 몸에 막히지 않는지 확인한다.
            for (int step = 0; step < 60; step++)
            {
                first.Move(Vector3.right * .1f);
                second.Move(Vector3.left * .1f);
                yield return null;
            }
            Assert.That(first.transform.position.x, Is.EqualTo(3f).Within(.1f));
            Assert.That(second.transform.position.x, Is.EqualTo(-3f).Within(.1f));
            Assert.That(first.transform.position.z, Is.EqualTo(frontLane).Within(.1f));
            Assert.That(second.transform.position.z, Is.EqualTo(backLane).Within(.1f));
        }

        [UnityTest]
        public IEnumerator ReviewArrival_PreparesWorkingStaffBeforeStartingTheCamera()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            KeepOnlyNpc(mia);
            yield return WaitUntil(() => mia.IsUsing);
            yield return StartLocalReview(mia, "working");
        }

        [UnityTest]
        public IEnumerator ReviewArrival_StandsUpBeforePositioningThePlayerAndCamera()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            KeepOnlyNpc(mia);
            INpcSeat seat = Seats().First(candidate => !candidate.IsTaken);
            typeof(NpcRoutine)
                .GetMethod("SitOn", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(mia, new object[] { seat });
            Assert.That(mia.IsSeated, Is.True);
            yield return StartLocalReview(mia, "seated");
            Assert.That(seat.IsTaken, Is.False, "복습을 시작하면 앉아 있던 자리도 반환합니다.");
        }

        [UnityTest]
        public IEnumerator ReviewArrival_FlowerShopWorkerReturnsFromAnotherActivity()
        {
            yield return LoadAt("BloomFlowersInterior", 10 * 60, paused: true);
            using var presence = new NpcReviewPresence("FLOWER_SHOP_STAFF");
            yield return null;
            var npc = Object
                .FindObjectsByType<NpcIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .First(item =>
                    item.Code == "FLOWER_SHOP_STAFF" && item.GetComponent<NpcTownWalker>() == null
                );
            yield return null;
            var routine = npc.GetComponent<NpcRoutine>();
            KeepOnlyNpc(routine);
            var controller = npc.GetComponent<CharacterController>();
            controller.enabled = false;
            npc.transform.SetPositionAndRotation(
                npc.transform.position + Vector3.right * 1.2f,
                Quaternion.Euler(0, 70, 0)
            );
            controller.enabled = true;
            yield return StartLocalReview(routine, "flower-shop");
        }

        [UnityTest]
        public IEnumerator ReviewArrival_BakeryCameraKeepsClearOfDisplays()
        {
            yield return LoadAt("MapleBakeryInterior", 10 * 60, paused: true);
            using var presence = new NpcReviewPresence("BAKERY_STAFF");
            yield return null;
            yield return null;
            NpcRoutine routine = Routine("BAKERY_STAFF");
            KeepOnlyNpc(routine);
            yield return StartLocalReview(routine, "bakery-clearance");
        }

        private static readonly string[] ReviewBuildings =
        {
            "CafeScene/CAFE_STAFF",
            "HotelScene/HOTEL_STAFF",
            "ShopScene/SHOP_STAFF",
            "PoliceStationScene/POLICE_OFFICER",
            "PostOfficeInterior/POST_OFFICE_STAFF",
            "MapleDinerInterior/DINER_STAFF",
            "TownBankInterior/BANK_STAFF",
            "SudsLaundromatInterior/LAUNDROMAT_STAFF",
            "BloomFlowersInterior/FLOWER_SHOP_STAFF",
            "MainStreetBarberInterior/BARBER_STAFF",
            "MapleBakeryInterior/BAKERY_STAFF",
            "LittleArcadeInterior/ARCADE_STAFF",
            "HomeLivingInterior/HOME_STORE_STAFF",
        };

        [UnityTest]
        public IEnumerator ReviewArrival_AllBuildingsHoldTheNpcInClearDialogueView(
            [ValueSource(nameof(ReviewBuildings))] string building
        )
        {
            string[] visit = building.Split('/');
            yield return LoadAt(visit[0], 10 * 60, paused: true);
            using var presence = new NpcReviewPresence(visit[1]);
            yield return null;
            yield return null;
            NpcRoutine routine = Routine(visit[1]);
            Assert.That(routine, Is.Not.Null, $"{building}: 복습 대상 직원이 있어야 합니다.");
            KeepOnlyNpc(routine);
            yield return StartLocalReview(routine, visit[1]);
        }

        private static IEnumerator StartLocalReview(NpcRoutine npcRoutine, string scenario)
        {
            var body = npcRoutine.GetComponent<Passerby>();
            var post = (Pose)
                typeof(Passerby)
                    .GetField("homePose", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(body);
            var learning = Object.FindFirstObjectByType<LearningController>();
            var dialogue = Object.FindFirstObjectByType<DialogueController>();
            System.Func<NpcIdentity, bool> talk = npc =>
            {
                Assert.That(
                    npcRoutine.IsUsing,
                    Is.False,
                    "카메라를 열기 전에 생활 동작을 멈춥니다."
                );
                Assert.That(
                    npcRoutine.IsSeated,
                    Is.False,
                    "이동할 위치를 계산하기 전에 일어납니다."
                );
                Assert.That(
                    HorizontalDistance(npc.transform.position, post.position),
                    Is.LessThan(2f),
                    "장식을 피하더라도 응대 자리 가까이에 머뭅니다."
                );
                return dialogue.TryStart(npc.Code, npc.LookTarget, ConversationInitiator.User);
            };
            try
            {
                yield return (IEnumerator)
                    typeof(LearningController)
                        .GetMethod("ArriveAndTalk", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(
                            learning,
                            new object[] { body.GetComponent<NpcIdentity>().Code, "복습", talk }
                        );
                Vector3 position = npcRoutine.transform.position;
                Assert.That(
                    body.AllowsPracticePosition(position),
                    Is.True,
                    "NPC는 자신의 건물 구역에 머뭅니다."
                );
                Vector3 shift = position - post.position;
                Assert.That(
                    Physics
                        .RaycastAll(
                            post.position + Vector3.up * .7f,
                            shift.normalized,
                            shift.magnitude,
                            ~0,
                            QueryTriggerInteraction.Ignore
                        )
                        .Any(hit =>
                            hit.collider.GetComponentInParent<CharacterController>() == null
                        ),
                    Is.False,
                    "NPC를 벽 반대편으로 옮기지 않습니다."
                );
                for (int frame = 0; frame < 20; frame++)
                    yield return null;
                Assert.That(
                    HorizontalDistance(npcRoutine.transform.position, position),
                    Is.LessThan(.02f)
                );
                Assert.That(
                    Object.FindFirstObjectByType<PlayerCameraRig>().ViewMode,
                    Is.EqualTo(CameraViewMode.Dialogue)
                );
                var counter = (Bounds)
                    typeof(NpcRoutine)
                        .GetField("counterBounds", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(npcRoutine);
                Vector3 customer = Object.FindFirstObjectByType<PlayerMover>().transform.position;
                Vector3 from = new Vector3(position.x, counter.center.y, position.z);
                Vector3 to = new Vector3(customer.x, counter.center.y, customer.z);
                int side =
                    Mathf.Abs((post.position.x - counter.center.x) / counter.extents.x)
                    > Mathf.Abs((post.position.z - counter.center.z) / counter.extents.z)
                        ? 0
                        : 2;
                // 카페는 카운터 너머, 꽃집은 카운터 옆 실내 응대 자리다.
                if (scenario == "working" || scenario == "seated")
                    Assert.That(
                        (from[side] - counter.center[side]) * (to[side] - counter.center[side]),
                        Is.LessThan(0),
                        "손님이 카운터 직원 쪽으로 들어가지 않아야 합니다."
                    );
                Assert.That(
                    body.AllowsPracticePosition(customer),
                    Is.True,
                    "손님도 직원과 같은 층의 실내에 머뭅니다."
                );
                Assert.That(counter.Contains(to), Is.False, "손님을 카운터 안에 놓지 않습니다.");
                var camera = Object.FindFirstObjectByType<PlayerCameraRig>();
                Vector3 face = body.GetComponent<NpcIdentity>().LookTarget.position;
                Vector3 toCamera = camera.transform.position - face;
                var obstacles = Physics
                    .RaycastAll(
                        face,
                        toCamera.normalized,
                        toCamera.magnitude,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    )
                    .Where(hit =>
                        !hit.transform.IsChildOf(body.transform)
                        && !hit.transform.IsChildOf(
                            Object.FindFirstObjectByType<PlayerMover>().transform
                        )
                    )
                    .Select(hit => $"{hit.transform.name}:{hit.distance:F2}")
                    .ToArray();
                Assert.That(
                    obstacles,
                    Is.Empty,
                    $"{scenario}: npc={position}, face={face}, camera={camera.transform.position}, player={customer}, counter={counter}"
                );
                yield return CaptureReviewPose(scenario);
                if (scenario == "bakery-clearance")
                    Assert.That(
                        Object
                            .FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                            .Where(item =>
                                item.gameObject.scene == body.gameObject.scene
                                && item.enabled
                                && item.sharedMaterial != null
                                && item.sharedMaterial.renderQueue < 2500
                                && item.GetComponentInParent<NpcIdentity>() == null
                                && item.GetComponentInParent<PlayerMover>() == null
                                && item.bounds.SqrDistance(camera.transform.position) < .45f * .45f
                            )
                            .Select(item =>
                                $"{item.name}: {Mathf.Sqrt(item.bounds.SqrDistance(camera.transform.position)):F2}m"
                            )
                            .ToArray(),
                        Is.Empty,
                        "카메라가 진열대나 벽에 붙어 화면을 막지 않아야 합니다."
                    );
                var meshObstacles = Object
                    .FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                    .Where(renderer =>
                        renderer.gameObject.scene == body.gameObject.scene
                        && renderer.enabled
                        && renderer.GetComponentInParent<NpcIdentity>() == null
                        && renderer.GetComponentInParent<PlayerMover>() == null
                        && renderer.sharedMaterial != null
                        && renderer.sharedMaterial.renderQueue < 2500
                    )
                    .Where(renderer =>
                        renderer.bounds.IntersectRay(
                            new Ray(face, toCamera.normalized),
                            out float distance
                        )
                        && distance < toCamera.magnitude
                    )
                    .Select(renderer => renderer.name)
                    .ToArray();
                Assert.That(
                    meshObstacles,
                    Is.Empty,
                    "콜라이더가 없는 장식도 얼굴을 가리지 않아야 합니다."
                );
                Vector3 mouth = face - Vector3.up * .28f;
                Vector3 mouthToCamera = camera.transform.position - mouth;
                Assert.That(
                    Object
                        .FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                        .Where(renderer =>
                            renderer.gameObject.scene == body.gameObject.scene
                            && renderer.enabled
                            && renderer.GetComponentInParent<NpcIdentity>() == null
                            && renderer.GetComponentInParent<PlayerMover>() == null
                            && renderer.sharedMaterial != null
                            && renderer.sharedMaterial.renderQueue < 2500
                        )
                        .Any(renderer =>
                            renderer.bounds.IntersectRay(
                                new Ray(mouth, mouthToCamera.normalized),
                                out float distance
                            )
                            && distance < mouthToCamera.magnitude
                        ),
                    Is.False,
                    "눈뿐 아니라 입까지 카운터와 장식에 가리지 않아야 합니다."
                );
            }
            finally
            {
                if (dialogue.IsTalking)
                    dialogue.End();
            }
        }

        private static IEnumerator CaptureReviewPose(string scenario)
        {
            string output = System.Environment.GetEnvironmentVariable("REVIEW_POSE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output))
                yield break;
            Directory.CreateDirectory(output);
            Object.FindFirstObjectByType<ControlsGuideView>()?.SetBlocked(true);
            Object.FindFirstObjectByType<A305.Mission.FirstDayMissionView>()?.FoldQuestCard();
            Object
                .FindFirstObjectByType<DialogueController>()
                .SetReviewReference(
                    new[]
                    {
                        new ReviewReferenceItem("It's for a friend.", "친구에게 줄 거예요.", false),
                    },
                    0,
                    "<b>상황 복습</b>\n\n이 장소에서 부탁할 일을 이야기해 보세요."
                );
            Camera camera = Object.FindFirstObjectByType<PlayerCameraRig>().GetComponent<Camera>();
            Canvas[] canvases = Object
                .FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas =>
                    canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                )
                .ToArray();
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .3f;
            }
            foreach (
                Vector2Int size in new[] { new Vector2Int(1600, 900), new Vector2Int(844, 390) }
            )
            {
                var target = new RenderTexture(size.x, size.y, 24);
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                yield return null;
                yield return null;
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(
                    Path.Combine(output, $"{scenario}-{size.x}x{size.y}.png"),
                    pixels.EncodeToPNG()
                );
                camera.targetTexture = null;
                Object.Destroy(pixels);
                Object.Destroy(target);
            }
            foreach (Canvas canvas in canvases)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [UnityTest]
        public IEnumerator CafeStaff_WhenThePlayerComesNearTheCounter_OffersConversation()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            KeepOnlyNpc(mia);
            Bounds counter = SolidBounds(NearestPlaced("counter", mia.transform));
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();
            player.Teleport(
                new Vector3(counter.center.x, mia.transform.position.y, counter.min.z - .8f),
                Quaternion.identity
            );
            yield return WaitUntil(() => offer.IsShowing);
            Assert.That(offer.IsShowing, Is.True, "카운터 앞 손님에게 먼저 말을 겁니다.");

            Assert.That(Object.FindFirstObjectByType<DialogueController>().IsTalking, Is.False);
            offer
                .GetComponentsInChildren<Button>()
                .Single(button => button.name == "AcceptButton")
                .onClick.Invoke();
            yield return null;
            Assert.That(Object.FindFirstObjectByType<DialogueController>().IsTalking, Is.True);
            Assert.That(mia.Doing, Is.Null, "선택해 대화가 시작되면 하던 일을 멈춥니다.");
            yield return WaitUntil(() => IsPlaying(mia, "Talk"));
        }

        [UnityTest]
        public IEnumerator CafeStaff_BehindTheCounter_TurnsToFaceTheCustomerBeforeOffering()
        {
            yield return LoadAt("CafeScene", 9 * 60, paused: true);
            NpcRoutine mia = Routine("CAFE_STAFF");
            KeepOnlyNpc(mia);
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
        public IEnumerator HotelStaff_OnBreak_LeavesTheWorkplace()
        {
            yield return LoadAt("HotelScene", BreakStartMinute("HOTEL_RECEPTIONIST"), paused: true);
            yield return null;
            Assert.That(NpcIdentity.Active.Any(npc => npc.Code == "HOTEL_RECEPTIONIST"), Is.False);
        }

        [UnityTest]
        public IEnumerator TownPasserby_RestingAtABench_StopsWhenThePlayerTalksToHim()
        {
            // 자유 시간, 욕구가 모두 시작값이면 에너지를 채우는 벤치가 분수 구경보다 급하다(20 > 15).
            yield return LoadAt("TownScene", 10 * 60, paused: true);
            NpcRoutine ben = Routine("TOWN_PASSERBY");
            KeepOnlyNpc(ben);
            // Ben은 마을 시작 위치 가까운 집 앞에 있어 곧장 다가오므로, 플레이어를 벤치가 없는 동서 길 서쪽으로 멀리 보내 무시한 것으로 두고 쉬게 한다.
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            player.Teleport(new Vector3(-14f, .12f, 10f), Quaternion.identity);
            yield return WaitUntil(() => ben.IsUsing);
            Assert.That(ben.Doing.Action, Is.EqualTo("REST"));
            Assert.That(
                HorizontalDistance(ben.transform, NearestPlaced("bench", ben.transform)),
                Is.LessThan(NearObjectMeters)
            );

            // 쉬는 동안에는 다가오지 않으므로 플레이어가 클릭으로 먼저 말을 건다.
            Vector3 awayFromBench =
                ben.transform.position - NearestPlaced("bench", ben.transform).position;
            awayFromBench.y = 0f;
            player.Teleport(
                ben.transform.position + awayFromBench.normalized * 1.5f,
                Quaternion.identity
            );
            PasserbyOfferView offer = Object.FindFirstObjectByType<PasserbyOfferView>();
            Assert.That(
                ben.GetComponent<Passerby>().TalkByClick(NpcClickTalk.ReachMeters),
                Is.True
            );
            Assert.That(Object.FindFirstObjectByType<DialogueController>().IsTalking, Is.False);
            offer
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "AcceptButton")
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
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
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

        // 단독 생활·방향 검사에서는 다른 NPC의 자리 경쟁과 몸 충돌을 제외한다.
        // 여러 NPC의 실제 충돌·작업 자리 간격은 별도 시나리오로 확인한다.
        private static void KeepOnlyNpc(NpcRoutine npc)
        {
            foreach (
                NpcRoutine other in Object.FindObjectsByType<NpcRoutine>(FindObjectsSortMode.None)
            )
            {
                other.gameObject.SetActive(other == npc);
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

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            )
            {
                return Task.FromResult(time);
            }
        }
    }
}
