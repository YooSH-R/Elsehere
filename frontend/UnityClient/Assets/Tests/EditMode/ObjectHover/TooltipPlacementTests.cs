using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class TooltipPlacementTests
    {
        private const float Gap = 10f;
        private const float Margin = 20f;
        private const float Tolerance = 0.001f;

        private static readonly Vector2 TooltipSize = new Vector2(200f, 100f);
        private static readonly Vector2 CanvasSize = new Vector2(1000f, 800f);

        [Test]
        public void PlaceAbove_InMiddle_CentersAboveAnchor()
        {
            Vector2 bottomLeft = Place(new Vector2(500f, 300f));

            AssertPosition(new Vector2(400f, 310f), bottomLeft);
        }

        [Test]
        public void PlaceAbove_NearRightEdge_ShiftsLeftInsideMargin()
        {
            Vector2 bottomLeft = Place(new Vector2(980f, 300f));

            AssertPosition(new Vector2(780f, 310f), bottomLeft);
        }

        [Test]
        public void PlaceAbove_NearLeftEdge_ShiftsRightInsideMargin()
        {
            Vector2 bottomLeft = Place(new Vector2(10f, 300f));

            AssertPosition(new Vector2(20f, 310f), bottomLeft);
        }

        [Test]
        public void PlaceAbove_NearTop_ShiftsDownInsideMargin()
        {
            Vector2 bottomLeft = Place(new Vector2(500f, 750f));

            AssertPosition(new Vector2(400f, 680f), bottomLeft);
        }

        [Test]
        public void PlaceAbove_BelowBottom_ShiftsUpInsideMargin()
        {
            Vector2 bottomLeft = Place(new Vector2(500f, -50f));

            AssertPosition(new Vector2(400f, 20f), bottomLeft);
        }

        [Test]
        public void PlaceAround_PointerOnly_PlacesAbovePointer()
        {
            Vector2 pointer = new Vector2(500f, 300f);

            Vector2 bottomLeft = PlaceAround(pointer, new Rect(pointer, Vector2.zero));

            AssertPosition(new Vector2(400f, 310f), bottomLeft);
        }

        [Test]
        public void PlaceAround_PointedArea_PlacesAboveItsTopFollowingPointer()
        {
            Vector2 bottomLeft = PlaceAround(
                new Vector2(480f, 300f),
                new Rect(420f, 250f, 160f, 120f)
            );

            AssertPosition(new Vector2(380f, 380f), bottomLeft);
        }

        [Test]
        public void PlaceAround_AboveCovered_PlacesBelow()
        {
            Vector2 pointer = new Vector2(500f, 300f);

            Vector2 bottomLeft = PlaceAround(
                pointer,
                new Rect(pointer, Vector2.zero),
                new Rect(0f, 400f, 1000f, 400f)
            );

            AssertPosition(new Vector2(400f, 190f), bottomLeft);
        }

        [Test]
        public void PlaceAround_AboveAndBelowCovered_PlacesRight()
        {
            Vector2 bottomLeft = PlaceAround(
                new Vector2(500f, 300f),
                PointedBox,
                AboveBox,
                BelowBox
            );

            AssertPosition(new Vector2(560f, 250f), bottomLeft);
        }

        [Test]
        public void PlaceAround_OnlyLeftFree_PlacesLeft()
        {
            Vector2 bottomLeft = PlaceAround(
                new Vector2(500f, 300f),
                PointedBox,
                AboveBox,
                BelowBox,
                new Rect(560f, 0f, 440f, 800f)
            );

            AssertPosition(new Vector2(240f, 250f), bottomLeft);
        }

        [Test]
        public void PlaceAround_EverySideCovered_PicksLeastCovered()
        {
            // 왼쪽 자리만 왼쪽 가장자리 10px 폭과 겹쳐 가장 적게 가린다.
            Vector2 bottomLeft = PlaceAround(
                new Vector2(500f, 300f),
                PointedBox,
                AboveBox,
                BelowBox,
                new Rect(560f, 0f, 440f, 800f),
                new Rect(0f, 0f, 250f, 800f)
            );

            AssertPosition(new Vector2(240f, 250f), bottomLeft);
        }

        // 캐릭터 옆 바닥처럼 포인터가 캐릭터 영역 안에 있으면 포인터 둘레 네 자리가 모두 캐릭터와 겹친다.
        // 이때는 덜 덮는 자리를 고르지 않고 캐릭터 바깥 자리에 둔다.
        [Test]
        public void PlaceAround_PointerInsideCharacter_PlacesOutsideTheCharacter()
        {
            Vector2 pointer = new Vector2(600f, 300f);
            Rect character = new Rect(300f, 100f, 380f, 450f);

            Vector2 bottomLeft = PlaceAround(pointer, new Rect(pointer, Vector2.zero), character);

            Assert.That(new Rect(bottomLeft, TooltipSize).Overlaps(character), Is.False);
        }

        [Test]
        public void PlaceAround_NearTop_PlacesBelowInsteadOfCoveringPointer()
        {
            Vector2 pointer = new Vector2(500f, 750f);

            Vector2 bottomLeft = PlaceAround(pointer, new Rect(pointer, Vector2.zero));

            AssertPosition(new Vector2(400f, 640f), bottomLeft);
        }

        // 가리킨 사물(450~550, 250~350)과 그 위·아래를 막는 영역
        private static readonly Rect PointedBox = new Rect(450f, 250f, 100f, 100f);
        private static readonly Rect AboveBox = new Rect(0f, 360f, 1000f, 440f);
        private static readonly Rect BelowBox = new Rect(0f, 0f, 1000f, 240f);

        private static Vector2 Place(Vector2 anchor)
        {
            return TooltipPlacement.PlaceAbove(anchor, TooltipSize, CanvasSize, Gap, Margin);
        }

        private static Vector2 PlaceAround(Vector2 pointer, Rect pointedArea, params Rect[] avoid)
        {
            return TooltipPlacement.PlaceAround(
                pointer,
                pointedArea,
                TooltipSize,
                CanvasSize,
                Gap,
                Margin,
                avoid
            );
        }

        private static void AssertPosition(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
        }
    }
}
