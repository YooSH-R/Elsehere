using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class CorrectivePoseWeightTests
    {
        [Test]
        public void SignedHipCorrectionRejectsOppositeBendAndQuaternionSignDoesNotMatter()
        {
            Quaternion forward = Quaternion.AngleAxis(30f, Vector3.left);
            Assert.That(
                CorrectivePoseWeight.Evaluate(
                    Quaternion.identity,
                    forward,
                    Vector3.left,
                    0f,
                    60f,
                    false
                ),
                Is.EqualTo(50f).Within(0.001f)
            );
            Assert.That(
                CorrectivePoseWeight.Evaluate(
                    Quaternion.identity,
                    Quaternion.Inverse(forward),
                    Vector3.left,
                    0f,
                    60f,
                    false
                ),
                Is.Zero
            );
            Quaternion sameRotation = new Quaternion(
                -forward.x,
                -forward.y,
                -forward.z,
                -forward.w
            );
            Assert.That(
                CorrectivePoseWeight.Evaluate(
                    Quaternion.identity,
                    sameRotation,
                    Vector3.left,
                    0f,
                    60f,
                    false
                ),
                Is.EqualTo(50f).Within(0.001f)
            );
        }

        [Test]
        public void KneeBendInterpolatesFromReferencePoseAndClampsAtFullCorrection()
        {
            Quaternion reference = Quaternion.Euler(23f, 41f, -17f);
            float weight = CorrectivePoseWeight.Evaluate(
                reference,
                reference * Quaternion.AngleAxis(55f, Vector3.right),
                Vector3.right,
                10f,
                100f,
                true
            );
            Assert.That(weight, Is.EqualTo(50f).Within(0.001f));
            Assert.That(
                CorrectivePoseWeight.Evaluate(reference, reference, Vector3.right, 10f, 100f, true),
                Is.Zero
            );
            Assert.That(
                CorrectivePoseWeight.Evaluate(
                    reference,
                    reference * Quaternion.AngleAxis(120f, Vector3.right),
                    Vector3.right,
                    10f,
                    100f,
                    true
                ),
                Is.EqualTo(100f)
            );
        }
    }
}
