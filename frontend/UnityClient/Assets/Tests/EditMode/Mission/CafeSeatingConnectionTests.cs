using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.Mission.Tests
{
    public class CafeSeatingConnectionTests
    {
        [Test]
        public void CafeScene_ChairsOnBothSidesHaveAnInteractableSeat()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/CafeScene.unity",
                OpenSceneMode.Additive
            );
            try
            {
                Transform[] chairs = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(item => item.name == "CafeChair" && item.gameObject.activeInHierarchy)
                    .ToArray();
                RestSeat[] seats = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RestSeat>(true))
                    .ToArray();
                Assert.That(chairs, Is.Not.Empty);
                foreach (Transform chair in chairs)
                {
                    Assert.That(
                        seats.Any(seat =>
                            Vector3.Distance(seat.transform.position, chair.position) < .08f
                        ),
                        Is.True,
                        $"A chair in {chair.parent.name} has no seat interaction."
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
