using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string ProductionSceneFolder = "Assets/Scenes/Production/";

        // 입구 화면은 공간이 아니라 플레이어·카메라·출입구가 없다. Scene List 맨 앞에 있지만 공간 씬 검사에서 뺀다.
        private const string EntrySceneName = "EntryScene";
        private const float ArrivalTimeoutSeconds = 5f;
        private const float PositionTolerance = 0.05f;
        private const float AngleToleranceDegrees = 1f;

        // 튜토리얼 동안에는 단계를 마칠 때까지 장소 이동을 잠그므로, 이동 자체를 보는 이 시험은 튜토리얼을 마친 사용자로 한다.
        [SetUp]
        public void StartAfterTheTutorial()
        {
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [TearDown]
        public void ClearTutorial()
        {
            TutorialState.Clear();
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
        public IEnumerator TryTravel_CafeToTown_KeepsACameraAndShowsWeatherLikeOpeningTown()
        {
            yield return LoadSpace("TownScene");
            string opened = ShowEveryWeatherLook();
            yield return LoadSpace("CafeScene");
            int framesWithoutCamera = 0;

            Assert.That(SpaceTravel.TryTravel("TownScene", "CafeDoor"), Is.True);
            float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != "TownScene" || SpaceTravel.IsTraveling)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("TownScene에 도착하지 못했습니다.");
                }

                if (Camera.allCamerasCount == 0)
                {
                    framesWithoutCamera++;
                }

                yield return null;
            }

            // 그리는 카메라가 없으면 Editor는 덮개 대신 "No cameras rendering"을 띄운다.
            Assert.That(framesWithoutCamera, Is.Zero);
            yield return WaitUntilCurtainOpens();

            // 날씨는 어디서 들어왔든 마을 씬에 디자인한 하늘·환경광을 기준으로 바뀌어야 한다.
            Assert.That(ShowEveryWeatherLook(), Is.EqualTo(opened));
        }

        [UnityTest]
        public IEnumerator LittleArcade_WalkThroughOpenHallAndEnterOriginalDoor()
        {
            yield return LoadSpace("TownScene");
            Transform arcade = GameObject.Find("LittleArcade").transform;
            var player = FindPlayer();
            Vector3 street = arcade.TransformPoint(new Vector3(2f, .05f, 2f));
            player.Teleport(street, Quaternion.LookRotation(-arcade.forward));
            Assert.That(
                arcade.Find("TenantEntrances/ArcadeFrontDoor"),
                Is.Null,
                "오락실 외벽은 기존 창문을 유지해야 합니다."
            );
            Assert.That(
                arcade.Find("TenantEntrances/StairHallDoor"),
                Is.Null,
                "계단실 입구는 열린 통로여야 합니다."
            );
            yield return WalkForward(
                player,
                -arcade.forward,
                () => arcade.InverseTransformPoint(player.transform.position).z <= -.88f
            );
            Assert.That(
                arcade.InverseTransformPoint(player.transform.position).z,
                Is.LessThan(-.8f),
                "인도에서 공용 계단실로 걸어 들어가지 못했습니다."
            );
            Assert.That(SpaceTravel.IsTraveling, Is.False);
            foreach (float side in new[] { -1f, 1f })
            {
                yield return WalkForward(
                    player,
                    side * arcade.right,
                    () => SpaceTravel.IsTraveling
                );
                Assert.That(
                    SpaceTravel.IsTraveling,
                    Is.True,
                    "계단실 안 원래 오락실 문에서 전환되지 않았습니다."
                );
                yield return WaitUntilCurtainOpens();
                Assert.That(
                    arcade.InverseTransformPoint(player.transform.position).x * side,
                    Is.GreaterThan(.4f)
                );
            }
        }

        [UnityTest]
        public IEnumerator OriginalShopDoors_AllowWalkingInAndOutWithMovementKeys()
        {
            yield return LoadSpace("TownScene");
            var player = FindPlayer();
            var failures = new List<string>();
            var doors = Object
                .FindObjectsByType<ShopDoorway>(FindObjectsSortMode.None)
                .Select(door => door.transform)
                .Append(GameObject.Find("PoliceStationDoor").transform)
                .ToArray();
            Assert.That(doors, Has.Length.EqualTo(12));
            foreach (var door in doors)
            {
                var room = door.GetComponentInParent<ShopInterior>();
                Assert.That(room, Is.Not.Null, door.name);
                Vector3 center = ShopThresholdCenter(door);
                center.y = door.transform.position.y + .03f;
                bool hallDoor = door.name == "ArcadeDoor" || door.name == "BookshopDoor";
                Vector3 inward = hallDoor ? door.transform.forward : -door.transform.forward;
                float approach = hallDoor ? 1.3f : 1.65f;
                player.Teleport(center - inward * approach, Quaternion.LookRotation(inward));
                foreach (var direction in new[] { inward, -inward })
                {
                    yield return WalkForward(player, direction, () => SpaceTravel.IsTraveling);
                    if (!SpaceTravel.IsTraveling)
                    {
                        failures.Add(
                            $"{door.name}: {door.transform.InverseTransformPoint(player.transform.position)} / lock={SpaceTravel.IsLocked}, movement={player.IsMovementLocked}"
                        );
                        break;
                    }
                    yield return WaitUntilCurtainOpens();
                    Assert.That(
                        Vector3.Dot(player.transform.position - center, direction),
                        Is.GreaterThan(.4f),
                        door.name
                    );
                    Assert.That(
                        room.IsLoaded,
                        Is.EqualTo(direction == inward),
                        $"{door.name}: {room.SceneName}"
                    );
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator PostOffice_StreamsOnlyDuringTheVisitAfterSkippingTownTutorial()
        {
            TutorialState.Clear();
            yield return LoadSpace("TownScene");
            TutorialState.GetOrCreate("TOWN_PASSERBY").Skip();
            yield return null;
            yield return null;
            Assert.That(
                GameObject.Find("PoBoxWall"),
                Is.Null,
                "거리에서 우체국 실내를 로드하지 않아야 합니다."
            );
            Transform door = GameObject.Find("PostOfficeDoor").transform;
            ShopInterior room = door.GetComponentInParent<ShopInterior>();
            Assert.That(room, Is.Not.Null, "우체국 문이 실내 씬과 연결되어야 합니다.");
            Assert.That(room.SceneName, Is.EqualTo("PostOfficeInterior"));
            var player = FindPlayer();
            Vector3 center = door.TransformPoint(door.GetComponent<BoxCollider>().center);
            center.y = door.position.y + .02f;
            player.Teleport(center + 2f * door.forward, Quaternion.LookRotation(-door.forward));
            foreach (int side in new[] { -1, 1 })
            {
                yield return WalkForward(
                    player,
                    side * door.forward,
                    () => SpaceTravel.IsTraveling
                );
                Assert.That(
                    SpaceTravel.IsTraveling,
                    Is.True,
                    $"우체국 문 앞에서 전환되지 않았습니다. 거리={Vector3.Dot(player.transform.position - center, door.forward)}, 장소 잠금={SpaceTravel.IsLocked}, 이동 잠금={player.IsMovementLocked}"
                );
                yield return WaitUntilCurtainOpens();
                Assert.That(
                    Vector3.Dot(player.transform.position - center, door.forward) * side,
                    Is.GreaterThan(.4f)
                );
                bool inside = side < 0;
                Assert.That(
                    SceneManager.GetSceneByName("PostOfficeInterior").isLoaded,
                    Is.EqualTo(inside),
                    $"loaded={string.Join(", ", LoadedSceneNames())}, side={side}, traveling={SpaceTravel.IsTraveling}"
                );
                Assert.That(GameObject.Find("PoBoxWall") != null, Is.EqualTo(inside));
                if (inside)
                {
                    var lamp = SceneManager
                        .GetSceneByName("PostOfficeInterior")
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Light>())
                        .Single(light => light.name == "InteriorLight");
                    var weather = Object.FindFirstObjectByType<WeatherPresenter>();
                    weather.ShowTime(12 * 60);
                    float noonIntensity = lamp.intensity;
                    weather.ShowTime(0);
                    Assert.That(
                        lamp.intensity,
                        Is.GreaterThan(noonIntensity),
                        "방문 중인 매장 조명도 마을의 밤 설정을 따라야 합니다."
                    );
                }
            }
        }

        private static IEnumerator WalkForward(
            PlayerMover player,
            Vector3 direction,
            System.Func<bool> arrived
        )
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            float previousStep = Time.captureDeltaTime;
            Keyboard keyboard = null;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                InputSystem.settings.backgroundBehavior = InputSettings
                    .BackgroundBehavior
                    .IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings
                    .EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                yield return null;
                yield return null;
                player.CameraRig.SnapView(Quaternion.LookRotation(direction).eulerAngles.y);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (int frame = 0; frame < 100 && !arrived(); frame++)
                {
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                Time.captureDeltaTime = previousStep;
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
            }
        }

        [UnityTest]
        public IEnumerator TownShopDoors_CoverTheScreenWhenEnteringAndLeaving()
        {
            yield return LoadSpace("TownScene");
            string[] doorNames =
            {
                "PostOfficeDoor",
                "MapleDinerDoor",
                "TownBankDoor",
                "SudsLaundromatDoor",
                "BloomFlowersDoor",
                "MainStreetBarberDoor",
                "PoliceStationDoor",
                "BakeryDoor",
                "ArcadeDoor",
                "BookshopDoor",
                "HomeGoodsDoor",
                "TeaRoomDoor",
            };
            var player = FindPlayer();
            foreach (string name in doorNames)
            {
                var door = GameObject.Find(name).transform;
                Vector3 center = ShopThresholdCenter(door);
                center.y = door.position.y;
                Vector3 normal = door.forward;
                foreach (int side in new[] { 1, -1 })
                {
                    player.Teleport(
                        center + normal * (side * 0.45f),
                        Quaternion.LookRotation(-side * normal)
                    );
                    float deadline = Time.realtimeSinceStartup + ArrivalTimeoutSeconds;
                    while (Vector3.Dot(player.transform.position - center, normal) * side > 0f)
                    {
                        Assert.That(
                            Time.realtimeSinceStartup,
                            Is.LessThan(deadline),
                            $"{name} 문을 건너지 못했습니다 ({side})."
                        );
                        yield return null;
                    }
                    Assert.That(CurtainOpacity(), Is.EqualTo(1f), $"{name} 이동 순간");
                    Assert.That(player.IsMovementLocked, Is.True, name);
                    yield return WaitUntilCurtainOpens();
                    Assert.That(CurtainOpacity(), Is.Zero, name);
                    Assert.That(player.IsMovementLocked, Is.False, name);
                    Assert.That(
                        Mathf.Abs(player.transform.position.y - center.y),
                        Is.LessThan(0.45f),
                        $"{name} 도착 층"
                    );
                    Assert.That(
                        Vector3.Dot(player.transform.position - center, normal) * side,
                        Is.LessThan(-0.5f),
                        $"{name} 도착 방향"
                    );
                    yield return new WaitForFixedUpdate();
                }
            }
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

        [UnityTest]
        public IEnumerator EverySceneInSceneList_HasOnePlayerCameraArrival_AndDoorwaysThatLeadBack()
        {
            // 디자인해서 추가한 공간 씬도 Scene List에 오르면 같은 기준으로 확인한다.
            var doorways = new List<DoorwayLink>();
            foreach (string sceneName in SceneListNames())
            {
                yield return LoadSpace(sceneName);
                doorways.AddRange(
                    Object
                        .FindObjectsByType<SpaceDoorway>(FindObjectsSortMode.None)
                        .Select(doorway => new DoorwayLink(sceneName, doorway))
                );

                Assert.That(
                    FindAll<PlayerMover>(),
                    Has.Length.EqualTo(1),
                    $"{sceneName}의 PlayerMover"
                );
                Assert.That(
                    FindAll<PlayerCameraRig>(),
                    Has.Length.EqualTo(1),
                    $"{sceneName}의 PlayerCameraRig"
                );
                Assert.That(
                    FindAll<SpaceArrival>(),
                    Has.Length.EqualTo(1),
                    $"{sceneName}의 SpaceArrival"
                );
            }

            Assert.That(doorways, Is.Not.Empty);
            foreach (DoorwayLink doorway in doorways)
            {
                List<DoorwayLink> destinations = doorways
                    .Where(candidate =>
                        candidate.SceneName == doorway.DestinationScene
                        && candidate.DoorwayId == doorway.DestinationDoorwayId
                    )
                    .ToList();

                Assert.That(destinations, Has.Count.EqualTo(1), $"{doorway}의 도착 출입구");
                Assert.That(destinations[0].DestinationScene, Is.EqualTo(doorway.SceneName));
                Assert.That(destinations[0].DestinationDoorwayId, Is.EqualTo(doorway.DoorwayId));
            }
        }

        // Test Runner가 실행 중에 넣는 임시 씬은 빼고, Scene List의 배포용 씬만 확인한다.
        private static IEnumerable<string> SceneListNames()
        {
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(index);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                if (scenePath.StartsWith(ProductionSceneFolder) && sceneName != EntrySceneName)
                {
                    yield return sceneName;
                }
            }
        }

        private static T[] FindAll<T>()
            where T : Object
        {
            return Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }

        private static Vector3 ShopThresholdCenter(Transform door)
        {
            if (door.TryGetComponent(out BoxCollider sensor))
                return door.TransformPoint(sensor.center);
            Transform outside = door.parent.Find("OutsideDoorway");
            Transform inside = door.parent.Find("InsideDoorway");
            return (outside.position + inside.position) / 2f;
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
        private static string ShowEveryWeatherLook()
        {
            var presenter = Object.FindFirstObjectByType<WeatherPresenter>();
            // 게임 시계는 실제 시간으로 흐르므로, 두 번 볼 사이에 해가 지면 하늘색이 달라진다. 같은 시각에서 비교한다.
            presenter.ShowTime(12 * 60);
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

        private readonly struct DoorwayLink
        {
            public DoorwayLink(string sceneName, SpaceDoorway doorway)
            {
                SceneName = sceneName;
                DoorwayId = doorway.DoorwayId;
                DestinationScene = doorway.DestinationScene;
                DestinationDoorwayId = doorway.DestinationDoorwayId;
            }

            public string SceneName { get; }

            public string DoorwayId { get; }

            public string DestinationScene { get; }

            public string DestinationDoorwayId { get; }

            public override string ToString()
            {
                return $"{SceneName}/{DoorwayId} → {DestinationScene}/{DestinationDoorwayId}";
            }
        }
    }
}
