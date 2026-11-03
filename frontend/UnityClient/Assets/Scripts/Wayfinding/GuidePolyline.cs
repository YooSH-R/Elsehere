using System.Collections.Generic;
using UnityEngine;

namespace A305.Wayfinding
{
    /// <summary>꺾인 점을 차례로 이은 길. 길이를 재고, 앞에서부터 일부만 잘라 내고, 일정한 간격으로 점을 채운다.</summary>
    public static class GuidePolyline
    {
        public static float Length(IReadOnlyList<Vector3> line)
        {
            float length = 0f;
            for (int i = 1; i < line.Count; i++)
                length += Vector3.Distance(line[i - 1], line[i]);
            return length;
        }

        /// <summary>시작점에서 length만큼 간 곳까지를 result에 담는다. 끝점은 그 자리에 새로 둔다.</summary>
        public static void Take(IReadOnlyList<Vector3> line, float length, List<Vector3> result)
        {
            result.Clear();
            if (line.Count == 0)
                return;
            result.Add(line[0]);
            float left = length;
            for (int i = 1; i < line.Count; i++)
            {
                float span = Vector3.Distance(line[i - 1], line[i]);
                if (span >= left)
                {
                    if (left > 0f)
                        result.Add(Vector3.Lerp(line[i - 1], line[i], left / span));
                    return;
                }

                result.Add(line[i]);
                left -= span;
            }
        }

        /// <summary>
        /// 꺾인 점은 그대로 두고, 그 사이에 spacing보다 멀어지지 않게 점을 채워 result에 담는다.
        /// 점마다 바닥 높이를 맞출 수 있게 한다.
        /// </summary>
        public static void Resample(
            IReadOnlyList<Vector3> corners,
            float spacing,
            List<Vector3> result
        )
        {
            result.Clear();
            if (corners.Count == 0)
                return;
            result.Add(corners[0]);
            for (int i = 1; i < corners.Count; i++)
            {
                Vector3 from = corners[i - 1];
                Vector3 to = corners[i];
                // spacing 간격으로 채우고 남은 짧은 칸은 꺾인 점까지 그대로 둔다.
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / spacing));
                for (int step = 1; step < steps; step++)
                    result.Add(Vector3.MoveTowards(from, to, spacing * step));
                result.Add(to);
            }
        }
    }
}
