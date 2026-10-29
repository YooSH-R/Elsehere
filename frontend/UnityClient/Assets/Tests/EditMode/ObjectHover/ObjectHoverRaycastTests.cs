using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectHoverRaycastTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Find_CupInsideFurnitureMovementBounds_RespectsVisibleOcclusion(bool blocked)
        {
            var furniture = new GameObject("Furniture");
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                furniture.SetActive(false);
                furniture.transform.position = new Vector3(5000, 0, 5);
                SetWord(furniture, "table");
                furniture.AddComponent<BoxCollider>().size = new Vector3(4, 4, 8);
                var surface = new GameObject("VisibleSurface");
                surface.transform.SetParent(furniture.transform, false);
                surface.transform.localPosition = Vector3.forward * 2;
                surface.AddComponent<BoxCollider>().isTrigger = true;
                surface.AddComponent<ObjectHoverHitArea>();
                furniture.SetActive(true);

                cup.SetActive(false);
                cup.transform.position = new Vector3(5000, 0, 4);
                HoverableObject word = SetWord(cup, "mug");
                cup.AddComponent<ObjectHoverHitArea>();
                cup.GetComponent<BoxCollider>().isTrigger = true;
                cup.SetActive(true);
                wall.transform.position = new Vector3(5000, 0, 2);
                wall.SetActive(blocked);
                Physics.SyncTransforms();

                HoverableObject found = new ObjectHoverRaycast().Find(
                    new Ray(new Vector3(5000, 0, 0), Vector3.forward),
                    20,
                    ~0,
                    null,
                    out _
                );
                Assert.That(found, Is.SameAs(blocked ? null : word));
            }
            finally
            {
                Object.DestroyImmediate(furniture);
                Object.DestroyImmediate(cup);
                Object.DestroyImmediate(wall);
            }
        }

        private static HoverableObject SetWord(GameObject target, string key)
        {
            HoverableObject word = target.AddComponent<HoverableObject>();
            var fields = new SerializedObject(word);
            fields.FindProperty("unityKey").stringValue = key;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return word;
        }

        [Test]
        public void Find_HoverOnlyTrigger_ReturnsSharedWordObject()
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                item.SetActive(false);
                item.transform.position = new Vector3(5000, 0, 5);
                HoverableObject word = item.AddComponent<HoverableObject>();
                var fields = new SerializedObject(word);
                fields.FindProperty("unityKey").stringValue = "bench";
                fields.ApplyModifiedPropertiesWithoutUndo();
                item.AddComponent<ObjectHoverHitArea>();
                item.GetComponent<BoxCollider>().isTrigger = true;
                item.SetActive(true);
                Physics.SyncTransforms();

                var raycast = new ObjectHoverRaycast();
                HoverableObject found = raycast.Find(
                    new Ray(new Vector3(5000, 0, 0), Vector3.forward),
                    10,
                    ~0,
                    null,
                    out _
                );

                Assert.That(found, Is.SameAs(word));
                Assert.That(found.UnityKey, Is.EqualTo("bench"));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }
    }
}
