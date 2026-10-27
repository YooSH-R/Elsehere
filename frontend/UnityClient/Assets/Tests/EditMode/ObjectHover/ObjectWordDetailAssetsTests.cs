using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordDetailAssetsTests
    {
        [TestCase("Home/CozyCottage/SageBed", -.44f, .78f, -.9f, false, "pillow")]
        [TestCase("Home/CozyCottage/SageBed", 0f, .7f, .2f, false, "duvet")]
        [TestCase("Home/CozyCottage/SageBed", 0f, .82f, .9f, false, "blanket")]
        [TestCase("Home/CozyCottage/KitchenCounter", -.8f, 1.08f, .1f, false, "sink")]
        [TestCase("Home/CozyCottage/KitchenCounter", -.57f, 1.43f, -.1f, false, "faucet")]
        [TestCase("Home/CozyCottage/KitchenCounter", .78f, 1.08f, 0f, false, "stove")]
        [TestCase("Home/CozyCottage/OakBookcase", -.55f, .335f, .07f, true, "book")]
        [TestCase("Home/CozyCottage/CoffeeTable", .23f, .63f, 0f, false, "book")]
        [TestCase("Home/CozyCottage/SideTable", 0f, .4f, .3f, true, "drawer")]
        [TestCase("Home/CozyCottage/CurtainedWindow", 1.13f, 0f, .17f, true, "curtain")]
        [TestCase("World/TinyTreats/HousePlants/Monstera", 0f, .08f, 0f, true, "flowerpot")]
        [TestCase("World/TinyTreats/HousePlants/SnakePlant", 0f, .06f, 0f, true, "flowerpot")]
        [TestCase("World/TinyTreats/HousePlants/Succulent", 0f, .035f, 0f, true, "flowerpot")]
        public void PrefabSurface_ChoosesDetailNearbyAndOverviewFarAway(
            string path,
            float x,
            float y,
            float z,
            bool front,
            string detail
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/" + path + ".prefab"
            );
            var instance = Object.Instantiate(prefab, new Vector3(5000, 0, 0), Quaternion.identity);
            try
            {
                var overview = instance.GetComponent<HoverableObject>();
                Assert.That(overview.Parts, Is.Not.Null, path + "의 세부 데이터 에셋이 없습니다.");
                Vector3 point = instance.transform.TransformPoint(new Vector3(x, y, z));
                Vector3 direction = front ? Vector3.forward : Vector3.up;
                Physics.SyncTransforms();
                var raycast = new ObjectHoverRaycast();
                HoverableObject hit = raycast.Find(
                    new Ray(point + direction * 3f, -direction),
                    6f,
                    ~0,
                    null,
                    out Vector3 surface
                );
                Assert.That(hit, Is.SameAs(overview), path + "의 보이는 표면을 선택해야 합니다.");
                Vector3 localPoint = instance.transform.InverseTransformPoint(surface);
                int index = overview.Parts.FindPart(localPoint, 2f, 3f);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), path + " surface " + localPoint);
                Assert.That(overview.Parts.Parts[index].Word.Key, Is.EqualTo(detail));
                Assert.That(overview.Parts.FindPart(localPoint, 4f, 3f), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
