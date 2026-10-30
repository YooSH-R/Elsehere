using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Player;
using NUnit.Framework;
using UnityEngine;
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

        [UnityTest]
        public IEnumerator TryTravel_HomeToTown_PlacesPlayerAtTownHomeDoor()
        {
            yield return LoadSpace("HomeScene");

            Assert.That(SpaceTravel.TryTravel("TownScene", "HomeDoor"), Is.True);
            yield return WaitForArrival("TownScene");

            AssertPlayerArrivedAt("HomeDoor");
        }

        [UnityTest]
        public IEnumerator TryTravel_WhileTraveling_RejectsSecondRequest()
        {
            yield return LoadSpace("TownScene");

            Assert.That(SpaceTravel.TryTravel("CafeScene", "TownDoor"), Is.True);
            Assert.That(SpaceTravel.TryTravel("HotelScene", "TownDoor"), Is.False);
            yield return WaitForArrival("CafeScene");

            AssertPlayerArrivedAt("TownDoor");
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
        public IEnumerator EverySceneInSceneList_HasOnePlayerCameraAndArrival()
        {
            // 디자인해서 추가한 공간 씬도 Scene List에 오르면 같은 기준으로 확인한다.
            foreach (string sceneName in SceneListNames())
            {
                yield return LoadSpace(sceneName);

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
        }

        [UnityTest]
        public IEnumerator EveryDoorway_LeadsToDoorwayThatLeadsBack()
        {
            var doorways = new List<DoorwayLink>();
            foreach (string sceneName in SceneListNames())
            {
                yield return LoadSpace(sceneName);
                doorways.AddRange(
                    Object
                        .FindObjectsByType<SpaceDoorway>(FindObjectsSortMode.None)
                        .Select(doorway => new DoorwayLink(sceneName, doorway))
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
