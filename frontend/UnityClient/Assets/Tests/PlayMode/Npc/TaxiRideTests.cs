using System.Collections;
using System.Linq;
using A305.Dialogue;
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
    /// 마을(TownScene)을 실제로 불러와 가까운 택시에 바로 타서 기사와 대화한 뒤 다시 떠나고,
    /// 승객으로 타면 목적지에 내려 주는지 확인한다. 목적지별 하차 위치와 차로 계산은 EditMode 테스트가 맡는다.
    /// </summary>
    public class TaxiRideTests
    {
        private const float WaitTimeoutSeconds = 15f;
        private const string DriverCode = "TAXI_DRIVER";

        [SetUp]
        public void FixDaytime() => NpcTestClock.SetDaytime();

        [TearDown]
        public void RestoreClock() => NpcTestClock.Restore();

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
        public IEnumerator ShowcaseTaxiWaitsOutsideHotelUntilTheLearnerBoardsOrStopsDemo()
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            taxi.PrepareShowcasePickup();
            Assert.That(taxi.Phase, Is.EqualTo(TaxiPhase.Waiting));
            Vector3 waitingAt = taxi.transform.position;
            Assert.That(taxi.IsPassenger, Is.False);
            Single<PlayerMover>().Teleport(waitingAt + Vector3.right * 20, Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            taxi.PrepareShowcasePickup();
            Assert.That(Vector3.Distance(waitingAt, taxi.transform.position), Is.LessThan(.01f));
            taxi.ReleaseShowcasePickup();
            Assert.That(taxi.Phase, Is.EqualTo(TaxiPhase.Cruising));
            Assert.That(car.HasPulledOver, Is.False);
        }

        [UnityTest]
        public IEnumerator HailingTaxiFromBehind_BoardsWithoutMovingTaxi_ThenDrivesOnAfterTalk()
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();

            // 택시가 멀어지는 타이밍에 의존하지 않도록 멈춘 뒤 뒤쪽에서 접근한다.
            car.enabled = false;
            CarLoopRoute.Pose pose = car.Route.PoseAt(car.Distance);
            Vector3 curbside = Vector3.Cross(Vector3.up, pose.Forward);
            player.Teleport(
                taxi.transform.position - pose.Forward * 5f + curbside * 2.6f + Vector3.up * .2f,
                Quaternion.identity
            );
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Vector3 beforeBoarding = taxi.transform.position;
            Click(offer, "TalkButton");
            yield return null;

            Assert.That(taxi.IsPassenger, Is.True, "택시 타기를 누르면 바로 타야 합니다.");
            Assert.That(Single<DialogueController>().IsTalking, Is.True);
            Assert.That(player.IsMovementLocked, Is.True);
            Vector3 stoppedAt = taxi.transform.position;
            Assert.That(Vector3.Distance(stoppedAt, beforeBoarding), Is.LessThan(.01f));
            car.enabled = true;

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
        public IEnumerator BoardingPrompt_TracksRadiusAndTutorialGate_AndRechecksDistanceOnClick()
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            car.enabled = false;
            Vector3 center = taxi.transform.position;
            Vector3 nearby = center + Vector3.left * 6f + Vector3.up * .2f;
            Vector3 outside = center + Vector3.left * 9f + Vector3.up * .2f;

            player.Teleport(outside, Quaternion.identity);
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.False);

            player.Teleport(nearby, Quaternion.identity);
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.True);
            Assert.That(
                offer
                    .GetComponentsInChildren<TMPro.TMP_Text>()
                    .Any(label => label.text.Contains("택시 타기")),
                Is.True
            );
            var speechIcon = offer.ShownButtonRect.Find("Icon").GetComponent<Image>();
            Assert.That(
                speechIcon.isActiveAndEnabled,
                Is.True,
                "택시 타기에 말풍선이 보여야 합니다."
            );

            player.Teleport(outside, Quaternion.identity);
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.False);

            player.Teleport(nearby, Quaternion.identity);
            taxi.IsHailAllowed = false;
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.False);

            taxi.IsHailAllowed = true;
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.True);

            // 버튼이 그려진 뒤 다음 프레임 전에 멀어져도 탑승하면 안 된다.
            player.Teleport(outside, Quaternion.identity);
            Click(offer, "TalkButton");
            Assert.That(taxi.IsPassenger, Is.False);
            Assert.That(taxi.Phase, Is.EqualTo(TaxiPhase.Cruising));
            yield return null;
            Assert.That(offer.IsTalkPromptShowing, Is.False);
            car.enabled = true;
        }

        [UnityTest]
        public IEnumerator PassengerRequestsHotel_TaxiDrivesAndDropsThemAtTheHotel()
        {
            yield return RideTo("Could you take me to the hotel, please?", "HOTEL", -5, true);
        }

        // 가는 도중에 다른 곳을 말하면 서버가 새 목적지를 보내고, 택시는 그곳에 내려 준다.
        [UnityTest]
        public IEnumerator PassengerChangesDestinationDuringTheRide_DropsThemAtTheNewPlace()
        {
            yield return RideTo("Take me to the hotel.", "HOTEL", -11, false, changeTo: "CAFE");
        }

        [UnityTest]
        public IEnumerator PassengerEndsDuringJourney_RestoresCameraAndPlayer()
        {
            yield return RideTo("Take me to the hotel.", "HOTEL", -5, true, true);
        }

        [UnityTest]
        public IEnumerator SceneUnloadingWhileRiding_DoesNotTouchTheRemovedDialogue()
        {
            // 승객인 채로 장면이 바뀌면 대화창이 택시보다 먼저 지워질 수 있다. 그때 내리면서 지워진 대화창을 고쳐 오류가 났다.
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            CarLoopRoute.Pose pose = taxi.GetComponent<LoopingCar>()
                .Route.PoseAt(taxi.GetComponent<LoopingCar>().Distance);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Single<PlayerMover>()
                .Teleport(
                    pose.Position
                        + pose.Forward * 5
                        + Vector3.Cross(Vector3.up, pose.Forward) * 2.6f
                        + Vector3.up * .2f,
                    Quaternion.identity
                );
            yield return WaitUntil(() => offer.IsTalkPromptShowing);
            Click(offer, "TalkButton");
            yield return null;
            Assert.That(taxi.IsPassenger, Is.True);

            // 실제 장면 종료처럼 신규 출현을 멈춘 뒤 대화 처리와 대화창을 택시보다 먼저 지운다.
            // 일부 객체만 지운 채 출퇴근을 계속 실행하면 다음 검사까지 깨진 장면이 남는다.
            Single<NpcPopulation>().enabled = false;
            Object.DestroyImmediate(Single<DialogueController>());
            Object.DestroyImmediate(Single<DialoguePanelView>().gameObject);
            taxi.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneUnloadingWhileHailPromptIsShown_DoesNotTouchTheRemovedPrompt()
        {
            // [택시 타기]가 떠 있는 채로 장면이 바뀌면 제안 화면이 택시보다 먼저 지워질 수 있다.
            // 그때 택시가 꺼지면서 지워진 제안 화면의 버튼 판을 끄려다 오류가 났다.
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            CarLoopRoute.Pose pose = taxi.GetComponent<LoopingCar>()
                .Route.PoseAt(taxi.GetComponent<LoopingCar>().Distance);
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            Single<PlayerMover>()
                .Teleport(
                    pose.Position
                        + pose.Forward * 5
                        + Vector3.Cross(Vector3.up, pose.Forward) * 2.6f
                        + Vector3.up * .2f,
                    Quaternion.identity
                );
            yield return WaitUntil(() => offer.IsTalkPromptShowing);

            // 장면 종료 중에는 새 출퇴근 NPC가 제거된 제안 화면에 연결되면 안 된다.
            Single<NpcPopulation>().enabled = false;
            Object.DestroyImmediate(offer.gameObject);
            taxi.enabled = false;
            yield return null;
        }

        private static IEnumerator RideTo(
            string request,
            string destination,
            float arrivalZ,
            bool eastSide,
            bool endEarly = false,
            string changeTo = null
        )
        {
            yield return LoadTown();
            TaxiRide taxi = Single<TaxiRide>();
            LoopingCar car = taxi.GetComponent<LoopingCar>();
            PlayerMover player = Single<PlayerMover>();
            PasserbyOfferView offer = Single<PasserbyOfferView>();
            CarLoopRoute.Pose pose = car.Route.PoseAt(car.Distance);
            var arrivals = new System.Collections.Generic.List<string>();
            taxi.ArrivedAtDestination += arrivals.Add;
            player.Teleport(
                pose.Position
                    + pose.Forward * 5
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
            // 목적지는 서버의 의도 판정이 대화 문맥으로 고른다. 이 검사는 서버가 보낸 목적지 이후의 실제 운행을 확인한다.
            taxi.ChooseDestination(DriverCode, destination);
            Assert.That(taxi.DestinationCode, Is.EqualTo(destination));
            if (changeTo != null)
            {
                // 첫 목적지로 출발한 뒤, 도착하기 전에 다른 곳을 말한다.
                yield return new WaitForSeconds(.3f);
                Assert.That(taxi.IsPassenger, Is.True);
                taxi.ChooseDestination(DriverCode, changeTo);
                Assert.That(taxi.DestinationCode, Is.EqualTo(changeTo));
            }
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
                    Bounds body = BoundsOf(taxi.gameObject);
                    Assert.That(
                        body.Intersects(truck),
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
            Assert.That(
                arrivals,
                endEarly ? Is.Empty : Is.EqualTo(new[] { changeTo ?? destination })
            );
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
            // 택시의 이동·종료를 검사할 때 새 행인을 생성하거나 제거하지 않게 한다.
            foreach (
                NpcPopulation population in Object.FindObjectsByType<NpcPopulation>(
                    FindObjectsSortMode.None
                )
            )
                population.enabled = false;
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(DialogueController)
                .GetField("conversationApi", flags)
                .SetValue(Single<DialogueController>(), null);
            typeof(SpaceArrival)
                .GetField("apiSettings", flags)
                .SetValue(Single<SpaceArrival>(), null);
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
