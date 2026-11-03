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
    /// 마을(TownScene)을 실제로 불러와 경찰서 창구의 경찰관에게 신고하면 접수만 하고, 대화를 마치면 옆 주차 칸의 순찰차가
    /// 출동해 말한 장소 앞에 섰다가(장소가 없으면 본길을 한 바퀴 돌고) 주차 칸으로 돌아오는지 확인한다.
    /// 대화를 마치고 밖으로 나온 플레이어가 출동하는 모습을 보게 하려는 것이다.
    /// 경찰서 문은 여닫지 않고, 다른 장소처럼 문에 닿으면 화면을 덮었다가 실내(또는 문 밖)에 세우는지도 확인한다.
    /// 순찰차는 앞의 차에 양보하므로 택시·승용차와 서로 막히는 경우가 프레임 속도에 따라 생긴다.
    /// 출동 흐름만 보도록 다른 차를 세워 둔다.
    /// 신고 문장 판별과 출동 단계 전환은 EditMode 테스트가 맡는다.
    /// </summary>
    public class PoliceStationTests
    {
        private const string OfficerCode = "POLICE_OFFICER";
        private const float TripTimeoutSeconds = 30f;
        private const float DoorTimeoutSeconds = 5f;

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
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator ReportingATheftAtTheDiner_PatrolCarRespondsThereAndComesBack()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;
            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(car.LightsOn, Is.False);

            TalkToOfficerAndSay("My wallet was stolen at the diner.");

            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked), "대화 중에는 접수만 합니다.");
            Assert.That(VehicleStatus(), Does.Contain("신고 접수").And.Contain("메이플 다이너"));

            yield return EndTalk();

            Assert.That(
                car.Phase,
                Is.EqualTo(PolicePhase.LeavingBay),
                "대화를 마치면 출동해야 합니다."
            );
            Assert.That(car.SceneCode, Is.EqualTo("DINER"));
            Assert.That(car.LightsOn, Is.True);

            float previousScale = Time.timeScale;
            Time.timeScale = 12;
            try
            {
                yield return WaitForPhase(car, PolicePhase.OnScene);
                // 다이너는 동쪽 건물이라 북행 차로(x=1.5) 다이너 앞(z=39)에 선다.
                Assert.That(car.transform.position.x, Is.EqualTo(1.5f).Within(.3f));
                Assert.That(car.transform.position.z, Is.EqualTo(39f).Within(.5f));
                yield return WaitForPhase(car, PolicePhase.Parked);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            Assert.That(Vector3.Distance(car.transform.position, bay), Is.LessThan(.05f));
            Assert.That(car.LightsOn, Is.False);
        }

        [UnityTest]
        public IEnumerator ReportingWithoutAPlace_PatrolsTheMainStreetAndComesBack()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;

            TalkToOfficerAndSay("I lost my wallet.");
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

        [UnityTest]
        public IEnumerator SmallTalkWithTheOfficer_DoesNotDispatch()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
            HoldOtherTraffic(car);
            Vector3 bay = car.transform.position;

            TalkToOfficerAndSay("Hello, officer. Nice day today.");
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

        private static void TalkToOfficerAndSay(string text)
        {
            NpcIdentity officer = Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .Single(npc => npc.Code == OfficerCode);
            var dialogue = Single<DialogueController>();
            Assert.That(
                dialogue.TryStart(officer.Code, officer.LookTarget, ConversationInitiator.User),
                Is.True
            );
            var panel = Single<DialoguePanelView>();
            panel.GetComponentInChildren<TMP_InputField>(true).text = text;
            Click(panel, "SendButton");
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
