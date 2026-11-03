using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Wayfinding.Tests
{
    public class GuidePolylineTests
    {
        // 동쪽으로 3m 간 뒤 북쪽으로 4m 꺾는 7m 길
        private static readonly Vector3[] Corner =
        {
            new Vector3(0, 0, 0),
            new Vector3(3, 0, 0),
            new Vector3(3, 0, 4),
        };

        [Test]
        public void Length_AddsEverySegment()
        {
            Assert.That(GuidePolyline.Length(Corner), Is.EqualTo(7f).Within(.001f));
        }

        [Test]
        public void Take_StopsPartWayAlongASegment()
        {
            var taken = new List<Vector3>();

            GuidePolyline.Take(Corner, 5f, taken);

            AssertPoints(taken, new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 0, 2));
        }

        [Test]
        public void Take_LongerThanTheLine_KeepsTheWholeLine()
        {
            var taken = new List<Vector3>();

            GuidePolyline.Take(Corner, 10f, taken);

            AssertPoints(taken, Corner);
        }

        [Test]
        public void Take_Nothing_KeepsOnlyTheStart()
        {
            var taken = new List<Vector3>();

            GuidePolyline.Take(Corner, 0f, taken);

            AssertPoints(taken, Corner[0]);
        }

        [Test]
        public void Resample_AddsAPointEverySpacingAndKeepsCorners()
        {
            var sampled = new List<Vector3>();

            GuidePolyline.Resample(Corner, 2f, sampled);

            AssertPoints(
                sampled,
                new Vector3(0, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 0, 0),
                new Vector3(3, 0, 2),
                new Vector3(3, 0, 4)
            );
        }

        [Test]
        public void Take_ReplacesWhatTheListHeld()
        {
            var taken = new List<Vector3> { Vector3.one };

            GuidePolyline.Take(Corner, 1f, taken);

            AssertPoints(taken, new Vector3(0, 0, 0), new Vector3(1, 0, 0));
        }

        private static void AssertPoints(List<Vector3> actual, params Vector3[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length), string.Join(" ", actual));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(
                    Vector3.Distance(actual[i], expected[i]),
                    Is.LessThan(.001f),
                    $"{i}번째 점 {actual[i]}"
                );
        }
    }
}
