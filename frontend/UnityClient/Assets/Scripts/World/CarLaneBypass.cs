using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 남쪽 도로에 주차한 차량을 지나갈 때의 완만한 우회 경로. 비켜 가는 차는 맞은편 차로까지 넘어가 마주 오는 차와 겹치므로,
    /// 우회를 시작하는 곳부터 남쪽 유턴을 돌아 다시 그 높이까지는 한 번에 한 차만 지나가는 구간으로 둔다.
    /// </summary>
    public static class CarLaneBypass
    {
        private const float SingleLaneNorthZ = -12f;

        /// <summary>앞으로 실제 주행할 경로에 장애물이 있는지.</summary>
        public static bool ShouldYield(
            CarLoopRoute route,
            float distance,
            float offset,
            Vector3 obstacle
        )
        {
            Vector3 previous = PoseAt(route, distance, offset).Position;
            // 굽은 곳의 순간 접선을 길게 늘리면 도로 밖의 사람을 앞에 있다고 오인한다.
            // 실제 앞으로 지나갈 짧은 선분들을 검사하고, 기존 양보 거리와 차 폭은 유지한다.
            for (float ahead = .5f; ahead <= CarLoopRoute.YieldAheadMeters; ahead += .5f)
            {
                Vector3 next = PoseAt(route, distance + ahead, offset).Position;
                Vector3 segment = next - previous;
                segment.y = 0;
                Vector3 relative = obstacle - previous;
                relative.y = 0;
                if (segment.sqrMagnitude > .000001f)
                {
                    float along = Vector3.Dot(relative, segment) / segment.sqrMagnitude;
                    if (
                        along > 0
                        && along <= 1
                        && (relative - segment * along).magnitude
                            < CarLoopRoute.YieldHalfWidthMeters
                    )
                        return true;
                }
                previous = next;
            }
            return false;
        }

        public static CarLoopRoute.Pose PoseAt(CarLoopRoute route, float distance, float offset)
        {
            if (offset <= 0)
                return route.PoseAt(distance);
            Vector3 position = PositionAt(route, distance, offset);
            Vector3 ahead = PositionAt(route, distance + .05f, offset);
            return new CarLoopRoute.Pose(position, (ahead - position).normalized);
        }

        /// <summary>한 대씩 구간(남행·북행 두 차로와 남쪽 유턴)에 있는지</summary>
        public static bool IsInSingleLane(CarLoopRoute route, float distance) =>
            route.PoseAt(distance).Position.z < SingleLaneNorthZ;

        /// <summary>한 대씩 구간으로 들어가는 곳. 남행 차로에서 우회를 시작하는 지점이다.</summary>
        public static float SingleLaneEntry(CarLoopRoute route) =>
            route.SouthboundDistance(SingleLaneNorthZ);

        private static Vector3 PositionAt(CarLoopRoute route, float distance, float offset)
        {
            CarLoopRoute.Pose pose = route.PoseAt(distance);
            if (pose.Forward.z > -.999f)
                return pose.Position;
            float enter = Mathf.SmoothStep(
                0,
                1,
                Mathf.InverseLerp(SingleLaneNorthZ, -14.5f, pose.Position.z)
            );
            float leave = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-25, -23, pose.Position.z));
            return pose.Position + Vector3.right * (offset * Mathf.Min(enter, leave));
        }
    }
}
