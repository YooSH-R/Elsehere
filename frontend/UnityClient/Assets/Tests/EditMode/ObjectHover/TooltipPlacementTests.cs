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

        // 화면 오른쪽 끝을 눌러 고정한 뒤 메뉴(≡)를 펼치면 둘레 네 자리가 모두 메뉴 버튼이나 목록에 걸려,
        // 가장 적게 덮는 메뉴 버튼 자리에 카드가 놓였다.
        [Test]
        public void PlaceAround_MenuOnEverySideNearRightEdge_PlacesClearOfMenu()
        {
            Vector2 bottomLeft = PlaceAround(
                RightEdgePointer,
                new Rect(RightEdgePointer, Vector2.zero),
                MenuButtonBox,
                MenuListBox
            );

            Rect card = new Rect(bottomLeft, TooltipSize);
            Assert.That(card.Overlaps(MenuButtonBox), Is.False, "카드가 메뉴 버튼과 겹칩니다.");
            Assert.That(card.Overlaps(MenuListBox), Is.False, "카드가 메뉴 목록과 겹칩니다.");
            Assert.That(card.Contains(RightEdgePointer), Is.False, "카드가 포인터를 덮습니다.");
        }

        [Test]
        public void PlaceAround_MenuOnEverySideNearRightEdge_PicksFreeSpotClosestToPointer()
        {
            // 메뉴 목록 아래(780, 220)가 목록 왼쪽(390, 610)보다 포인터에 가깝다.
            Vector2 bottomLeft = PlaceAround(
                RightEdgePointer,
                new Rect(RightEdgePointer, Vector2.zero),
                MenuButtonBox,
                MenuListBox
            );

            AssertPosition(new Vector2(780f, 220f), bottomLeft);
        }

        // 고정한 카드에 안내 문구가 붙어 길어져도 카드 왼쪽 위 모서리는 그대로 두고 아래로 늘인다.
        [Test]
        public void TryKeepTopLeft_GrowingCard_KeepsTopLeftCorner()
        {
            bool kept = TooltipPlacement.TryKeepTopLeft(
                new Vector2(300f, 400f),
                TooltipSize,
                new Vector2(200f, 160f),
                CanvasSize,
                Margin,
                new Rect[0],
                out Vector2 bottomLeft
            );

            Assert.That(kept, Is.True);
            AssertPosition(new Vector2(300f, 340f), bottomLeft);
        }

        [Test]
        public void TryKeepTopLeft_SameSize_KeepsPosition()
        {
            bool kept = TooltipPlacement.TryKeepTopLeft(
                new Vector2(300f, 400f),
                TooltipSize,
                TooltipSize,
                CanvasSize,
                Margin,
                new Rect[0],
                out Vector2 bottomLeft
            );

            Assert.That(kept, Is.True);
            AssertPosition(new Vector2(300f, 400f), bottomLeft);
        }

        [Test]
        public void TryKeepTopLeft_GrowingPastBottomMargin_DoesNotKeep()
        {
            // 아래 여백(20)까지 늘일 수 없으면 자리를 다시 정해야 한다.
            bool kept = TooltipPlacement.TryKeepTopLeft(
                new Vector2(300f, 40f),
                TooltipSize,
                new Vector2(200f, 160f),
                CanvasSize,
                Margin,
                new Rect[0],
                out _
            );

            Assert.That(kept, Is.False);
        }

        [Test]
        public void TryKeepTopLeft_GrowingOntoOtherScreen_DoesNotKeep()
        {
            // 늘어난 카드가 나중에 열린 창을 덮으면 자리를 다시 정해야 한다.
            bool kept = TooltipPlacement.TryKeepTopLeft(
                new Vector2(300f, 400f),
                TooltipSize,
                new Vector2(200f, 160f),
                CanvasSize,
                Margin,
                new[] { new Rect(250f, 300f, 300f, 60f) },
                out _
            );

            Assert.That(kept, Is.False);
        }

        [Test]
        public void PlaceAround_MovingOutOfUiMeetsMoreUi_KeepsMovingUntilClear()
        {
            // 포인터 아래로 비켜도 바로 밑의 띠에 걸려, 그 띠 아래까지 내려가야 빈자리가 있다.
            Vector2 pointer = new Vector2(500f, 400f);
            Rect[] ui =
            {
                new Rect(0f, 410f, 1000f, 390f),
                new Rect(0f, 300f, 490f, 110f),
                new Rect(510f, 300f, 490f, 110f),
                new Rect(0f, 180f, 1000f, 100f),
            };

            Vector2 bottomLeft = PlaceAround(pointer, new Rect(pointer, Vector2.zero), ui);

            Rect card = new Rect(bottomLeft, TooltipSize);
            foreach (Rect area in ui)
            {
                Assert.That(card.Overlaps(area), Is.False, $"카드가 {area}와 겹칩니다.");
            }
        }

        // 가리킨 사물(450~550, 250~350)과 그 위·아래를 막는 영역
        private static readonly Rect PointedBox = new Rect(450f, 250f, 100f, 100f);
        private static readonly Rect AboveBox = new Rect(0f, 360f, 1000f, 440f);
        private static readonly Rect BelowBox = new Rect(0f, 0f, 1000f, 240f);

        // 오른쪽 위 메뉴 버튼, 그 아래로 펼친 메뉴 목록, 목록 오른쪽 가장자리의 포인터
        private static readonly Rect MenuButtonBox = new Rect(890f, 690f, 90f, 90f);
        private static readonly Rect MenuListBox = new Rect(600f, 330f, 340f, 350f);
        private static readonly Vector2 RightEdgePointer = new Vector2(970f, 600f);

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
