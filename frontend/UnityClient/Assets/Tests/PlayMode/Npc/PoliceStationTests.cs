using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 마을(TownScene)을 실제로 불러와 경찰서 창구의 경찰관이 신고를 받으면(서버 턴 응답의 출동 장소) 접수만 하고, 대화를 마치면
    /// 옆 주차 칸의 순찰차가 출동해 그 장소 앞에 섰다가(장소가 없으면 본길을 한 바퀴 돌고) 주차 칸으로 돌아오는지 확인한다.
    /// 신고인지와 장소는 서버의 의도 판정이 문맥으로 고르므로, 여기서는 창구가 받은 출동 장소부터 본다.
    /// 경찰서 문은 여닫지 않고, 다른 장소처럼 문에 닿으면 화면을 덮었다가 실내(또는 문 밖)에 세우는지도 확인한다.
    /// 순찰차는 앞의 차에 양보하므로 택시·승용차와 서로 막히는 경우가 프레임 속도에 따라 생긴다.
    /// 출동 흐름만 보도록 다른 차를 세워 둔다. 트럭 옆을 한 대씩 지나가는지는 택시만 남겨 두고 따로 본다.
    /// 턴 응답에서 출동 장소를 꺼내는 것과 출동 단계 전환은 EditMode 테스트가 맡는다.
    /// </summary>
    public class PoliceStationTests
    {
        private const string OfficerCode = "POLICE_OFFICER";
        private const float TripTimeoutSeconds = 30f;
        private const float DoorTimeoutSeconds = 5f;

        // 신호 대기와 장소 앞 20초를 넉넉히 넣은 한 구간의 게임 시간
        private const float TripGameSeconds = 150f;

        [SetUp]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            TutorialState.Clear();
        }

        // 시험이 시간 제한으로 끊기면 코루틴의 finally가 돌지 않아 빨라진 시간이 다음 시험까지 남는다.
        [TearDown]
        public void RestoreSharedState()
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = 0;
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator ReportAtTheFountainPark_PatrolCarRespondsThereAndComesBack()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;
            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(car.LightsOn, Is.False);

            TalkToOfficerAndReport("PARK");

            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked), "대화 중에는 접수만 합니다.");
            Assert.That(VehicleStatus(), Does.Contain("신고 접수").And.Contain("분수대 공원"));

            yield return EndTalk();

            Assert.That(
                car.Phase,
                Is.EqualTo(PolicePhase.LeavingBay),
                "대화를 마치면 출동해야 합니다."
            );
            Assert.That(car.SceneCode, Is.EqualTo("PARK"));
            Assert.That(car.LightsOn, Is.True);

            float previousScale = Time.timeScale;
            Time.timeScale = 12;
            try
            {
                yield return WaitForPhase(car, PolicePhase.OnScene);
                // 분수대 공원은 본길 동쪽이라 북행 차로(x=1.5) 공원 앞(z=21)에 선다.
                Assert.That(car.transform.position.x, Is.EqualTo(1.5f).Within(.3f));
                Assert.That(car.transform.position.z, Is.EqualTo(21f).Within(.5f));
                yield return WaitForPhase(car, PolicePhase.Parked);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            Assert.That(Vector3.Distance(car.transform.position, bay), Is.LessThan(.05f));
            Assert.That(car.LightsOn, Is.False);
        }

        // 신고를 받은 경찰관이 창구를 떠나 직접 순찰차를 몰고, 돌아와 주차하면 다시 창구에 선다.
        [UnityTest]
        public IEnumerator ReportingOfficer_DrivesThePatrolCarAndComesBackToTheDesk()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            GameObject officer = Officer().gameObject;
            GameObject atTheWheel = car.transform.Find("Drivers/" + OfficerCode).gameObject;
            Assert.That(
                atTheWheel.activeSelf,
                Is.False,
                "주차해 있을 때는 운전석이 비어 있습니다."
            );

            TalkToOfficerAndReport("ICE_CREAM_PLAZA");
            yield return EndTalk();

            Assert.That(officer.activeSelf, Is.False, "출동한 경찰관은 창구를 떠납니다.");
            Assert.That(atTheWheel.activeSelf, Is.True, "출동한 경찰관이 운전석에 앉습니다.");
            Assert.That(
                car.transform.Find("Drivers")
                    .Cast<Transform>()
                    .Count(driver => driver.gameObject.activeSelf),
                Is.EqualTo(1),
                "운전석에는 신고를 받은 경찰관 한 명만 앉습니다."
            );

            float previousScale = Time.timeScale;
            Time.timeScale = 12;
            try
            {
                yield return WaitForPhase(car, PolicePhase.OnScene);
                Assert.That(car.transform.position.z, Is.EqualTo(-23f).Within(.5f));
                Assert.That(atTheWheel.activeSelf, Is.True);
                yield return WaitForPhase(car, PolicePhase.Parked);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            Assert.That(officer.activeSelf, Is.True, "돌아오면 창구에 다시 섭니다.");
            Assert.That(atTheWheel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ReportingWithoutAPlace_PatrolsTheMainStreetAndComesBack()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;

            TalkToOfficerAndReport(null);
            Assert.That(VehicleStatus(), Does.Contain("신고 접수"));
            yield return EndTalk();

            Assert.That(car.Phase, Is.EqualTo(PolicePhase.LeavingBay));
            Assert.That(car.SceneCode, Is.Null);
            float previousScale = Time.timeScale;
            Time.timeScale = 12;
            try
            {
                yield return WaitForPhase(car, PolicePhase.Patrolling);
                float farthestNorth = car.transform.position.z;
                float deadline = Time.realtimeSinceStartup + TripTimeoutSeconds;
                while (car.Phase == PolicePhase.Patrolling)
                {
                    farthestNorth = Mathf.Max(farthestNorth, car.transform.position.z);
                    Assert.That(
                        Time.realtimeSinceStartup,
                        Is.LessThan(deadline),
                        $"순찰을 마치지 못했습니다. 위치 {car.transform.position}"
                    );
                    yield return null;
                }
                Assert.That(
                    farthestNorth,
                    Is.GreaterThan(60f),
                    "본길 북쪽 끝까지 순찰해야 합니다."
                );
                yield return WaitForPhase(car, PolicePhase.Parked);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            Assert.That(Vector3.Distance(car.transform.position, bay), Is.LessThan(.05f));
        }

        // 택시가 트럭 옆을 비켜 가는 동안 순찰차가 본길에 들어서면 둘이 서로를 앞차로 보고 함께 서 버렸다.
        // 트럭 옆과 남쪽 유턴은 한 번에 한 차만 지나가므로, 택시가 들어오기 전에 출동하면 택시가 기다린다.
        [UnityTest]
        public IEnumerator TaxiReachingTheTruckWhileThePatrolCarLeaves_WaitsSoNeitherStopsForever()
        {
            yield return RespondWhileTheTaxiDrivesSouthFrom(-8f);
        }

        // 택시가 이미 트럭 옆에 들어와 있으면 순찰차가 주차 칸에서 기다렸다가 나간다.
        [UnityTest]
        public IEnumerator DispatchingWhileTheTaxiPassesTheTruck_PatrolCarWaitsSoNeitherStopsForever()
        {
            yield return RespondWhileTheTaxiDrivesSouthFrom(-13f);
        }

        // 서버(PoliceDesk)가 보내는 마을 장소 코드 전체. 신고한 곳이 어디든 그 앞으로 출동해야 한다.
        [UnityTest]
        public IEnumerator EveryTownPlace_IsAPatrolCarDestination()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();

            string[] places =
            {
                "HOME",
                "HOTEL",
                "CAFE",
                "SHOP",
                "POST_OFFICE",
                "DINER",
                "BANK",
                "LAUNDROMAT",
                "FLOWER_SHOP",
                "BARBER",
                "BAKERY",
                "PARK",
                "ARCADE",
                "HOME_STORE",
                "ICE_CREAM_PLAZA",
            };
            Assert.That(places.Where(place => car.DisplayNameOf(place) == null), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SmallTalkWithTheOfficer_DoesNotDispatch()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;

            TalkToOfficer();
            yield return EndTalk();
            yield return new WaitForSeconds(1);

            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(Vector3.Distance(car.transform.position, bay), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator WalkingUpToTheStationDoor_CoversTheScreenAndPlacesPlayerInsideFacingTheDesk()
        {
            yield return LoadTownAfterTheTutorial();
            Assert.That(
                Object
                    .FindObjectsByType<ShopSwingDoor>(FindObjectsSortMode.None)
                    .Where(door => door.name == "PoliceStationDoor"),
                Is.Empty,
                "경찰서 문은 여닫지 않고 장면을 바꿉니다."
            );

            yield return WalkInto(Doorway("OutsideDoorway"));

            AssertStandingAt(Doorway("InsideDoorway").ArrivalPoint);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TownScene"));
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.False);
        }

        [UnityTest]
        public IEnumerator WalkingUpToTheDoorFromInside_PlacesPlayerOutsideFacingTheStreet()
        {
            yield return LoadTownAfterTheTutorial();

            yield return WalkInto(Doorway("InsideDoorway"));

            AssertStandingAt(Doorway("OutsideDoorway").ArrivalPoint);
            Assert.That(Single<PlayerMover>().IsMovementLocked, Is.False);
        }

        private static NpcIdentity Officer() =>
            Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .Single(npc => npc.Code == OfficerCode);

        private static void TalkToOfficer()
        {
            NpcIdentity officer = Officer();
            Assert.That(
                Single<DialogueController>()
                    .TryStart(officer.Code, officer.LookTarget, ConversationInitiator.User),
                Is.True
            );
        }

        // 서버가 신고를 받아 턴 응답에 출동 장소를 실은 것처럼 창구에 전한다. placeCode가 null이면 본길 순찰이다.
        private static void TalkToOfficerAndReport(string placeCode)
        {
            TalkToOfficer();
            Object
                .FindObjectsByType<PoliceReportDesk>(FindObjectsSortMode.None)
                .Single(desk => desk.OfficerCode == OfficerCode)
                .TakeReport(OfficerCode, placeCode);
        }

        private static IEnumerator EndTalk()
        {
            Click(Single<DialoguePanelView>(), "EndButton");
            yield return null;
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
        }

        private static string VehicleStatus()
        {
            // 택시와 같은 화면 위쪽 상태 판(TaxiTripStatus)을 쓴다.
            var status = (TMP_Text)
                typeof(DialoguePanelView)
                    .GetField(
                        "vehicleStatusText",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .GetValue(Single<DialoguePanelView>());
            Assert.That(
                status.transform.parent.gameObject.activeInHierarchy,
                Is.True,
                "출동 상태가 보이지 않습니다."
            );
            return status.text;
        }

        private static InteriorDoorway Doorway(string name)
        {
            return Object
                .FindObjectsByType<InteriorDoorway>(FindObjectsSortMode.None)
                .Single(doorway => doorway.name == name);
        }

        // 문을 보고 감지 영역 안에 세우면 다음 물리 갱신에서 문에 닿는다. 카메라는 플레이어 뒤에 둔다.
        // 화면이 다 덮였다가 다시 걷힐 때까지 기다린다.
        private static IEnumerator WalkInto(InteriorDoorway doorway)
        {
            Vector3 sensor = doorway.transform.TransformPoint(
                doorway.GetComponent<BoxCollider>().center
            );
            Transform arrival = doorway.ArrivalPoint;
            Single<PlayerMover>()
                .Teleport(
                    new Vector3(sensor.x, arrival.position.y, sensor.z),
                    Quaternion.LookRotation(-arrival.forward)
                );
            Single<PlayerCameraRig>().SnapBehindTarget();

            bool covered = false;
            float deadline = Time.realtimeSinceStartup + DoorTimeoutSeconds;
            while (!covered || CurtainOpacity() > 0f)
            {
                covered |= CurtainOpacity() >= 1f;
                Assert.That(
                    Time.realtimeSinceStartup,
                    Is.LessThan(deadline),
                    covered
                        ? "화면이 다시 걷히지 않았습니다."
                        : "문에 닿아도 화면이 덮이지 않았습니다."
                );
                yield return null;
            }
        }

        private static float CurtainOpacity()
        {
            var curtain = Object.FindFirstObjectByType<SpaceTravelCurtain>();
            return curtain != null ? curtain.Opacity : 0f;
        }

        // 도착 지점에 서서 그 방향을 보고, 카메라도 플레이어 뒤에서 같은 쪽을 본다.
        private static void AssertStandingAt(Transform arrival)
        {
            Transform player = Single<PlayerMover>().transform;
            Vector3 offset = player.position - arrival.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(.05f));
            Assert.That(Vector3.Angle(player.forward, arrival.forward), Is.LessThan(1f));
            Vector3 view = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
            Assert.That(Vector3.Angle(view, arrival.forward), Is.LessThan(1f));
        }

        private static void HoldOtherTraffic(PoliceCar patrolCar)
        {
            foreach (
                LoopingCar other in Object.FindObjectsByType<LoopingCar>(FindObjectsSortMode.None)
            )
                if (other.gameObject != patrolCar.gameObject)
                    other.enabled = false;
        }

        private static IEnumerator RespondWhileTheTaxiDrivesSouthFrom(float taxiZ)
        {
            yield return LoadTown();
            PoliceCar patrolCar = Single<PoliceCar>();
            LoopingCar taxi = Single<TaxiRide>().GetComponent<LoopingCar>();
            foreach (
                LoopingCar other in Object.FindObjectsByType<LoopingCar>(FindObjectsSortMode.None)
            )
                if (other.gameObject != patrolCar.gameObject && other != taxi)
                    other.enabled = false;
            taxi.StopAt(taxi.Route.SouthboundDistance(taxiZ));
            taxi.Release();

            Assert.That(patrolCar.Dispatch("DINER"), Is.True);
            // 시간을 빠르게 돌리면 느린 프레임에 차가 한 번에 수 m를 건너뛰어 앞차를 지나친다.
            // 프레임마다 같은 시간만큼 진행해 두 차가 만나는 자리가 실행할 때마다 같게 한다.
            Time.captureDeltaTime = .2f;
            try
            {
                yield return WatchBothCarsUntil(patrolCar, taxi, PolicePhase.OnScene);
                // 돌아올 때도 남행으로 같은 구간에 들어와 주차 칸으로 들어간다.
                yield return WatchBothCarsUntil(patrolCar, taxi, PolicePhase.Parked);
            }
            finally
            {
                Time.captureDeltaTime = 0;
            }
        }

        private static IEnumerator WatchBothCarsUntil(
            PoliceCar patrolCar,
            LoopingCar taxi,
            PolicePhase phase
        )
        {
            // 프레임마다 같은 시간만큼 진행하므로 게임 시간으로 제한한다.
            float deadline = Time.time + TripGameSeconds;
            while (patrolCar.Phase != phase)
            {
                string where =
                    $"순찰차 {patrolCar.transform.position}({patrolCar.Phase}), 택시 {taxi.transform.position}";
                Assert.That(
                    Overlaps(patrolCar.transform, taxi.transform),
                    Is.False,
                    $"두 차가 겹쳤습니다. {where}"
                );
                Assert.That(
                    Time.time,
                    Is.LessThan(deadline),
                    $"{phase} 단계가 되지 않았습니다. {where}"
                );
                yield return null;
            }
        }

        // 두 차 바닥을 차 폭·길이(1.84m×4.1m)보다 조금 작은 사각형으로 보고 분리축으로 겹침을 본다.
        private static bool Overlaps(Transform first, Transform second)
        {
            Vector2 between = Flat(second.position - first.position);
            foreach (
                Vector3 axis in new[] { first.right, first.forward, second.right, second.forward }
            )
            {
                Vector2 flatAxis = Flat(axis).normalized;
                float reach = Reach(first, flatAxis) + Reach(second, flatAxis);
                if (Mathf.Abs(Vector2.Dot(between, flatAxis)) > reach)
                    return false;
            }
            return true;
        }

        private static float Reach(Transform car, Vector2 axis)
        {
            const float halfWidth = .85f;
            const float halfLength = 1.95f;
            return halfWidth * Mathf.Abs(Vector2.Dot(Flat(car.right).normalized, axis))
                + halfLength * Mathf.Abs(Vector2.Dot(Flat(car.forward).normalized, axis));
        }

        private static Vector2 Flat(Vector3 vector) => new Vector2(vector.x, vector.z);

        private static IEnumerator WaitForPhase(PoliceCar car, PolicePhase phase)
        {
            float deadline = Time.realtimeSinceStartup + TripTimeoutSeconds;
            while (car.Phase != phase)
            {
                Assert.That(
                    Time.realtimeSinceStartup,
                    Is.LessThan(deadline),
                    $"{phase} 단계가 되지 않았습니다. 지금 {car.Phase}, 위치 {car.transform.position}"
                );
                yield return null;
            }
        }

        // 튜토리얼 중에는 안내 화면이 이동을 잠그므로, 걸어서 문에 닿는 시험은 튜토리얼을 마친 사용자로 한다.
        private static IEnumerator LoadTownAfterTheTutorial()
        {
            TutorialState.MarkFinishedOnServer();
            yield return LoadTown();
            Assert.That(
                Single<PlayerMover>().IsMovementLocked,
                Is.False,
                "걷기 전에 이동 잠금이 풀려 있어야 합니다."
            );
        }

        private static IEnumerator LoadTown()
        {
            SceneManager.sceneLoaded += ConfigureOfflineScene;
            try
            {
                yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            }
            finally
            {
                SceneManager.sceneLoaded -= ConfigureOfflineScene;
            }
            yield return null;
        }

        private static void ConfigureOfflineScene(Scene scene, LoadSceneMode mode)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(DialogueController)
                .GetField("conversationApi", flags)
                .SetValue(Single<DialogueController>(), null);
            typeof(SpaceArrival)
                .GetField("apiSettings", flags)
                .SetValue(Single<SpaceArrival>(), null);
        }

        private static void Click(Component view, string buttonName)
        {
            Button button = view.GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == buttonName);
            Assert.That(
                button.gameObject.activeInHierarchy,
                Is.True,
                $"{buttonName}이(가) 보이지 않습니다."
            );
            button.onClick.Invoke();
        }

        private static T Single<T>()
            where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
        }
    }
}
