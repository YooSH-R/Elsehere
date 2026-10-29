using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class SpeechExpressionTests
    {
        [Test]
        public void BlinkAndSmileDoNotReplaceSpeechOrJointCorrections()
        {
            var root = new GameObject("Expression test");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.up, Vector3.right },
                triangles = new[] { 0, 1, 2 },
            };
            string[] names =
            {
                "viseme_AA",
                "viseme_PP",
                "mouthSmileLeft",
                "mouthSmileRight",
                "eyeBlinkLeft",
                "eyeBlinkRight",
                "KneeCorrective",
            };
            foreach (string name in names)
                mesh.AddBlendShapeFrame(
                    name,
                    100,
                    new[] { Vector3.up * .01f, Vector3.zero, Vector3.zero },
                    null,
                    null
                );
            try
            {
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(6, 73);
                var face = root.AddComponent<CharacterFaceController>();
                face.ApplySpeech(
                    SpeechMouthSample.Pose(CharacterFacePose.OpenMouth, .6f),
                    .3f,
                    .8f
                );
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(60).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(4), Is.EqualTo(80).Within(.01));
                Assert.That(skin.GetBlendShapeWeight(2), Is.InRange(1, 30));
                Assert.That(skin.GetBlendShapeWeight(6), Is.EqualTo(73));
                face.ApplySpeech(SpeechMouthSample.Pose(CharacterFacePose.ClosedLips), .4f, .8f);
                Assert.That(skin.GetBlendShapeWeight(1), Is.EqualTo(100));
                Assert.That(
                    skin.GetBlendShapeWeight(2),
                    Is.Zero,
                    "Smile must not pull apart a sealed P/B/M."
                );
                Assert.That(skin.GetBlendShapeWeight(4), Is.EqualTo(80).Within(.01));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void BlinkHasClosingHoldOpeningAndExpressionReturnsToRest()
        {
            var profile = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
            try
            {
                Assert.That(profile.Sample(.8f, 3.825f).Smile, Is.GreaterThan(.15f));
                Assert.That(profile.Sample(1.25f, 3.825f).Blink, Is.Zero);
                Assert.That(profile.Sample(1.285f, 3.825f).Blink, Is.InRange(.1f, .9f));
                Assert.That(profile.Sample(1.33f, 3.825f).Blink, Is.GreaterThan(.95f));
                Assert.That(profile.Sample(1.39f, 3.825f).Blink, Is.InRange(.1f, .9f));
                Assert.That(profile.Sample(1.5f, 3.825f).Blink, Is.Zero);
                Assert.That(profile.Sample(3.825f, 3.825f).Smile, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
