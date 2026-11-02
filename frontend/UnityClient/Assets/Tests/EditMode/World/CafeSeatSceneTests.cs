using System.Linq;
using A305.Mission;
using A305.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.World.Tests
{
    public sealed class CafeSeatSceneTests
    {
        [TestCase("Assets/Scenes/Production/CafeScene.unity")]
        [TestCase("Assets/Scenes/Sandbox/CafeInteriorSandbox.unity")]
        public void CafeHasFourWorkingSeatMarkersAndPlayerPose(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            try
            {
                RestSeat[] seats = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RestSeat>(true))
                    .ToArray();
                PlayerSeatPose[] poses = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerSeatPose>(true))
                    .ToArray();

                Assert.That(seats, Has.Length.EqualTo(4));
                Assert.That(seats.All(seat => seat.gameObject.activeInHierarchy), Is.True);
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
