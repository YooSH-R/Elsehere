using UnityEngine;

namespace A305.World
{
    /// <summary>남쪽 도로에 주차한 차량을 지나갈 때의 완만한 우회 경로.</summary>
    public static class CarLaneBypass
    {
        public static CarLoopRoute.Pose PoseAt(CarLoopRoute route, float distance, float offset)
        {
            if (offset <= 0)
                return route.PoseAt(distance);
            Vector3 position = PositionAt(route, distance, offset);
            Vector3 ahead = PositionAt(route, distance + .05f, offset);
            return new CarLoopRoute.Pose(position, (ahead - position).normalized);
        }

        private static Vector3 PositionAt(CarLoopRoute route, float distance, float offset)
        {
            CarLoopRoute.Pose pose = route.PoseAt(distance);
            if (pose.Forward.z > -.999f)
                return pose.Position;
            float enter = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-12, -14.5f, pose.Position.z));
            float leave = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-25, -23, pose.Position.z));
            return pose.Position + Vector3.right * (offset * Mathf.Min(enter, leave));
        }
    }
}
