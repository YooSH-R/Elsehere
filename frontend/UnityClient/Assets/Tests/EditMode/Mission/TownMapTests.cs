using NUnit.Framework;
using UnityEngine;

namespace A305.Mission.Tests
{
    public class TownMapTests
    {
        private const float Tolerance = 0.0001f;

        // 서쪽 X=-20 ~ 동쪽 X=20, 남쪽 Z=-30 ~ 북쪽 Z=30.
        private static readonly TownMap Map = new TownMap(new Rect(-20f, -30f, 40f, 60f));

        [Test]
        public void ToMap_SouthWestCorner_IsLeftTop()
        {
            AssertMapPoint(new Vector2(0f, 1f), Map.ToMap(new Vector3(-20f, 0f, -30f)));
        }

        [Test]
        public void ToMap_NorthEastCorner_IsRightBottom()
        {
            AssertMapPoint(new Vector2(1f, 0f), Map.ToMap(new Vector3(20f, 0f, 30f)));
        }

        [Test]
        public void ToMap_AreaCentre_IsMapCentre()
        {
            AssertMapPoint(new Vector2(.5f, .5f), Map.ToMap(Vector3.zero));
        }

        [Test]
        public void ToMap_HeightAboveGround_IsIgnored()
        {
            AssertMapPoint(new Vector2(.65f, .4f), Map.ToMap(new Vector3(4f, 12f, 9f)));
        }

        [Test]
        public void ToMap_OutsideArea_StaysOnMapEdge()
        {
            AssertMapPoint(new Vector2(1f, 1f), Map.ToMap(new Vector3(-100f, 0f, 100f)));
        }

        [Test]
        public void ToMapRotation_FacingNorth_PointsRight()
        {
            Assert.That(Map.ToMapRotation(0f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ToMapRotation_FacingEast_PointsDown()
        {
            Assert.That(Map.ToMapRotation(90f), Is.EqualTo(-90f).Within(Tolerance));
        }

        [Test]
        public void Pointing_TowardMapTop_RotatesQuarterTurnCounterclockwise()
        {
            Assert.That(
                TownMap.Pointing(new Vector2(.4f, .2f), new Vector2(.4f, .6f)),
                Is.EqualTo(90f).Within(Tolerance)
            );
        }

        [Test]
        public void Pointing_TowardMapLeft_RotatesHalfTurn()
        {
            Assert.That(
                TownMap.Pointing(new Vector2(.5f, .5f), new Vector2(.1f, .5f)),
                Is.EqualTo(180f).Within(Tolerance)
            );
        }

        [Test]
        public void WidthPerHeight_StreetLongerThanWide_IsWiderMap()
        {
            Assert.That(Map.WidthPerHeight, Is.EqualTo(1.5f).Within(Tolerance));
        }

        private static void AssertMapPoint(Vector2 expected, Vector2 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "y");
        }
    }
}
