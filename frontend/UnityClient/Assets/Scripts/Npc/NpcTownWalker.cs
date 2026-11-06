using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>행인이 대화하지 않을 때 출퇴근 경로를 따른다. 대화 중에는 Passerby가 이동을 멈춘다.</summary>
    [RequireComponent(typeof(Passerby))]
    public sealed class NpcTownWalker : MonoBehaviour
    {
        private const float WalkSpeedMetersPerSecond = 1.6f;
        private const float CrossingLookAheadMeters = .6f;
        private const float RequiredRedSeconds = 8f;
        private const float SignalRadiusMeters = 8f;
        private NavMeshPath path;
        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }

        /// <summary>횡단보도 위에서는 대화나 새 제안으로 걸음을 멈추지 않는다.</summary>
        public bool IsCrossing => IsRoadSurface(transform.position, .1f);

        internal static int WalkingAreas => WithoutArea(NavMesh.AllAreas, "Road");
        internal static int StandingAreas => WithoutArea(WalkingAreas, "Crosswalk");

        private void Awake()
        {
            // Unity의 경로 객체는 에셋 역직렬화 스레드에서 생성할 수 없다.
            path = new NavMeshPath();
        }

        private static int WithoutArea(int areas, string name)
        {
            int area = NavMesh.GetAreaFromName(name);
            return area < 0 ? areas : areas & ~(1 << area);
        }

        internal void Walk()
        {
            var body = GetComponent<Passerby>();
            Vector3 direction = Destination - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude <= .01f)
            {
                body.StandFacing(transform.rotation);
                return;
            }

            int areas = WalkingAreas;
            if (
                !NavMesh.SamplePosition(transform.position, out NavMeshHit start, 1f, areas)
                || !NavMesh.SamplePosition(Destination, out NavMeshHit end, 1f, areas)
            )
            {
                body.StandFacing(transform.rotation);
                return;
            }
            if (NavMesh.Raycast(start.position, end.position, out _, areas))
            {
                if (
                    !NavMesh.CalculatePath(start.position, end.position, areas, path)
                    || path.status != NavMeshPathStatus.PathComplete
                    || path.corners.Length < 2
                )
                {
                    body.StandFacing(transform.rotation);
                    return;
                }
                direction = path.corners[1] - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude < .01f && path.corners.Length > 2)
                {
                    direction = path.corners[2] - transform.position;
                    direction.y = 0;
                }
            }

            float speed = Mathf.Min(
                WalkSpeedMetersPerSecond,
                direction.magnitude / Mathf.Max(Time.deltaTime, .001f)
            );
            body.WalkToward(direction, speed);
        }

        internal static bool ShouldWaitForSignal(
            TrafficLights lights,
            Vector3 position,
            Vector3 direction
        )
        {
            if (lights == null || IsRoadSurface(position, .1f) || direction.sqrMagnitude < .0001f)
                return false;
            Vector3 ahead = position + direction.normalized * CrossingLookAheadMeters;
            if (!OnCrosswalk(ahead, .2f))
                return false;
            bool controlled = false;
            foreach (TrafficSignalHead head in lights.Heads)
            {
                if (head == null)
                    continue;
                Vector3 offset = head.transform.position - ahead;
                offset.y = 0;
                if (offset.sqrMagnitude <= SignalRadiusMeters * SignalRadiusMeters)
                {
                    controlled = true;
                    break;
                }
            }
            bool crossesMainRoad = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            return controlled && !lights.CanCross(crossesMainRoad, RequiredRedSeconds);
        }

        internal static bool IsRoadSurface(Vector3 point, float distance)
        {
            int road = NavMesh.GetAreaFromName("Road");
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            int areas = (road < 0 ? 0 : 1 << road) | (crossing < 0 ? 0 : 1 << crossing);
            return areas != 0 && NavMesh.SamplePosition(point, out _, distance, areas);
        }

        private static bool OnCrosswalk(Vector3 point, float distance)
        {
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            return crossing >= 0 && NavMesh.SamplePosition(point, out _, distance, 1 << crossing);
        }
    }
}
