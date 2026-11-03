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
        public void Marker_AboveTheTargetOnScreen_IsShown()
        {
            // Ben 머리 위 자리가 화면 안(위쪽 안내 카드 아래)에 있으면 그 자리에서 아래를 가리킨다.
            Assert.That(
                CoachMarkLayout.ShowsMarker(new Vector2(400f, 300f), false, Canvas, 50f, 150f),
                Is.True
            );
        }

        [TestCase(400f, 520f, false)]
        [TestCase(400f, 900f, false)]
        [TestCase(1200f, 300f, false)]
        [TestCase(300f, 300f, true)]
        public void Marker_UnderTheGuideCard_OffScreen_OrBehind_IsHidden(
            float x,
            float y,
            bool isBehind
        )
        {
            // 화살표를 몸통·얼굴로 내리면 Ben 얼굴을 가리므로, 머리 위에 둘 수 없으면 띄우지 않는다.
            // Ben은 기다리는 구도에서 늘 화면 가운데로 다가오고, 숙소 문은 발밑 나침반이 가리킨다.
            Assert.That(
                CoachMarkLayout.ShowsMarker(new Vector2(x, y), isBehind, Canvas, 50f, 150f),
                Is.False
            );
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
        public void Bubble_AboveWouldCoverTheKeepClearArea_SitsBelowInstead()
        {
            // 대화 위에 남긴 [건너뛰기] 카드처럼 가리면 안 되는 곳이 위에 있으면 아래로 간다.
            var keepClear = new Rect(250f, 450f, 300f, 100f);

            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(50f, 250f, 700f, 120f),
                new Vector2(300f, 100f),
                Canvas,
                20f,
                10f,
                keepClear
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Below));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(250f, 130f, 300f, 100f)));
            Assert.That(bubble.Rect.Overlaps(keepClear), Is.False);
        }

        [Test]
        public void Bubble_SideWouldCoverTheKeepClearArea_SlidesDownBelowIt()
        {
            // 옆자리 위쪽 끝이 가리면 안 되는 곳에 걸리면 같은 옆에서 그 아래로 내려 놓는다.
            var keepClear = new Rect(250f, 400f, 300f, 100f);

            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(500f, 300f, 200f, 150f),
                new Vector2(200f, 100f),
                Canvas,
                20f,
                10f,
                keepClear
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Left));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(280f, 280f, 200f, 100f)));
            Assert.That(bubble.Tail.x, Is.EqualTo(480f));
            Assert.That(bubble.Tail.y, Is.InRange(bubble.Rect.yMin, bubble.Rect.yMax));
        }

        [Test]
        public void Bubble_NoRoomBelow_StaysAboveButMovesBesideTheKeepClearArea()
        {
            var keepClear = new Rect(300f, 480f, 200f, 100f);

            CoachMarkLayout.BubblePose bubble = CoachMarkLayout.Bubble(
                new Rect(50f, 100f, 700f, 300f),
                new Vector2(200f, 100f),
                Canvas,
                20f,
                10f,
                keepClear
            );

            Assert.That(bubble.Side, Is.EqualTo(CoachMarkLayout.BubbleSide.Above));
            Assert.That(bubble.Rect, Is.EqualTo(new Rect(80f, 420f, 200f, 100f)));
            Assert.That(bubble.Tail.y, Is.EqualTo(420f));
            Assert.That(bubble.Tail.x, Is.InRange(bubble.Rect.xMin, bubble.Rect.xMax));
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
