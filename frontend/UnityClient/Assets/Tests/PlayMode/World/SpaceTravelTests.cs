using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    /// <summary>배포용 공간 씬을 실제로 불러와 출입구 연결과 도착 위치를 확인한다.</summary>
    public class SpaceTravelTests
    {
        private const string ProductionSceneFolder = "Assets/Scenes/Production/";
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
            FindPlayer()
                .Teleport(
                    cafeDoor.position + cafeDoor.forward * 0.4f,
                    Quaternion.LookRotation(-cafeDoor.forward)
                );
            yield return WaitForArrival("CafeScene");

            AssertPlayerArrivedAt("TownDoor");
        }

        [UnityTest]
        public IEnumerator EverySceneInSceneList_HasOnePlayerCameraAndArrival()
        {
            // 디자인해서 추가한 공간 씬도 Scene List에 오르면 같은 기준으로 확인한다.
            foreach (string sceneName in SceneListNames())
            {
                yield return LoadSpace(sceneName);

                Assert.That(FindAll<PlayerMover>(), Has.Length.EqualTo(1), $"{sceneName}의 PlayerMover");
                Assert.That(FindAll<PlayerCameraRig>(), Has.Length.EqualTo(1), $"{sceneName}의 PlayerCameraRig");
                Assert.That(FindAll<SpaceArrival>(), Has.Length.EqualTo(1), $"{sceneName}의 SpaceArrival");
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
                if (scenePath.StartsWith(ProductionSceneFolder))
                {
                    yield return Path.GetFileNameWithoutExtension(scenePath);
                }
            }
        }

        private static T[] FindAll<T>()
            where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

            Vector3 cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
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
