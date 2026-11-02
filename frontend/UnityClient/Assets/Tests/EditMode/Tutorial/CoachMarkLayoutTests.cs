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
        public void Marker_OffScreen_GoesToTheBottomCornerOnTheTargetSide_PointingThatWay()
        {
            // 대상이 화면 밖이면 위·옆 가장자리에서 안내 카드·메뉴와 겹치지 않게, 대상이 있는 쪽 아래 구석에서 돌아볼 방향을 가리킨다.
            CoachMarkLayout.MarkerPose right = CoachMarkLayout.Marker(
                new Vector2(1200f, 300f),
                false,
                Canvas,
                50f
            );
            CoachMarkLayout.MarkerPose aboveLeft = CoachMarkLayout.Marker(
                new Vector2(300f, 900f),
                false,
                Canvas,
                50f
            );
            CoachMarkLayout.MarkerPose aboveRight = CoachMarkLayout.Marker(
                new Vector2(700f, 900f),
                false,
                Canvas,
                50f
            );

            AssertNear(right.Position, new Vector2(750f, 50f));
            Assert.That(right.ArrowDegrees, Is.EqualTo(0f));
            AssertNear(aboveLeft.Position, new Vector2(50f, 50f));
            Assert.That(aboveLeft.ArrowDegrees, Is.EqualTo(180f));
            AssertNear(aboveRight.Position, new Vector2(750f, 50f));
            Assert.That(aboveRight.ArrowDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void Marker_BehindTheCamera_GoesToTheBottomCornerOnTheTargetSide()
        {
            // 카메라 뒤의 점은 화면 좌표가 좌우로 뒤집혀 나오므로 되돌려 대상이 있는 쪽 아래 구석에 둔다.
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                new Vector2(300f, 300f),
                true,
                Canvas,
                50f
            );

            AssertNear(pose.Position, new Vector2(750f, 50f));
            Assert.That(pose.ArrowDegrees, Is.EqualTo(0f));
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }
    }
}
