using System.Collections.Generic;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 마을 본길을 도는 차의 길. 오른쪽 통행이라 동쪽 차로(+x)로 북쪽 끝까지 가서 반원을 그리며 유턴하고,
    /// 서쪽 차로(-x)로 남쪽 끝까지 내려와 다시 유턴한다. 길 위의 위치는 출발점에서 잰 거리(m)로 나타낸다.
    /// </summary>
    public sealed class CarLoopRoute
    {
        // 차 가운데에서 이 거리 안, 차 폭 안쪽 앞에 사람이 있으면 멈춘다.
        private const float YieldAheadMeters = 6f;
        private const float YieldHalfWidthMeters = 1.4f;

        private readonly float southZ;
        private readonly float northZ;
        private readonly float laneOffset;

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

        /// <param name="southZ">남쪽 유턴 지점</param>
        /// <param name="northZ">북쪽 유턴 지점</param>
        /// <param name="laneOffset">도로 가운데에서 차로 가운데까지의 거리. 유턴 반원의 반지름이다.</param>
        public CarLoopRoute(float southZ, float northZ, float laneOffset)
        {
            this.southZ = southZ;
            this.northZ = northZ;
            this.laneOffset = laneOffset;
        }

        private float Straight => northZ - southZ;
        private float Turn => Mathf.PI * laneOffset;

        public float Length => 2f * Straight + 2f * Turn;

        public Pose PoseAt(float distance)
        {
            float d = Wrap(distance);
            if (d < Straight)
                return new Pose(new Vector3(laneOffset, 0f, southZ + d), Vector3.forward);
            d -= Straight;
            if (d < Turn)
            {
                float angle = d / laneOffset;
                return new Pose(
                    new Vector3(
                        laneOffset * Mathf.Cos(angle),
                        0f,
                        northZ + laneOffset * Mathf.Sin(angle)
                    ),
                    new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle))
                );
            }
            d -= Turn;
            if (d < Straight)
                return new Pose(new Vector3(-laneOffset, 0f, northZ - d), Vector3.back);
            float southAngle = (d - Straight) / laneOffset;
            return new Pose(
                new Vector3(
                    -laneOffset * Mathf.Cos(southAngle),
                    0f,
                    southZ - laneOffset * Mathf.Sin(southAngle)
                ),
                new Vector3(Mathf.Sin(southAngle), 0f, -Mathf.Cos(southAngle))
            );
        }

        /// <summary>북쪽으로 가는 차로에서 z 지점을 지날 때의 거리</summary>
        public float NorthboundDistance(float z) => z - southZ;

        /// <summary>남쪽으로 가는 차로에서 z 지점을 지날 때의 거리</summary>
        public float SouthboundDistance(float z) => Straight + Turn + (northZ - z);

        /// <summary>
        /// step만큼 앞으로 간 거리를 돌려준다. 그 사이에 정지 지점이 있으면 그 자리에서 서고, 이미 정지 지점에 있으면 그대로 기다린다.
        /// </summary>
        public float Advance(float distance, float step, IEnumerable<float> stopPoints)
        {
            float from = Wrap(distance);
            float travel = step;
            foreach (float stop in stopPoints)
            {
                float ahead = Wrap(stop - from);
                if (ahead < travel)
                    travel = ahead;
            }
            return Wrap(from + travel);
        }

        /// <summary>차 앞, 차 폭 안쪽에 가까이 있는 사람을 치지 않도록 설지 정한다.</summary>
        public static bool ShouldYield(Vector3 carPosition, Vector3 carForward, Vector3 obstacle)
        {
            Vector3 offset = obstacle - carPosition;
            offset.y = 0f;
            float ahead = Vector3.Dot(offset, carForward);
            float side = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, carForward)));
            return ahead > 0f && ahead < YieldAheadMeters && side < YieldHalfWidthMeters;
        }

        private float Wrap(float distance) => Mathf.Repeat(distance, Length);
    }
}
