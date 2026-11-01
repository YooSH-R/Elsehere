using NUnit.Framework;
using UnityEngine;

namespace A305.Tutorial.Tests
{
    /// <summary>코치 마크가 밝힐 구멍과 그 둘레를 덮는 어두운 판, 설명 위치를 화면 크기에 맞게 계산하는지.</summary>
    public class CoachMarkLayoutTests
    {
        private static readonly Vector2 Canvas = new Vector2(800f, 600f);

        [Test]
        public void Hole_PadsTheTargetAndStaysInsideTheCanvas()
        {
            Rect hole = CoachMarkLayout.Hole(new Rect(10f, 500f, 100f, 90f), Canvas, 12f);

            Assert.That(hole.xMin, Is.EqualTo(0f));
            Assert.That(hole.yMin, Is.EqualTo(488f));
            Assert.That(hole.xMax, Is.EqualTo(122f));
            Assert.That(hole.yMax, Is.EqualTo(600f));
        }

        [Test]
        public void Shades_CoverEverythingButTheHole()
        {
            var hole = new Rect(100f, 100f, 200f, 100f);

            Rect[] shades = CoachMarkLayout.Shades(hole, Canvas);

            float shadedArea = 0f;
            foreach (Rect shade in shades)
            {
                Assert.That(shade.Overlaps(hole), Is.False, $"구멍과 겹치는 판: {shade}");
                shadedArea += shade.width * shade.height;
            }

            Assert.That(shadedArea, Is.EqualTo(Canvas.x * Canvas.y - hole.width * hole.height));
        }

        [Test]
        public void Union_CoversEveryTarget()
        {
            Rect union = CoachMarkLayout.Union(
                new[] { new Rect(10f, 20f, 30f, 40f), new Rect(100f, 5f, 10f, 10f) }
            );

            Assert.That(union, Is.EqualTo(Rect.MinMaxRect(10f, 5f, 110f, 60f)));
        }

        [Test]
        public void Marker_OnScreen_PointsDownAtTheTarget()
        {
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                new Vector2(400f, 300f),
                false,
                Canvas,
                50f
            );

            Assert.That(pose.Position, Is.EqualTo(new Vector2(400f, 300f)));
            Assert.That(pose.ArrowDegrees, Is.EqualTo(-90f));
        }

        [Test]
        public void Marker_OffScreen_StaysOnTheEdgePointingTowardTheTarget()
        {
            // 대상이 화면 밖이면 화살표를 가장자리에 두고 대상 쪽을 가리켜 어느 쪽으로 돌아야 할지 알려 준다.
            CoachMarkLayout.MarkerPose right = CoachMarkLayout.Marker(
                new Vector2(1200f, 300f),
                false,
                Canvas,
                50f
            );
            CoachMarkLayout.MarkerPose above = CoachMarkLayout.Marker(
                new Vector2(400f, 900f),
                false,
                Canvas,
                50f
            );

            AssertNear(right.Position, new Vector2(750f, 300f));
            Assert.That(right.ArrowDegrees, Is.EqualTo(0f).Within(0.01f));
            AssertNear(above.Position, new Vector2(400f, 550f));
            Assert.That(above.ArrowDegrees, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void Marker_BehindTheCamera_StaysOnTheBottomEdgeOnTheTargetSide()
        {
            // 카메라 뒤의 점은 화면 좌표가 좌우로 뒤집혀 나오므로 되돌리고, 뒤돌아봐야 한다는 뜻으로 아래 가장자리에 둔다.
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                new Vector2(300f, 300f),
                true,
                Canvas,
                50f
            );

            Assert.That(pose.Position.y, Is.EqualTo(50f).Within(0.01f));
            Assert.That(pose.Position.x, Is.GreaterThan(400f));
            Assert.That(pose.ArrowDegrees, Is.LessThan(0f).And.GreaterThan(-90f));
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }
    }
}
