using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CharacterFaceControllerTests
    {
        [TestCase(0f)]
        [TestCase(.25f)]
        [TestCase(1f)]
        public void SpeechTransitionBlendsTwoMouthsAndKeepsCorrectiveWeight(float progress)
        {
            var root = new GameObject("Speech transition");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string channel in new[] { "viseme_PP", "viseme_AA", "viseme_FF", "KneeCorrective" }
            )
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
                skin.SetBlendShapeWeight(2, 48);
                skin.SetBlendShapeWeight(3, 32);
                var face = root.AddComponent<CharacterFaceController>();
                face.BlendPoses(
                    CharacterFacePose.ClosedLips,
                    CharacterFacePose.OpenMouth,
                    progress
                );
                Assert.That(
                    skin.GetBlendShapeWeight(0),
                    Is.EqualTo(100 * (1 - progress)).Within(.001f)
                );
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(100 * progress).Within(.001f));
                Assert.That(skin.GetBlendShapeWeight(2), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(3), Is.EqualTo(32));
                face.SetPose(CharacterFacePose.Neutral);
                Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(CharacterFacePose.ClosedLips, "viseme_PP")]
        [TestCase(CharacterFacePose.E, "viseme_E")]
        [TestCase(CharacterFacePose.OH, "viseme_OH")]
        [TestCase(CharacterFacePose.OU, "viseme_OU")]
        [TestCase(CharacterFacePose.WideI, "viseme_I")]
        [TestCase(CharacterFacePose.FlatEu, "viseme_EU")]
        public void SpeechPoseActivatesItsOwnShapeAndNeutralClearsIt(
            CharacterFacePose pose,
            string expectedChannel
        )
        {
            var root = new GameObject("Speech face");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            string[] channels =
            {
                "viseme_AA",
                "viseme_FF",
                "viseme_TH",
                "viseme_PP",
                "viseme_E",
                "viseme_OH",
                "viseme_OU",
                "viseme_I",
                "viseme_EU",
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
                int corrective = mesh.GetBlendShapeIndex("KneeCorrective");
                skin.SetBlendShapeWeight(corrective, 32);
                var face = root.AddComponent<CharacterFaceController>();
                face.SetPose(CharacterFacePose.OpenMouth);
                face.SetPose(pose, .7f);
                Assert.That(
                    skin.GetBlendShapeWeight(mesh.GetBlendShapeIndex(expectedChannel)),
                    Is.EqualTo(70).Within(.001f)
                );
                Assert.That(
                    skin.GetBlendShapeWeight(mesh.GetBlendShapeIndex("viseme_AA")),
                    Is.Zero
                );
                face.SetPose(CharacterFacePose.Neutral);
                for (int index = 0; index < channels.Length - 1; index++)
                    Assert.That(skin.GetBlendShapeWeight(index), Is.Zero, channels[index]);
                Assert.That(skin.GetBlendShapeWeight(corrective), Is.EqualTo(32));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

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
