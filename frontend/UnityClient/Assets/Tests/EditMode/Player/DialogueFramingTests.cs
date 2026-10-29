using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    public class DialogueFramingTests
    {
        [TestCase(1.777778f)]
        [TestCase(.642857f)]
        public void FaceStaysAboveTheDialogueAndBesideTheHelpList(float aspect)
        {
            var eye = new Vector3(0, 1.6f, 0);
            var face = new Vector3(1, 1.1f, 3);
            var desired = new Vector2(.66f, .76f);
            Pose pose = DialogueFraming.Pose(eye, face, 60, aspect, desired);
            Vector3 local = Quaternion.Inverse(pose.rotation) * (face - pose.position);
            float halfHeight = local.z * Mathf.Tan(30 * Mathf.Deg2Rad);
            Assert.That(.5f + local.y / (2 * halfHeight), Is.EqualTo(desired.y).Within(.001f));
            Assert.That(
                .5f + local.x / (2 * halfHeight * aspect),
                Is.EqualTo(desired.x).Within(.001f)
            );
            Assert.That(pose.position, Is.EqualTo(eye));
            Assert.That(
                Vector3.Dot(pose.rotation * Vector3.right, Vector3.up),
                Is.EqualTo(0).Within(.001f),
                "대화 카메라의 수평선이 기울지 않아야 합니다."
            );
        }
    }
}
