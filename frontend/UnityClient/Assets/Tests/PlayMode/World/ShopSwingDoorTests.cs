#if UNITY_EDITOR
using System.Collections;
using A305.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace A305.World.PlayModeTests
{
    public sealed class ShopSwingDoorTests
    {
        private GameObject doorway;
        private GameObject visitor;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(visitor);
            Object.DestroyImmediate(doorway);
        }

        [UnityTest]
        public IEnumerator ApproachingCharacterOpensDoorAndLeavingClosesIt()
        {
            doorway = new GameObject("Doorway");
            doorway.transform.position = new Vector3(400, 0, 400);
            doorway.SetActive(false);
            var sensor = doorway.AddComponent<BoxCollider>();
            sensor.size = new Vector3(4, 3, 4);
            sensor.isTrigger = true;
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(doorway.transform, false);
            var leaf = hinge.gameObject.AddComponent<BoxCollider>();
            leaf.center = new Vector3(.8f, 0, 0);
            leaf.size = new Vector3(1.6f, 2.4f, .1f);
            var door = doorway.AddComponent<ShopSwingDoor>();
            var fields = new SerializedObject(door);
            fields.FindProperty("hinge").objectReferenceValue = hinge;
            fields.FindProperty("leafCollider").objectReferenceValue = leaf;
            fields.ApplyModifiedPropertiesWithoutUndo();
            doorway.SetActive(true);
            visitor = new GameObject("Visitor");
            visitor.transform.position = doorway.transform.position + new Vector3(0, 0, 3);
            var controller = visitor.AddComponent<CharacterController>();
            Assert.That(leaf.enabled, Is.True);
            Physics.SyncTransforms();
            controller.Move(Vector3.back * 1.5f);
            yield return new WaitForSeconds(.8f);
            Assert.That(
                Quaternion.Angle(Quaternion.identity, hinge.localRotation),
                Is.GreaterThan(85)
            );
            Assert.That(leaf.enabled, Is.False, "The open leaf must not obstruct the entrance.");
            controller.Move(Vector3.forward * 4);
            yield return new WaitForSeconds(.8f);
            Assert.That(Quaternion.Angle(Quaternion.identity, hinge.localRotation), Is.LessThan(1));
            Assert.That(leaf.enabled, Is.True);
        }
    }
}
#endif
