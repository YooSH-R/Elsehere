using System.Collections;
using System.Linq;
using A305.Dialogue;
using A305.Mission;
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
    /// 마을(TownScene)을 실제로 불러와 인도에서 세운 택시가 옆에 서자마자 손님을 태워 기사와 대화한 뒤 다시 떠나고,
    /// 승객으로 타면 목적지에 내려 주는지 확인한다. 목적지별 하차 위치와 차로 계산은 EditMode 테스트가 맡는다.
    /// </summary>
    public class TaxiRideTests
    {
        private const float WaitTimeoutSeconds = 15f;

        [SetUp]
        [TearDown]
        public void ResetCrossSceneRecords()
        {
            PasserbyRestRecord.Clear();
            PasserbyOfferRecord.Clear();
            // 튜토리얼 동안에는 택시를 세울 수 없으므로 튜토리얼을 마친 사용자로 시작한다.
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [UnityTest]
        public IEnumerator HailingTaxi_BoardsBesideTheCustomerRightAway_ThenDrivesOnAfterTalk()
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();

            // 택시가 달리는 차로 쪽 인도, 택시 앞 8m에 선다.
            CarLoopRoute.Pose pose = car.Route.PoseAt(car.Distance);
            Vector3 curbside = Vector3.Cross(Vector3.up, pose.Forward);
            player.Teleport(
                pose.Position + pose.Forward * 8f + curbside * 2.6f + Vector3.up * .2f,
                Quaternion.identity
            );
            Vector3 customer = player.transform.position;
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Click(offer, "TalkButton");
            yield return null;

            Assert.That(taxi.IsPassenger, Is.True, "세우기를 누르면 바로 타야 합니다.");
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(player.IsMovementLocked, Is.True);
            Vector3 stoppedAt = taxi.transform.position;
            Assert.That(HorizontalDistance(stoppedAt, customer), Is.LessThan(3.5f));

            // 목적지를 말하기 전에는 그 자리에 서서 기다린다.
            yield return new WaitForSeconds(.5f);
            Assert.That(Vector3.Distance(stoppedAt, taxi.transform.position), Is.LessThan(.01f));

            Click(Single<DialoguePanelView>(), "EndButton");
            yield return null;
            Assert.That(taxi.Phase, Is.EqualTo(TaxiPhase.Cruising));
            yield return new WaitForSeconds(1f);
            Assert.That(Vector3.Distance(stoppedAt, taxi.transform.position), Is.GreaterThan(1f));
        }

        [UnityTest]
        public IEnumerator PassengerRequestsHotel_TaxiDrivesAndDropsThemAtTheHotel()
        {
            yield return RideTo("Could you take me to the hotel, please?", "HOTEL", -5, true);
        }

        [UnityTest]
        public IEnumerator PassengerEndsDuringJourney_RestoresCameraAndPlayer()
        {
            yield return RideTo("Take me to the hotel.", "HOTEL", -5, true, true);
        }

        [UnityTest]
        public IEnumerator TalkingToDriver_TripStatusStaysClearOfCurrentGoal()
        {
            // 왼쪽 위 목표 카드가 넓게 펼쳐지면 위쪽 가운데의 택시 안내와 겹쳤다.
            yield return LoadTown();
            ShowHotelGoal(Single<FirstDayMissionView>());
            TaxiRide taxi = Single<TaxiRide>();
            CarLoopRoute.Pose pose = taxi.GetComponent<LoopingCar>()
                .Route.PoseAt(taxi.GetComponent<LoopingCar>().Distance);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Single<PlayerMover>()
                .Teleport(
                    pose.Position
                        + pose.Forward * 8
                        + Vector3.Cross(Vector3.up, pose.Forward) * 2.6f
                        + Vector3.up * .2f,
                    Quaternion.identity
                );
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Click(offer, "TalkButton");
            yield return null;
            Assert.That(taxi.IsPassenger, Is.True);
            Canvas.ForceUpdateCanvases();

            RectTransform status = NamedRect("TaxiTripStatus");
            RectTransform goal = NamedRect("CurrentGoal");
            Assert.That(status.gameObject.activeInHierarchy, Is.True, "택시 안내가 보여야 합니다.");
            Assert.That(goal.gameObject.activeInHierarchy, Is.True, "목표 카드가 보여야 합니다.");
            Assert.That(
                ScreenRectOf(status).Overlaps(ScreenRectOf(goal)),
                Is.False,
                "택시 안내가 목표 카드와 겹칩니다."
            );
            // 캡처는 캔버스 모드를 잠시 바꿔 배치를 다시 계산하므로 검사를 마친 뒤에 찍는다.
            CapturePassengerView("taxi-status-with-goal");
        }

        // 호텔로 가는 단계의 목표 카드(제목·안내·목표 세 개)를 띄운다.
        private static void ShowHotelGoal(FirstDayMissionView mission)
        {
            var hotel = new FirstDayMissionStep
            {
                Index = 1,
                Title = "호텔 체크인",
                Scene = "HOTEL",
                Status = "CURRENT",
            };
            foreach (string goal in new[] { "예약 확인", "캐리어 맡기기", "카페 위치 묻기" })
                hotel.Goals.Add(new FirstDayMissionGoal { Title = goal });
            var state = new FirstDayMissionState
            {
                Started = true,
                Stage = 0,
                Scene = "HOTEL",
                Situation = "호텔로 이동해 다음 이야기를 이어 가세요.",
                Direction = "호텔로 이동하세요.",
            };
            state.Steps.Add(hotel);
            mission.SetAvailable(true);
            mission.Refresh(state, false, false);
        }

        private static RectTransform NamedRect(string name)
        {
            return Object
                .FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Single(rect => rect.name == name);
        }

        private static Rect ScreenRectOf(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera =
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static IEnumerator RideTo(
            string request,
            string destination,
            float arrivalZ,
            bool eastSide,
            bool endEarly = false
        )
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            CarLoopRoute.Pose pose = car.Route.PoseAt(car.Distance);
            player.Teleport(
                pose.Position
                    + pose.Forward * 8
                    + Vector3.Cross(Vector3.up, pose.Forward) * 2.6f
                    + Vector3.up * .2f,
                Quaternion.identity
            );
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Click(offer, "TalkButton");
            yield return null;
            Assert.That(taxi.IsPassenger, Is.True);
            Assert.That(player.IsMovementLocked, Is.True);
            CapturePassengerView("taxi-dialogue");
            Vector3 pickup = taxi.transform.position;
            var panel = Single<DialoguePanelView>();
            panel.GetComponentInChildren<TMPro.TMP_InputField>(true).text = request;
            Click(panel, "SendButton");
            Assert.That(taxi.DestinationCode, Is.EqualTo(destination));
            CapturePassengerView("taxi-driving");
            float previousScale = Time.timeScale;
            Time.timeScale = 12;
            try
            {
                yield return new WaitForSeconds(2);
                Assert.That(Vector3.Distance(pickup, taxi.transform.position), Is.GreaterThan(1));
                if (endEarly)
                    Click(panel, "EndButton");
                float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
                Bounds truck = BoundsOf(GameObject.Find("KenneyDeliveryTruck"));
                while (taxi.IsPassenger)
                {
                    Assert.That(
                        Time.realtimeSinceStartup,
                        Is.LessThan(deadline),
                        "목적지에 도착하지 못했습니다."
                    );
                    MeshRenderer body = taxi.GetComponentsInChildren<MeshRenderer>()
                        .First(renderer => renderer.name == "default");
                    Assert.That(
                        body.bounds.Intersects(truck),
                        Is.False,
                        $"주차 트럭과 겹침: {taxi.transform.position}"
                    );
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            Assert.That(player.enabled, Is.True);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(player.CameraRig.ViewMode, Is.Not.EqualTo(CameraViewMode.Dialogue));
            if (endEarly)
                yield break;
            Assert.That(Mathf.Abs(player.transform.position.z - arrivalZ), Is.LessThan(.5f));
            Assert.That(
                eastSide ? player.transform.position.x > 3.5f : player.transform.position.x < -3.5f,
                Is.True
            );
            Assert.That(Single<DialogueController>().IsTalking, Is.False);
            // 닫으면 이동할 수 있어야 한다. 피드백 창이 열렸다면 기존 학습 UI가 잠금을 관리한다.
            Assert.That(taxi.Phase, Is.EqualTo(TaxiPhase.Cruising));
        }

        private static Bounds BoundsOf(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
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

        private static void CapturePassengerView(string name)
        {
            string output = System.Environment.GetEnvironmentVariable("TAXI_QA_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                return;
            Camera camera = Single<PlayerMover>().CameraRig.GetComponent<Camera>();
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            RenderMode[] modes = canvases.Select(canvas => canvas.renderMode).ToArray();
            Camera[] cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            float[] distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .1f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(output, name + ".png"),
                    pixels.EncodeToPNG()
                );
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                }
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
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

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
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
