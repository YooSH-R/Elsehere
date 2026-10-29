#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    [TestFixture("MapleBakery", false)]
    [TestFixture("LittleArcade", false)]
    [TestFixture("HomeLiving", false)]
    [TestFixture("MapleBakery", true)]
    [TestFixture("LittleArcade", true)]
    [TestFixture("HomeLiving", true)]
    public sealed class NorthBlockTraversalTests
    {
        private readonly string buildingName;
        private readonly bool inTown;
        private GameObject building;
        private GameObject walker;
        private GameObject pavement;

        public NorthBlockTraversalTests(string buildingName, bool inTown)
        {
            this.buildingName = buildingName;
            this.inTown = inTown;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(walker);
            if (!inTown)
                Object.DestroyImmediate(building);
            Object.DestroyImmediate(pavement);
        }

        [UnityTest]
        public IEnumerator WideCharacterWalksUpAndDownWithoutJumping()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/NorthBlock/" + buildingName + ".prefab"
            );
            Assert.That(prefab, Is.Not.Null, "The enterable north-block bakery must be built.");
            if (inTown)
            {
                yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
                yield return null;
                building = GameObject.Find(
                    "Environment/TownMainstreet/BackgroundBuildings/" + buildingName
                );
                Assert.That(
                    building,
                    Is.Not.Null,
                    "The reviewed layout must also be installed in TownScene."
                );
            }
            else
            {
                building = Object.Instantiate(
                    prefab,
                    new Vector3(200, 0, 200),
                    Quaternion.identity
                );
                pavement = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pavement.transform.position = building.transform.position + new Vector3(0, -.1f, 3);
                pavement.transform.localScale = new Vector3(12, .2f, 6);
            }
            walker = new GameObject("TraversalCharacter");
            float entranceX =
                buildingName == "HomeLiving" ? 2.7f
                : buildingName == "LittleArcade" ? 2
                : 0;
            walker.transform.position = building.transform.TransformPoint(
                new Vector3(entranceX, .02f, 3.2f)
            );
            var controller = walker.AddComponent<CharacterController>();
            controller.height = 1.61f;
            controller.radius = .68f;
            controller.center = Vector3.up * .805f;
            controller.stepOffset = .15f;
            controller.skinWidth = .02f;
            Physics.SyncTransforms();
            yield return null;
            yield return WalkTo(controller, new Vector3(entranceX, 0, -.8f));

            if (buildingName == "HomeLiving")
            {
                Vector3[] exteriorRoute =
                {
                    new Vector3(2.7f, 0, 3.2f),
                    new Vector3(-4.6f, 0, 3.2f),
                    new Vector3(-4.6f, 0, 1.2f),
                    new Vector3(2.7f, 4, 1.2f),
                    new Vector3(2.7f, 4, -1),
                    new Vector3(0, 4, -1),
                };
                foreach (Vector3 point in exteriorRoute)
                    yield return WalkTo(controller, point);
                for (int index = exteriorRoute.Length - 2; index >= 0; index--)
                    yield return WalkTo(controller, exteriorRoute[index]);
                yield break;
            }

            // A shared stair hall must also let visitors enter the ground-floor tenant.
            yield return WalkTo(controller, new Vector3(-1.7f, 0, -.8f));

            Vector3[] route =
            {
                new Vector3(1.05f, 0, -.8f),
                new Vector3(1.05f, 2, -6.6f),
                new Vector3(2.95f, 2, -6.6f),
                new Vector3(2.95f, 4, -.8f),
                new Vector3(-1.7f, 4, -.8f),
            };
            foreach (Vector3 point in route)
                yield return WalkTo(controller, point);
            for (int index = route.Length - 2; index >= 0; index--)
                yield return WalkTo(controller, route[index]);
            yield return WalkTo(controller, new Vector3(entranceX, 0, -.8f));
            yield return WalkTo(controller, new Vector3(entranceX, 0, 3.2f));
        }

        private IEnumerator WalkTo(CharacterController controller, Vector3 localTarget)
        {
            Vector3 target = building.transform.TransformPoint(localTarget);
            for (int frame = 0; frame < 360; frame++)
            {
                Vector3 offset = target - walker.transform.position;
                offset.y = 0;
                controller.Move(Vector3.ClampMagnitude(offset, .045f) + Vector3.down * .04f);
                if (frame % 4 == 0)
                    yield return null;
                if (
                    offset.magnitude < .035f
                    && Mathf.Abs(walker.transform.position.y - target.y) < .08f
                )
                    break;
            }
            Assert.That(
                Vector3.Distance(walker.transform.position, target),
                Is.LessThan(.12f),
                "Walk route blocked at "
                    + localTarget
                    + "; actual="
                    + building.transform.InverseTransformPoint(walker.transform.position)
            );
        }
    }
}
#endif
