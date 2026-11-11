using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Player;
using A305.Tutorial;
using A305.Weather;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    /// <summary>배포용 공간 씬을 실제로 불러와 출입구 연결과 도착 위치를 확인한다.</summary>
    public class SpaceTravelTests
    {
        // 씬 I/O 대기 한도. 도착 위치·방향 검증과 분리하며 다른 비동기 로드 검사와 맞춘다.
        private const float ArrivalTimeoutSeconds = 30f;
        private const float PositionTolerance = 0.05f;
        private const float AngleToleranceDegrees = 1f;

        // 튜토리얼 동안에는 단계를 마칠 때까지 장소 이동을 잠그므로, 이동 자체를 보는 이 시험은 튜토리얼을 마친 사용자로 한다.
        // 영업시간이 아닌 장소는 문이 열리지 않으므로 모든 장소가 여는 오후 1시에 멈춘 시각으로 한다.
        [SetUp]
        public void StartAfterTheTutorialWhileEveryPlaceIsOpen()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            TestGameClock.Set(13 * 60);
        }

        [TearDown]
        public void ClearTutorialAndClock()
        {
            TutorialState.Clear();
            TestGameClock.Restore();
        }

        [UnityTest]
        public IEnumerator ReviewReturn_RestoresSavedStandingAndViewAndUnlocksMovement()
        {
            yield return LoadSpace("HomeScene");
            PlayerMover player = Object.FindFirstObjectByType<PlayerMover>();
            yield return null;
            var saved = new Pose(player.transform.position, player.transform.rotation);
            float viewYaw = 73;
            player.Teleport(saved.position + Vector3.right, Quaternion.Euler(0, 170, 0));

            Assert.That(SpaceTravel.TryRestoreReviewPosition(player, saved, viewYaw), Is.True);
            Assert.That(
                SpaceTravel.TryRestoreReviewPosition(player, saved, viewYaw),
                Is.False,
                "복귀 중에는 같은 복귀를 중복 시작하지 않습니다."
            );
            float deadline = Time.realtimeSinceStartup + 10;
            while (
                (SpaceTravel.IsTraveling || SpaceTravelCurtain.IsCoveringScreen)
                && Time.realtimeSinceStartup < deadline
            )
                yield return null;

            Assert.That(SpaceTravel.IsTraveling, Is.False);
            Assert.That(SpaceTravelCurtain.IsCoveringScreen, Is.False);
            Assert.That(player.IsMovementLocked, Is.False);
            Assert.That(player.transform.position.x, Is.EqualTo(saved.position.x).Within(.05f));
            Assert.That(player.transform.position.z, Is.EqualTo(saved.position.z).Within(.05f));
            Assert.That(
                Quaternion.Angle(player.transform.rotation, saved.rotation),
                Is.LessThan(1)
            );
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(player.CameraRig.ViewYawDegrees, viewYaw)),
                Is.LessThan(1)
            );
        }

        [UnityTest]
        public IEnumerator Travel_WhenAnotherSceneIsUnloading_DoesNotBlockLaterSceneLoads()
        {
            yield return LoadSpace("HomeScene");
            Scene temporaryScene = SceneManager.CreateScene("CI-reproduction-temporary");
            AsyncOperation unloading = SceneManager.UnloadSceneAsync(temporaryScene);
            Assert.That(
                temporaryScene.IsValid(),
                Is.True,
                "The old scene must still exist when travel starts."
            );
            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.True);

            float deadline = Time.realtimeSinceStartup + 30f;
            while (
                (!unloading.isDone || SpaceTravel.IsTraveling)
                && Time.realtimeSinceStartup < deadline
            )
            {
                yield return null;
            }

            Assert.That(unloading.isDone, Is.True, "The previous scene did not unload.");
            Assert.That(SpaceTravel.IsTraveling, Is.False, "The new scene did not finish loading.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("TownScene"));

            AsyncOperation nextLoad = SceneManager.LoadSceneAsync(
                "HomeScene",
                LoadSceneMode.Single
            );
            deadline = Time.realtimeSinceStartup + 30f;
            while (!nextLoad.isDone && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(nextLoad.isDone, Is.True, "A later scene load must not remain blocked.");
        }

        [UnityTest]
        public IEnumerator TryTravel_HomeToTown_PlacesPlayerAtTownHomeDoor()
        {
            yield return LoadSpace("HomeScene");

            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.True);
            yield return WaitForArrival("TownScene");

            AssertPlayerArrivedAt("HomeDoor");
        }

        // 공간 이동 한 번에 일어나는 일을 함께 본다. 씬을 불러오는 데 대부분의 시간이 들어 이동마다 시험을 나누지 않는다.
        [UnityTest]
        public IEnumerator TryTravel_TownToCafe_CoversTheSwapAndLeavesOnlyTheNewSpace()
        {
            yield return LoadSpace("TownScene");
            ThreadPriority usualPriority = Application.backgroundLoadingPriority;
            float opacityWhenSceneChanged = -1f;
            int players = -1;
            int listeners = -1;
            UnityAction<Scene, LoadSceneMode> onSceneLoaded = (scene, mode) =>
            {
                opacityWhenSceneChanged = CurtainOpacity();
                players = Object.FindObjectsByType<PlayerMover>(FindObjectsSortMode.None).Length;
                listeners = Object
                    .FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                    .Length;
            };
            SceneManager.sceneLoaded += onSceneLoaded;
            try
            {
                Assert.That(SpaceTravel.TryTravel("CafeScene", "TownDoor"), Is.True);
                Assert.That(
                    SpaceTravel.TryTravel("HotelScene", "TownDoor"),
                    Is.False,
                    "이동 중에는 다른 이동 요청을 받지 않습니다."
                );
                // 덮인 동안에는 프레임이 끊겨도 보이지 않으므로 불러오기에 프레임 시간을 더 쓴다.
                Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(ThreadPriority.High));
                yield return WaitForArrival("CafeScene");
            }
            finally
            {
                SceneManager.sceneLoaded -= onSceneLoaded;
            }

            AssertPlayerArrivedAt("TownDoor");
            // 씬이 바뀌는 순간의 끊김은 화면이 다 덮인 뒤에 일어나야 보이지 않는다.
            Assert.That(opacityWhenSceneChanged, Is.EqualTo(1f));
            // 이전 공간의 플레이어·카메라가 켜져 있으면 새 공간이 출입구와 카메라를 잘못 찾는다.
            Assert.That(players, Is.EqualTo(1));
            Assert.That(listeners, Is.EqualTo(1));

            yield return WaitUntilCurtainOpens();
            Assert.That(CurtainOpacity(), Is.EqualTo(0f));
            Assert.That(Application.backgroundLoadingPriority, Is.EqualTo(usualPriority));
            Assert.That(LoadedSceneNames(), Is.EqualTo(new[] { "CafeScene" }));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CafeScene"));
        }

        [UnityTest]
        public IEnumerator TryTravel_KeepsACameraRenderingUntilArrival()
        {
            yield return LoadSpace("CafeScene");
            int framesWithoutCamera = 0;

            Assert.That(SpaceTravel.TryTravel("TownScene", "CafeDoor"), Is.True);
            float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
            while (
                (SceneManager.GetActiveScene().name != "TownScene" || SpaceTravel.IsTraveling)
                && Time.realtimeSinceStartup < deadline
            )
            {
                if (Camera.allCamerasCount == 0)
                {
                    framesWithoutCamera++;
                }

                yield return null;
            }

            // 그리는 카메라가 없으면 Editor는 덮개 대신 "No cameras rendering"을 띄운다.
            Assert.That(framesWithoutCamera, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WalkingIntoTown_ShowsWeatherLikeOpeningTown()
        {
            // 하늘색은 게임 시각에 따라 바뀐다. 두 번 비교하는 사이 저녁으로 넘어가지 않게 시계를 멈춰 둔다.
            PinGameClock(paused: true);
            try
            {
                yield return CompareTownWeatherAfterWalkingIn();
            }
            finally
            {
                PinGameClock(paused: false);
            }
        }

        private IEnumerator CompareTownWeatherAfterWalkingIn()
        {
            yield return LoadSpace("TownScene");
            string opened = ShowEveryWeatherLook();
            yield return LoadSpace("CafeScene");

            Assert.That(SpaceTravel.TryTravel("TownScene", "CafeDoor"), Is.True);
            yield return WaitForArrival("TownScene");
            yield return WaitUntilCurtainOpens();

            // 날씨는 어디서 들어왔든 마을 씬에 디자인한 하늘·환경광을 기준으로 바뀌어야 한다.
            Assert.That(ShowEveryWeatherLook(), Is.EqualTo(opened));
        }

        // 마을 매장도 카페처럼 따로 장면이다. 문에 닿으면 마을을 내리고 매장 장면 하나만 남긴다.
        // 마을 NPC가 함께 내려가므로 매장 안에서 바깥 NPC의 이름표가 보이지 않는다.
        [UnityTest]
        public IEnumerator PostOfficeDoor_SwapsTheTownForThePostOfficeAndBack()
        {
            TutorialState.Clear();
            yield return LoadSpace("TownScene");
            TutorialState.GetOrCreate("TOWN_PASSERBY").Skip();
            yield return null;
            yield return null;

            Transform townDoor = FindDoorway("PostOfficeDoor").transform;
            FindPlayer()
                .Teleport(
                    townDoor.position + townDoor.forward * 0.4f,
                    Quaternion.LookRotation(-townDoor.forward)
                );
            Object.FindFirstObjectByType<PlayerCameraRig>().SnapBehindTarget();
            yield return WaitForArrival("PostOfficeInterior");
            yield return WaitUntilCurtainOpens();

            Assert.That(
                LoadedSceneNames(),
                Is.EqualTo(new[] { "PostOfficeInterior" }),
                "매장에 들어가면 마을 장면을 내립니다."
            );
            AssertPlayerArrivedAt("TownDoor");

            Transform shopDoor = FindDoorway("TownDoor").transform;
            FindPlayer()
                .Teleport(
                    shopDoor.position + shopDoor.forward * 0.4f,
                    Quaternion.LookRotation(-shopDoor.forward)
                );
            Object.FindFirstObjectByType<PlayerCameraRig>().SnapBehindTarget();
            yield return WaitForArrival("TownScene");
            yield return WaitUntilCurtainOpens();

            Assert.That(LoadedSceneNames(), Is.EqualTo(new[] { "TownScene" }));
            AssertPlayerArrivedAt("PostOfficeDoor");
        }

        [UnityTest]
        public IEnumerator WalkingIntoCafeDoor_EntersCafeAtTownDoor()
        {
            yield return LoadSpace("TownScene");
            Transform cafeDoor = FindDoorway("CafeDoor").transform;

            // 문 앞 감지 영역 안에 문을 보고 세우면, 다음 이동에서 문에 닿는다.
            // 카메라도 플레이어 뒤에 두어 앞으로 걸어 문에 들어가는 경우와 같게 한다.
            FindPlayer()
                .Teleport(
                    cafeDoor.position + cafeDoor.forward * 0.4f,
                    Quaternion.LookRotation(-cafeDoor.forward)
                );
            Object.FindFirstObjectByType<PlayerCameraRig>().SnapBehindTarget();
            yield return WaitForArrival("CafeScene");

            AssertPlayerArrivedAt("TownDoor");
        }

        [UnityTest]
        public IEnumerator WalkingBackwardThroughCafeDoor_KeepsWalkingAwayFromTheDoorInside()
        {
            yield return LoadSpace("TownScene");
            Transform outside = FindDoorway("CafeDoor").ArrivalPoint;
            // 문을 등지고 서서 카메라를 등 뒤에 두면, 뒤로 가는 키(S)가 문 쪽으로 걷는다.
            FindPlayer().Teleport(outside.position, outside.rotation);
            Object.FindFirstObjectByType<PlayerCameraRig>().SnapBehindTarget();

            float oldDelta = Time.captureDeltaTime;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                .EditorInputBehaviorInPlayMode
                .AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                yield return WaitForArrival("CafeScene");
                Transform inside = FindDoorway("TownDoor").ArrivalPoint;
                for (int frame = 0; frame < 45; frame++)
                    yield return null;

                Assert.That(
                    SceneManager.GetActiveScene().name == "CafeScene" && !SpaceTravel.IsTraveling,
                    Is.True,
                    "Holding S must not walk back out of the cafe door."
                );
                Assert.That(
                    Vector3.Dot(FindPlayer().transform.position - inside.position, inside.forward),
                    Is.GreaterThan(1f),
                    "Holding S must keep walking away from the door."
                );
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                InputSystem.settings.backgroundBehavior = oldBackground;
                Time.captureDeltaTime = oldDelta;
            }
        }

        private static IEnumerator LoadSpace(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // 새 씬의 Start가 실행되도록 한 프레임 넘긴다.
            yield return null;
        }

        private static IEnumerator WaitForArrival(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName || SpaceTravel.IsTraveling)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"{sceneName}에 도착하지 못했습니다.");
                }

                yield return null;
            }

            // 도착한 뒤 한 프레임 동안 이동·카메라 갱신이 위치를 흐트러뜨리지 않는지 함께 본다.
            yield return null;
        }

        private static string[] LoadedSceneNames()
        {
            return Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).name)
                .ToArray();
        }

        /// <summary>날씨마다 바로 바꿔 보고, 화면에 쓰이는 하늘·환경광·안개 값을 글로 남긴다.</summary>
        // 서버가 보내는 것처럼 1일차 오전 8시로 시계를 맞추고, paused면 흐르지 않게 한다.
        // 앞선 신호의 다음 간격과 겹치지 않도록 매번 더 늦은 실제 시각을 넘긴다.
        private static double pinnedBeatAt = 1e9;

        private static void PinGameClock(bool paused)
        {
            pinnedBeatAt += GameClock.BeatIntervalSeconds;
            GameClock.Shared.TickAsync(new FixedGameClockApi(paused), pinnedBeatAt);
        }

        private sealed class FixedGameClockApi : IGameClockApi
        {
            private readonly bool paused;

            public FixedGameClockApi(bool paused)
            {
                this.paused = paused;
            }

            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            )
            {
                return Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = 8 * 60,
                        Paused = paused,
                    }
                );
            }
        }

        private static string ShowEveryWeatherLook()
        {
            var presenter = Object.FindFirstObjectByType<WeatherPresenter>();
            var looks = new List<string>();
            foreach (WeatherDefinition weather in presenter.Settings.Weathers)
            {
                presenter.ShowImmediately(weather.Code);
                Material sky = RenderSettings.skybox;
                looks.Add(
                    $"{weather.Code}: {SkyColor(sky, "_Zenith")} {SkyColor(sky, "_Horizon")} "
                        + $"{RenderSettings.ambientSkyColor} {RenderSettings.ambientEquatorColor} "
                        + $"{RenderSettings.ambientGroundColor} {RenderSettings.fogColor} "
                        + $"{RenderSettings.fogStartDistance:F2} {RenderSettings.fogEndDistance:F2}"
                );
            }

            return string.Join("\n", looks);
        }

        private static string SkyColor(Material sky, string property)
        {
            return sky != null && sky.HasProperty(property)
                ? sky.GetColor(property).ToString()
                : "none";
        }

        private static float CurtainOpacity()
        {
            var curtain = Object.FindFirstObjectByType<SpaceTravelCurtain>();
            return curtain != null ? curtain.Opacity : 0f;
        }

        private static IEnumerator WaitUntilCurtainOpens()
        {
            float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
            while (
                (SpaceTravel.IsTraveling || CurtainOpacity() > 0f)
                && Time.realtimeSinceStartup < deadline
            )
            {
                yield return null;
            }
        }

        private static void AssertPlayerArrivedAt(string doorwayId)
        {
            Transform arrivalPoint = FindDoorway(doorwayId).ArrivalPoint;
            Transform player = FindPlayer().transform;

            Vector3 offset = player.position - arrivalPoint.position;
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(PositionTolerance));
            Assert.That(
                Vector3.Angle(player.forward, arrivalPoint.forward),
                Is.LessThan(AngleToleranceDegrees)
            );

            Vector3 cameraForward = Vector3.ProjectOnPlane(
                Camera.main.transform.forward,
                Vector3.up
            );
            Assert.That(
                Vector3.Angle(cameraForward, player.forward),
                Is.LessThan(AngleToleranceDegrees),
                "카메라가 플레이어 뒤에서 같은 방향을 봐야 합니다."
            );
        }

        private static SpaceDoorway FindDoorway(string doorwayId)
        {
            return Object
                .FindObjectsByType<SpaceDoorway>(FindObjectsSortMode.None)
                .Single(doorway => doorway.DoorwayId == doorwayId);
        }

        private static PlayerMover FindPlayer()
        {
            return Object.FindFirstObjectByType<PlayerMover>();
        }
    }
}
