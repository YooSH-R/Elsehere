using System.Collections;
using System.Linq;
using A305.Dialogue;
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
    /// 신고 문장 판별과 출동 단계 전환은 EditMode 테스트가 맡는다.
    /// </summary>
    public class PoliceStationTests
    {
        private const string OfficerCode = "POLICE_OFFICER";
        private const float TripTimeoutSeconds = 30f;

        [SetUp]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            TutorialState.Clear();
        }

        [UnityTest]
        public IEnumerator ReportingATheftAtTheDiner_PatrolCarRespondsThereAndComesBack()
        {
            yield return LoadTown();
            PoliceCar car = Single<PoliceCar>();
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
                while (car.Phase == PolicePhase.Patrolling)
                {
                    farthestNorth = Mathf.Max(farthestNorth, car.transform.position.z);
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
            Vector3 bay = car.transform.position;

            TalkToOfficerAndSay("Hello, officer. Nice day today.");
            yield return EndTalk();
            yield return new WaitForSeconds(1);

            Assert.That(car.Phase, Is.EqualTo(PolicePhase.Parked));
            Assert.That(Vector3.Distance(car.transform.position, bay), Is.LessThan(.01f));
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
