using System.Linq;
using A305.Mission;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    /// <summary>마을 벤치와 호텔 소파마다 카페 의자와 같은 앉기 자리가 두 개씩 연결됐는지.</summary>
    public sealed class BenchSofaSeatSceneTests
    {
        private static readonly string[] SceneReferences = { "player", "dialogue", "offerView" };

        [TestCase("Assets/Scenes/Production/TownScene.unity", "ParkBench", 4)]
        [TestCase("Assets/Scenes/Production/HotelScene.unity", "SageSofa", 2)]
        public void EveryBenchOrSofaHasTwoConnectedSeats(
            string path,
            string furnitureName,
            int furnitureCount
        )
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            try
            {
                RestSeat[] seats = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RestSeat>(true))
                    .ToArray();
                Transform[] furniture = seats
                    .Select(seat => seat.transform.parent)
                    .Distinct()
                    .ToArray();
                PlayerSeatPose[] poses = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerSeatPose>(true))
                    .ToArray();

                Assert.That(furniture.Select(item => item.name), Is.All.EqualTo(furnitureName));
                Assert.That(furniture, Has.Length.EqualTo(furnitureCount));
                Assert.That(seats, Has.Length.EqualTo(furnitureCount * 2));
                Assert.That(seats.All(seat => seat.gameObject.activeInHierarchy), Is.True);
                foreach (RestSeat seat in seats)
                {
                    var fields = new SerializedObject(seat);
                    foreach (string reference in SceneReferences)
                    {
                        Assert.That(
                            fields.FindProperty(reference).objectReferenceValue,
                            Is.Not.Null,
                            $"{seat.transform.parent.name}/{seat.name}.{reference}"
                        );
                    }
                }
                Assert.That(poses, Has.Length.EqualTo(1));
                Assert.That(poses[0].GetComponent<PlayerMover>(), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
