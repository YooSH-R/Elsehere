using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A305.Mission.Tests
{
    public class CafeSeatingConnectionTests
    {
        [Test]
        public void CafeScene_EachBoothSofaHasTwoConnectedSeats()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Production/CafeScene.unity",
                OpenSceneMode.Additive
            );
            try
            {
                Transform[] sofas = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(item =>
                        item.name == "Bench"
                        && item.parent.name == "BoothSet"
                        && item.gameObject.activeInHierarchy
                    )
                    .ToArray();
                Assert.That(sofas, Is.Not.Empty);
                foreach (Transform sofa in sofas)
                {
                    RestSeat[] seats = sofa.GetComponentsInChildren<RestSeat>();
                    Assert.That(
                        seats,
                        Has.Length.EqualTo(2),
                        $"{sofa.parent.name} sofa has no seating."
                    );
                    foreach (RestSeat seat in seats)
                    {
                        var fields = new UnityEditor.SerializedObject(seat);
                        foreach (string reference in new[] { "player", "dialogue", "offerView" })
                            Assert.That(
                                fields.FindProperty(reference).objectReferenceValue,
                                Is.Not.Null
                            );
                        Assert.That(seat.CushionTopMeters, Is.EqualTo(.57f).Within(.001f));
                        Assert.That(seat.FrontEdgeMeters, Is.EqualTo(.44f).Within(.001f));
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

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
