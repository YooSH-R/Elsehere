using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CharacterBlinkTests
    {
        [Test]
        public void BlinkChangesOnlyEyelidsAndKeepsSpeechSmileAndCorrectiveWeights()
        {
            var root = new GameObject("Blink test");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (
                string channel in new[]
                {
                    "eyeBlinkLeft",
                    "eyeBlinkRight",
                    "viseme_AA",
                    "mouthSmileLeft",
                    "KneeCorrective",
                }
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
                skin.SetBlendShapeWeight(2, 73);
                skin.SetBlendShapeWeight(3, 21);
                skin.SetBlendShapeWeight(4, 32);
                var face = root.AddComponent<CharacterFaceController>();
                face.ApplyBlink(.65f);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(65).Within(.001));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(65).Within(.001));
                Assert.That(skin.GetBlendShapeWeight(2), Is.EqualTo(73));
                Assert.That(skin.GetBlendShapeWeight(3), Is.EqualTo(21));
                Assert.That(skin.GetBlendShapeWeight(4), Is.EqualTo(32));
                face.ApplyBlink(0);
                Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(1), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(1.34f, 1.34f)]
        [TestCase(1.31f, 1.37f)]
        public void IdleBlinkUsesExpressionTimingAndKeepsTheMouthPose(float previous, float current)
        {
            var root = new GameObject("Idle blink");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            foreach (string channel in new[] { "eyeBlinkLeft", "eyeBlinkRight", "viseme_AA" })
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
                skin.SetBlendShapeWeight(2, 73);
                root.AddComponent<CharacterFaceController>();
                var blink = root.AddComponent<CharacterIdleBlink>();
                blink.ApplyAtTime(previous);
                blink.ApplyAtTime(current);
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(100).Within(.001));
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(100).Within(.001));
                Assert.That(skin.GetBlendShapeWeight(2), Is.EqualTo(73));
                blink.ApplyAtTime(2f);
                Assert.That(skin.GetBlendShapeWeight(0), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
