using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>벽에 막힌 3인칭 카메라 거리가 가까워졌다 멀어졌다 튀지 않게 하는 규칙.</summary>
    public class CameraCollisionDistanceTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Follow_WhenAWallComesCloser_PullsInAtOnce()
        {
            var distance = new CameraCollisionDistance(recoverSharpness: 3f);
            distance.Follow(4.2f, 0.02f);

            // 늦게 당기면 카메라가 벽 안으로 들어가 벽 너머가 보인다.
            Assert.That(distance.Follow(1.5f, 0.02f), Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void Follow_WhenTheWayClears_MovesBackOutGradually()
        {
            var distance = new CameraCollisionDistance(recoverSharpness: 3f);
            distance.Follow(1.5f, 0.02f);

            float afterOneFrame = distance.Follow(4.2f, 0.02f);

            Assert.That(afterOneFrame, Is.GreaterThan(1.5f));
            Assert.That(afterOneFrame, Is.LessThan(1.8f));
        }

        [Test]
        public void Follow_WhenTheWayStaysClear_ReachesTheChosenDistance()
        {
            var distance = new CameraCollisionDistance(recoverSharpness: 3f);
            distance.Follow(1.5f, 0.02f);

            float settled = 0f;
            for (int frame = 0; frame < 300; frame++)
                settled = distance.Follow(4.2f, 0.02f);

            Assert.That(settled, Is.EqualTo(4.2f).Within(0.01f));
        }

        [Test]
        public void Snap_MovesStraightToTheDistance()
        {
            var distance = new CameraCollisionDistance(recoverSharpness: 3f);
            distance.Follow(1.5f, 0.02f);

            Assert.That(distance.Snap(4.2f), Is.EqualTo(4.2f).Within(Tolerance));
        }

        [TestCase(0.3f, 4f, 0.3f, true, TestName = "Lamp post")]
        [TestCase(0.6f, 1.8f, 0.6f, true, TestName = "Person")]
        [TestCase(0.22f, 4f, 2f, false, TestName = "Wall piece")]
        [TestCase(8f, 4f, 8f, false, TestName = "Building")]
        public void IsThinObstacle_LetsTheCameraLookPastPostsAndPeople(
            float width,
            float height,
            float depth,
            bool isThin
        )
        {
            Assert.That(
                CameraCollisionDistance.IsThinObstacle(new Vector3(width, height, depth)),
                Is.EqualTo(isThin)
            );
        }
    }
}
