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
        public void IsCaptionBelow_WhenThereIsRoomUnderTheHole()
        {
            Assert.That(
                CoachMarkLayout.IsCaptionBelow(new Rect(0f, 200f, 100f, 50f), 120f, 16f),
                Is.True
            );
            Assert.That(
                CoachMarkLayout.IsCaptionBelow(new Rect(0f, 60f, 100f, 50f), 120f, 16f),
                Is.False
            );
        }
    }
}
