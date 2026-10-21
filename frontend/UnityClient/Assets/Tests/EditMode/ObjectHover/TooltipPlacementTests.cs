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

        private static Vector2 Place(Vector2 anchor)
        {
            return TooltipPlacement.PlaceAbove(anchor, TooltipSize, CanvasSize, Gap, Margin);
        }

        private static void AssertPosition(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
        }
    }
}
