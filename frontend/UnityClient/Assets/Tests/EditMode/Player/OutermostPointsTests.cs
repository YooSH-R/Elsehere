using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>
    /// 팔 관통 보정은 머리·몸통을 감싸는 상자를 매 프레임 잰다. 정점 수만 개를 모두 계산하지 않도록 여러 방향으로 가장 바깥에 있는
    /// 정점만 골라 두는데, 골라 둔 정점으로 잰 상자가 모든 정점으로 잰 상자와 거의 같아야 한다.
    /// </summary>
    public class OutermostPointsTests
    {
        private const int SpherePointCount = 6000;

        [Test]
        public void DenseSphere_KeepsOnlyAFewOutermostPoints()
        {
            List<Vector3> sphere = Sphere(SpherePointCount, radius: 1f);

            int[] kept = OutermostPoints.Select(sphere);

            Assert.That(kept.Length, Is.LessThanOrEqualTo(OutermostPoints.MaxCount));
            Assert.That(kept.Length, Is.LessThan(SpherePointCount / 10));
        }

        [Test]
        public void UnturnedShape_HasExactlyTheSameBox()
        {
            List<Vector3> egg = Sphere(SpherePointCount, radius: 1f)
                .Select(point => new Vector3(point.x * .7f, point.y * 1.2f + .3f, point.z))
                .ToList();

            int[] kept = OutermostPoints.Select(egg);

            (Vector3 min, Vector3 max) all = BoxOf(egg);
            (Vector3 min, Vector3 max) selected = BoxOf(kept.Select(index => egg[index]));
            Assert.That(selected.min, Is.EqualTo(all.min));
            Assert.That(selected.max, Is.EqualTo(all.max));
        }

        [Test]
        public void TurnedShape_BoxSidesStayWithinOneAndAHalfPercentOfItsSize()
        {
            // 머리는 걸으며 끄덕이고 돌아가므로 어느 방향으로 돌려도 상자의 각 면이 거의 같아야 한다.
            // 반지름 0.15m 머리라면 2.2mm로, 손을 머리에서 띄우는 여유(8mm)보다 충분히 작다.
            const float radius = 1f;
            const float tolerance = radius * .015f;
            List<Vector3> sphere = Sphere(SpherePointCount, radius);
            int[] kept = OutermostPoints.Select(sphere);
            var random = new System.Random(608);

            for (int turn = 0; turn < 100; turn++)
            {
                Quaternion rotation = Quaternion.Euler(
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 360f
                );
                (Vector3 min, Vector3 max) all = BoxOf(sphere.Select(point => rotation * point));
                (Vector3 min, Vector3 max) selected = BoxOf(
                    kept.Select(index => rotation * sphere[index])
                );

                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.That(
                        selected.min[axis] - all.min[axis],
                        Is.InRange(0f, tolerance),
                        $"{rotation.eulerAngles} 축 {axis} 아래쪽 면"
                    );
                    Assert.That(
                        all.max[axis] - selected.max[axis],
                        Is.InRange(0f, tolerance),
                        $"{rotation.eulerAngles} 축 {axis} 위쪽 면"
                    );
                }
            }
        }

        [Test]
        public void FewPoints_KeepsThemAll()
        {
            List<Vector3> hand = Sphere(OutermostPoints.MaxCount, radius: .05f);

            int[] kept = OutermostPoints.Select(hand);

            Assert.That(kept, Is.EquivalentTo(Enumerable.Range(0, hand.Count)));
        }

        // 구 표면에 고르게 놓인 점(피보나치 나선)
        private static List<Vector3> Sphere(int count, float radius)
        {
            var points = new List<Vector3>(count);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + .5f) / count;
                float ring = Mathf.Sqrt(1f - y * y);
                float angle = golden * i;
                points.Add(
                    new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring) * radius
                );
            }

            return points;
        }

        // Bounds.Encapsulate는 가운데·반 크기로 저장해 넣는 순서에 따라 끝값이 조금씩 달라지므로, 최소·최대를 직접 구한다.
        private static (Vector3 min, Vector3 max) BoxOf(IEnumerable<Vector3> points)
        {
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            foreach (Vector3 point in points)
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            return (min, max);
        }
    }
}
