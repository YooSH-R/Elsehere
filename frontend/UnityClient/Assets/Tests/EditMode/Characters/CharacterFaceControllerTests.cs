using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CharacterFaceControllerTests
    {
        [Test]
        public void IncompleteFaceRejectsExpressionBeforeChangingAnyRenderer()
        {
            var root = new GameObject("Missing face channel");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.AddBlendShapeFrame(
                "mouthSmileLeft",
                100,
                new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                null,
                null
            );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(0, 23);
                var face = root.AddComponent<CharacterFaceController>();
                Assert.Throws<System.InvalidOperationException>(() =>
                    face.SetPose(CharacterFacePose.Smile)
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(23));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ChangingExpressionResetsOnlyFaceChannelsAndKeepsCorrectiveWeights()
        {
            var root = new GameObject("Face");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            string[] channels =
            {
                "mouthSmileLeft",
                "mouthSmileRight",
                "eyeBlinkLeft",
                "eyeBlinkRight",
                "viseme_AA",
                "viseme_FF",
                "viseme_TH",
                "KneeCorrective",
            };
            foreach (string channel in channels)
                mesh.AddBlendShapeFrame(
                    channel,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(7, 32);
                var face = root.AddComponent<CharacterFaceController>();
                face.SetPose(CharacterFacePose.Smile, .5f);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(50));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(50));
                face.SetPose(CharacterFacePose.OpenMouth);
                Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(4), Is.EqualTo(100));
                face.SetPose(CharacterFacePose.Neutral);
                for (int i = 0; i < 7; i++)
                    Assert.That(skin.GetBlendShapeWeight(i), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(7), Is.EqualTo(32));
                Assert.That(mesh.vertices[0], Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
