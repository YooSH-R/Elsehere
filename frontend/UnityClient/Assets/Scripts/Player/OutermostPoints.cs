using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 점 묶음에서 여러 방향으로 가장 바깥에 있는 점만 고른다. 묶음을 어느 쪽으로 돌려도 감싸는 상자의 면은 거의 이 점들이 정하므로,
    /// 팔 관통 보정이 머리·몸통 정점 수만 개 대신 이 점들만 매 프레임 계산해도 상자가 거의 같다.
    /// 돌리지 않은 상자는 x·y·z 양쪽 끝 점을 꼭 넣어 모든 점으로 잰 것과 똑같다.
    /// </summary>
    public static class OutermostPoints
    {
        // 구 위에 고르게 둔 방향 수. 방향 사이가 좁을수록 돌린 상자의 오차가 작고, 처음 고를 때 시간이 든다.
        // 300개면 어느 방향으로 돌려도 상자의 면이 크기의 약 1% 안에 든다(200개는 1.5%를 넘었다).
        private const int SpreadDirectionCount = 300;

        private static readonly Vector3[] Directions = CreateDirections();

        /// <summary>고르는 점의 최대 수. 점이 이보다 적으면 모두 남긴다.</summary>
        public const int MaxCount = SpreadDirectionCount + 6;

        /// <returns>남길 점의 번호. 원래 순서대로 놓는다.</returns>
        public static int[] Select(IReadOnlyList<Vector3> points)
        {
            if (points.Count <= MaxCount)
            {
                return AllIndices(points.Count);
            }

            var outermost = new int[Directions.Length];
            var farthest = new float[Directions.Length];
            for (int d = 0; d < Directions.Length; d++)
            {
                farthest[d] = float.NegativeInfinity;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector3 direction = Directions[d];
                    float reach =
                        point.x * direction.x + point.y * direction.y + point.z * direction.z;
                    if (reach > farthest[d])
                    {
                        farthest[d] = reach;
                        outermost[d] = i;
                    }
                }
            }

            var kept = new SortedSet<int>(outermost);
            var indices = new int[kept.Count];
            kept.CopyTo(indices);
            return indices;
        }

        private static int[] AllIndices(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            return indices;
        }

        // x·y·z 양쪽 끝과, 구 위에 고르게 둔 방향(피보나치 나선)
        private static Vector3[] CreateDirections()
        {
            var directions = new List<Vector3>
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back,
            };
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < SpreadDirectionCount; i++)
            {
                float y = 1f - 2f * (i + .5f) / SpreadDirectionCount;
                float ring = Mathf.Sqrt(1f - y * y);
                float angle = golden * i;
                directions.Add(new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring));
            }

            return directions.ToArray();
        }
    }
}
