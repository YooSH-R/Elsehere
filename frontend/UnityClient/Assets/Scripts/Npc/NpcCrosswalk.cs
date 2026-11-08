using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>횡단 중 신호가 바뀌면 초반에만 되돌아가고, 선택한 인도까지 방향을 유지한다.</summary>
    internal sealed class NpcCrosswalk
    {
        private const float ReturnProgress = .2f;
        private const float SidewalkMargin = .4f;
        private Vector3 entrance;
        private Vector3 exit;
        private Vector3 forward;
        private bool returning;
        private bool committed;
        private bool crossesMainRoad;

        internal bool IsActive { get; private set; }

        internal void Clear()
        {
            IsActive = false;
            returning = false;
            committed = false;
        }

        internal Vector3 Direction(
            TrafficLights lights,
            Vector3 position,
            Vector3 requested,
            Vector3? plannedDirection = null
        )
        {
            if (!IsActive)
                Begin(lights, position, plannedDirection ?? requested);
            if (!IsActive)
                return requested;

            float length = Vector3.Dot(exit - entrance, forward);
            float progress = Vector3.Dot(position - entrance, forward) / length;
            if (progress > ReturnProgress)
                committed = true;
            if (!committed && lights != null && !lights.CanCross(crossesMainRoad, 0f))
                returning = true;

            Vector3 target = returning
                ? entrance - forward * SidewalkMargin
                : exit + forward * SidewalkMargin;
            Vector3 direction = Vector3.ProjectOnPlane(target - position, Vector3.up);
            Vector3 crossingForward = returning ? -forward : forward;
            if (
                Vector3.Dot(direction, crossingForward) <= .06f
                && !NpcTownWalker.IsRoadSurface(position, .1f)
            )
            {
                Clear();
                return Vector3.zero;
            }
            // 건널 방향은 유지하되, 앞사람을 비켜가는 보행 방향까지 덮어쓰지는 않는다.
            if (requested.sqrMagnitude > .0001f && Vector3.Dot(requested, crossingForward) >= 0)
                return Vector3.ClampMagnitude(requested, direction.magnitude);
            return direction;
        }

        private void Begin(TrafficLights lights, Vector3 position, Vector3 direction)
        {
            if (
                !NpcTownWalker.IsRoadSurface(position, .1f)
                || !NpcTownWalker.HasNearbySignal(lights, position)
                || direction.sqrMagnitude < .0001f
            )
                return;
            int area = NavMesh.GetAreaFromName("Crosswalk");
            if (
                area < 0
                || !NavMesh.SamplePosition(position, out NavMeshHit crossing, .3f, 1 << area)
            )
                return;
            crossesMainRoad = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            // 교차로의 경계는 대각선일 수도 있다. 교행 접선이나 가로·세로 축으로 출구를 정하면 차도 안에서 끝난다.
            forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            int mask = 1 << area;
            if (
                !NavMesh.Raycast(
                    crossing.position,
                    crossing.position - forward * 100f,
                    out NavMeshHit start,
                    mask
                )
                || !NavMesh.Raycast(
                    crossing.position,
                    crossing.position + forward * 100f,
                    out NavMeshHit end,
                    mask
                )
                || Vector3.Dot(end.position - start.position, forward) < .2f
            )
                return;
            entrance = start.position;
            exit = end.position;
            returning = false;
            committed = false;
            IsActive = true;
        }
    }
}
