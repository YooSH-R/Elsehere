using A305.EditorTools.Tripo;
using NUnit.Framework;
using UnityEngine;

namespace A305.Tripo.Tests
{
    public class TripoPartsTests
    {
        [Test]
        public void Attach_SkinnedPart_RebindsCommonBonesAndRejectsDifferentRestPose()
        {
            var character = new GameObject("Character");
            var bodyBone = new GameObject("Hips");
            bodyBone.transform.SetParent(character.transform, false);
            var part = new GameObject("Clothing");
            var partBone = new GameObject("Hips");
            partBone.transform.SetParent(part.transform, false);
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                bindposes = new[] { Matrix4x4.identity },
            };
            var skin = part.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { partBone.transform };
            skin.rootBone = partBone.transform;
            try
            {
                var slot = new TripoPartSlot
                {
                    model = part,
                    attachment = TripoAttachmentKind.Skinned,
                };
                var attached = TripoParts.Attach(character, slot);
                Assert.That(
                    attached.GetComponent<SkinnedMeshRenderer>().bones[0],
                    Is.SameAs(bodyBone.transform)
                );
                Assert.That(
                    attached.GetComponent<SkinnedMeshRenderer>().rootBone,
                    Is.SameAs(bodyBone.transform)
                );
                Assert.That(
                    skin.bones[0],
                    Is.SameAs(partBone.transform),
                    "원본 본 연결은 유지합니다."
                );
                Object.DestroyImmediate(attached);
                bodyBone.transform.localPosition = Vector3.up;
                Assert.Throws<System.InvalidOperationException>(() =>
                    TripoParts.Attach(character, slot)
                );
            }
            finally
            {
                Object.DestroyImmediate(part);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Attach_Earrings_UsesIndependentPositiveOffsetsAndRejectsFaceSurface()
        {
            var character = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(character.transform, false);
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var left = new TripoPartSlot
                {
                    name = "LeftEar",
                    bonePath = "Head",
                    model = part,
                    position = new Vector3(-0.1f, 0f, 0f),
                    rotation = new Vector3(0f, 25f, 0f),
                };
                var right = new TripoPartSlot
                {
                    name = "RightEar",
                    bonePath = "Head",
                    model = part,
                    position = new Vector3(0.1f, 0f, 0f),
                    rotation = new Vector3(0f, -25f, 0f),
                };
                var leftObject = TripoParts.Attach(character, left);
                var rightObject = TripoParts.Attach(character, right);
                Assert.That(leftObject, Is.Not.Null);
                Assert.That(rightObject, Is.Not.SameAs(leftObject));
                Assert.That(leftObject.transform.parent, Is.SameAs(head.transform));
                Assert.That(leftObject.transform.localPosition.x, Is.LessThan(0f));
                Assert.That(rightObject.transform.localPosition.x, Is.GreaterThan(0f));
                Assert.That(leftObject.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rightObject.transform.localScale, Is.EqualTo(Vector3.one));
                right.scale = -1f;
                Assert.Throws<System.InvalidOperationException>(() =>
                    TripoParts.Attach(character, right)
                );
                right.scale = 1f;
                right.attachment = TripoAttachmentKind.FaceSurface;
                Assert.Throws<System.InvalidOperationException>(() =>
                    TripoParts.Attach(character, right)
                );
            }
            finally
            {
                Object.DestroyImmediate(part);
                Object.DestroyImmediate(character);
            }
        }
    }
}
