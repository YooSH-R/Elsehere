using System.Collections.Generic;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 주차 칸과 본길 사이처럼 <see cref="CarLoopRoute"/> 밖을 잇는 짧은 곡선 길. 3차 베지어 곡선을 이어 붙이고,
    /// 길 위의 위치는 시작점에서 잰 거리(m)로 나타낸다. 곡선을 잘게 나눠 잰 길이로 거리를 곡선 위 지점에 맞춘다.
    /// </summary>
    public sealed class DrivePath
    {
        // 곡선 하나를 이만큼 나눠 길이를 잰다. 몇 m 길이의 진입로에서 걸음마다 5% 안쪽으로 고르게 움직인다.
        private const int SamplesPerCurve = 32;

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> distances = new List<float>();

        /// <summary>시작점, 시작점에서 나가는 손잡이, 끝점으로 들어오는 손잡이, 끝점</summary>
        public readonly struct Curve
        {
            public Curve(Vector3 start, Vector3 startHandle, Vector3 endHandle, Vector3 end)
            {
                Start = start;
                StartHandle = startHandle;
                EndHandle = endHandle;
                End = end;
            }

            public Vector3 Start { get; }
            public Vector3 StartHandle { get; }
            public Vector3 EndHandle { get; }
            public Vector3 End { get; }

            public Vector3 PointAt(float t)
            {
                float u = 1 - t;
                return u * u * u * Start
                    + 3 * u * u * t * StartHandle
                    + 3 * u * t * t * EndHandle
                    + t * t * t * End;
            }
        }

        public readonly struct Pose
        {
            public Pose(Vector3 position, Vector3 forward)
            {
                Position = position;
                Forward = forward;
            }

            public Vector3 Position { get; }
            public Vector3 Forward { get; }
        }

        /// <param name="curves">앞 곡선의 끝점이 다음 곡선의 시작점인 곡선들</param>
        public DrivePath(params Curve[] curves)
        {
            points.Add(curves[0].Start);
            distances.Add(0);
            foreach (Curve curve in curves)
            {
                for (int index = 1; index <= SamplesPerCurve; index++)
                {
                    Vector3 point = curve.PointAt(index / (float)SamplesPerCurve);
                    distances.Add(
                        distances[distances.Count - 1]
                            + Vector3.Distance(points[points.Count - 1], point)
                    );
                    points.Add(point);
                }
            }
        }

        public float Length => distances[distances.Count - 1];

        /// <summary>시작점에서 distance만큼 간 곳. 범위를 벗어나면 시작점이나 끝점에 머문다.</summary>
        public Pose PoseAt(float distance)
        {
            float clamped = Mathf.Clamp(distance, 0, Length);
            int segment = 1;
            while (segment < distances.Count - 1 && distances[segment] < clamped)
                segment++;
            Vector3 from = points[segment - 1];
            Vector3 to = points[segment];
            float span = distances[segment] - distances[segment - 1];
            float t = span > 0 ? (clamped - distances[segment - 1]) / span : 0;
            return new Pose(Vector3.Lerp(from, to, t), (to - from).normalized);
        }
    }
}
