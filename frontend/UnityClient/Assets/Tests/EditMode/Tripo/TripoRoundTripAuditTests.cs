using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoRoundTripAuditTests
    {
        [Test]
        public void Compare_ReportsLostFaceShapeAndDriftedSocketWithoutChangingOffsets()
        {
            var before = CreateModel(true);
            var after = CreateModel(false);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var slot = new TripoPartSlot
            {
                name = "LeftEar",
                bonePath = "Head",
                position = Vector3.left * 0.1f,
            };
            after.transform.Find("Head").localPosition += Vector3.up * 0.01f;
            try
            {
                var report = TripoRoundTripAudit.Compare(before, after, new[] { slot }, profile);
                Assert.That(report.Errors, Has.Some.Contains("mouthSmileLeft"));
                Assert.That(report.Errors, Has.Some.Contains("LeftEar"));
                Assert.That(slot.position, Is.EqualTo(Vector3.left * 0.1f));
            }
            finally
            {
                Object.DestroyImmediate(before.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                Object.DestroyImmediate(after.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Compare_ReportsScaledSocketEvenWhenOffsetIsZero()
        {
            var before = CreateModel(true);
            var after = CreateModel(true);
            var profile = ScriptableObject.CreateInstance<TripoValidationProfile>();
            var slot = new TripoPartSlot { name = "HeadAccessory", bonePath = "Head" };
            var changedHead = after.transform.Find("Head");
            changedHead.localScale = Vector3.one * 100f;
            try
            {
                var report = TripoRoundTripAudit.Compare(before, after, new[] { slot }, profile);
                Assert.That(report.Errors, Has.Some.Contains("HeadAccessory"));
                Assert.That(slot.position, Is.EqualTo(Vector3.zero));
                Assert.That(slot.scale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(before.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                Object.DestroyImmediate(after.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
                Object.DestroyImmediate(profile);
            }
        }

        private static GameObject CreateModel(bool withShape)
        {
            var root = new GameObject("Model");
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = Vector3.up * 0.5f;
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                bindposes = new[] { head.worldToLocalMatrix },
            };
            if (withShape)
                mesh.AddBlendShapeFrame(
                    "mouthSmileLeft",
                    100f,
                    new[] { Vector3.up * 0.1f, Vector3.zero, Vector3.zero },
                    new Vector3[3],
                    new Vector3[3]
                );
            var skin = root.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { head };
            skin.rootBone = head;
            skin.localBounds = new Bounds(Vector3.up * 0.5f, Vector3.one);
            return root;
        }
    }
}
