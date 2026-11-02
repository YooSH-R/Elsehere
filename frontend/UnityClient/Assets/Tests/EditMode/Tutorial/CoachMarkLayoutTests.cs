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
                new Vector2(400f, 300f),
                false,
                Canvas,
                50f,
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
                new Vector2(1200f, 300f),
                false,
                Canvas,
                50f,
                50f
            );
            CoachMarkLayout.MarkerPose aboveLeft = CoachMarkLayout.Marker(
                new Vector2(300f, 900f),
                new Vector2(300f, 900f),
                false,
                Canvas,
                50f,
                50f
            );
            CoachMarkLayout.MarkerPose aboveRight = CoachMarkLayout.Marker(
                new Vector2(700f, 900f),
                new Vector2(700f, 900f),
                false,
                Canvas,
                50f,
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
                new Vector2(300f, 300f),
                true,
                Canvas,
                50f,
                50f
            );

            AssertNear(pose.Position, new Vector2(750f, 50f));
            Assert.That(pose.ArrowDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void Marker_TopAboveTheScreen_ButTheBodyOnScreen_PointsDownAtTheBody()
        {
            // 문 가까이 서면 문 위 화살표 자리는 화면 위로 벗어나도 문은 보이므로, 옆 구석이 아니라 문 몸통을 가리킨다.
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                new Vector2(400f, 900f),
                new Vector2(400f, 300f),
                false,
                Canvas,
                50f,
                50f
            );

            AssertNear(pose.Position, new Vector2(400f, 300f));
            Assert.That(pose.ArrowDegrees, Is.EqualTo(-90f));
        }

        [Test]
        public void Marker_TopUnderTheGuideCard_PointsAtTheBodyInstead()
        {
            // 위쪽 안내 카드 자리(위쪽 여백)에 걸린 화살표는 카드에 가리므로 몸통을 가리킨다.
            CoachMarkLayout.MarkerPose pose = CoachMarkLayout.Marker(
                new Vector2(400f, 520f),
                new Vector2(400f, 300f),
                false,
                Canvas,
                50f,
                150f
            );

            AssertNear(pose.Position, new Vector2(400f, 300f));
        }

        [Test]
        public void Bubble_RoomOnTheRight_SitsRightOfTheHole_WithItsTailOnTheLeftEdge()
        {
            // 설명은 밝힌 대상 옆 말풍선으로 둔다. 오른쪽에 자리가 있으면 오른쪽에, 대상 높이 가운데에 맞춘다.
            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(100f, 250f, 100f, 100f),
                new Vector2(300f, 100f),
                Canvas,
                20f,
                10f
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Right));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(220f, 250f, 300f, 100f)));
            AssertNear(bubble.Tail, new Vector2(220f, 300f));
        }

        [Test]
        public void Bubble_NoRoomOnTheRight_SitsLeft()
        {
            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(600f, 250f, 100f, 100f),
                new Vector2(300f, 100f),
                Canvas,
                20f,
                10f
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Left));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(280f, 250f, 300f, 100f)));
            AssertNear(bubble.Tail, new Vector2(580f, 300f));
        }

        [Test]
        public void Bubble_WideHole_SitsAboveIt_CenteredOnIt()
        {
            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(50f, 100f, 700f, 120f),
                new Vector2(300f, 100f),
                Canvas,
                20f,
                10f
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Above));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(250f, 240f, 300f, 100f)));
            AssertNear(bubble.Tail, new Vector2(400f, 240f));
        }

        [Test]
        public void Bubble_NearTheBottom_StaysInsideTheCanvas_AndKeepsTheTailOnItsEdge()
        {
            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(100f, 0f, 100f, 40f),
                new Vector2(300f, 100f),
                Canvas,
                20f,
                10f
            );

            Assert.That(bubble.Rect, Is.EqualTo(new Rect(220f, 10f, 300f, 100f)));
            Assert.That(bubble.Tail.x, Is.EqualTo(220f));
            Assert.That(bubble.Tail.y, Is.InRange(bubble.Rect.yMin, bubble.Rect.yMax));
        }

        [Test]
        public void Compass_LiesOnTheGroundAtTheFeet_PointingTowardTheTarget()
        {
            // 걸어갈 곳은 플레이어 발밑 바닥에 나침반처럼 가리킨다. 높낮이는 보지 않고 바닥을 따라 대상 쪽을 가리킨다.
            CoachMarkLayout.CompassPose compass = CoachMarkLayout.Compass(
                new Vector3(1f, 0.5f, 1f),
                new Vector3(4f, 3f, 5f),
                0.8f
            );

            var toward = new Vector3(0.6f, 0f, 0.8f);
            AssertNear(compass.Direction, toward);
            AssertNear(compass.PointerPosition, new Vector3(1f, 0.5f, 1f) + toward * 0.8f);
            AssertNear(compass.Rotation * Vector3.right, toward);
            Assert.That(
                Mathf.Abs(Vector3.Dot(compass.Rotation * Vector3.forward, Vector3.up)),
                Is.EqualTo(1f).Within(0.001f),
                "그림 면이 바닥과 나란히 눕습니다."
            );
        }

        [Test]
        public void Compass_TargetRightAboveTheFeet_StillPointsSomewhereOnTheGround()
        {
            CoachMarkLayout.CompassPose compass = CoachMarkLayout.Compass(
                Vector3.zero,
                Vector3.up * 2f,
                0.8f
            );

            Assert.That(compass.Direction.y, Is.EqualTo(0f));
            Assert.That(compass.Direction.magnitude, Is.EqualTo(1f).Within(0.001f));
        }

        [TestCase(0f, 0f, 1f, 90f)]
        [TestCase(1f, 0f, 0f, 0f)]
        [TestCase(-1f, 0f, 0f, 180f)]
        [TestCase(0f, 0f, -1f, -90f)]
        public void CompassScreenArrow_PointsWhereToTurn_FromWhereTheCameraLooks(
            float x,
            float y,
            float z,
            float expectedDegrees
        )
        {
            // 1인칭처럼 발밑 나침반이 화면에 없으면 화면 아래 화살표로 알린다. 카메라가 보는 쪽이 위(90도)다.
            float degrees = CoachMarkLayout.CompassScreenArrowDegrees(
                new Vector3(0f, -0.3f, 1f),
                new Vector3(x, y, z)
            );

            Assert.That(Mathf.DeltaAngle(degrees, expectedDegrees), Is.EqualTo(0f).Within(0.01f));
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThan(0.001f),
                $"{actual} ≠ {expected}"
            );
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }
    }
}
