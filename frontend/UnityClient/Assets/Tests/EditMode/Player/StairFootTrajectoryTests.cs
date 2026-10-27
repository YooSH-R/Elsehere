using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    public sealed class StairFootTrajectoryTests
    {
        [TestCase(.1f)]
        [TestCase(-.1f)]
        public void ShoeClearsTheRiserBeforeCrossingAndLandsWithoutASnap(float height)
        {
            var start = new Vector3(0, .1f, 0);
            var end = new Vector3(0, .1f + height, .3f);
            Assert.That(StairFootTrajectory.Evaluate(start, end, 0, .04f), Is.EqualTo(start));
            Assert.That(StairFootTrajectory.Evaluate(start, end, 1, .04f), Is.EqualTo(end));
            for (int i = 1; i < 100; i++)
            {
                var point = StairFootTrajectory.Evaluate(start, end, i / 100f, .04f);
                // 신발 길이까지 포함한 모서리 통과 구간. 발을 옮기는 동안 높은 디딤면을 넘는다.
                if (point.z > .04f && point.z < .26f)
                    Assert.That(
                        point.y,
                        Is.GreaterThanOrEqualTo(Mathf.Max(start.y, end.y) + .039f)
                    );
            }
            var first = StairFootTrajectory.Evaluate(start, end, .001f, .04f);
            var last = StairFootTrajectory.Evaluate(start, end, .999f, .04f);
            Assert.That(Vector3.Distance(first, start), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(last, end), Is.LessThan(.0001f));
        }
    }
}
