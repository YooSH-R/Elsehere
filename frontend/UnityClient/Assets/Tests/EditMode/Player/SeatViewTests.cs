using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>앉은 캐릭터를 플레이어가 다가온 쪽에서 보도록 카메라 방향을 정한다.</summary>
    public class SeatViewTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void CameraYaw_LooksAtTheSeatFromWhereThePlayerApproached()
        {
            // 자리 왼쪽(-x)에서 다가오면 카메라도 그쪽에서 자리를 바라본다.
            float yaw = SeatView.CameraYawDegrees(new Vector3(-1f, 0f, 0f), Vector3.zero, 0f);

            AssertYaw(90f, yaw);
        }

        [Test]
        public void CameraYaw_ApproachedFromTheFront_FacesTheSeatedCharacter()
        {
            // 앞(+z)을 보는 벤치에 앞에서 다가오면 앉은 캐릭터를 얼굴 쪽에서 본다.
            float yaw = SeatView.CameraYawDegrees(
                new Vector3(.4f, 0f, 1.2f),
                new Vector3(.4f, 0f, 0f),
                0f
            );

            AssertYaw(180f, yaw);
        }

        [Test]
        public void CameraYaw_IgnoresHeightDifference()
        {
            // 보도 위 벤치처럼 자리와 서 있던 곳의 높이가 달라도 수평 방향만 본다.
            float yaw = SeatView.CameraYawDegrees(
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, .1f, 0f),
                0f
            );

            AssertYaw(270f, yaw);
        }

        [Test]
        public void CameraYaw_WithoutApproachDirection_LooksFromTheFrontOfTheSeat()
        {
            // 자리 바로 위에서 앉아 다가온 방향을 알 수 없으면 자리 앞쪽에서 본다.
            float yaw = SeatView.CameraYawDegrees(
                new Vector3(2f, 0f, 3f),
                new Vector3(2f, .5f, 3f),
                90f
            );

            AssertYaw(270f, yaw);
        }

        private static void AssertYaw(float expected, float actual)
        {
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual, expected)), Is.LessThan(Tolerance));
        }
    }
}
