using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>행인이 대화하지 않을 때 출퇴근 경로를 따른다. 대화 중에는 Passerby가 이동을 멈춘다.</summary>
    [RequireComponent(typeof(Passerby))]
    public sealed class NpcTownWalker : MonoBehaviour
    {
        private const float ArrivalToleranceMeters = .0001f;

        // 일터에서 말을 걸러 다가오는 속도는 0인 직원도 있으므로 출퇴근 속도와 분리한다.
        private const float MaximumWalkSpeedMetersPerSecond = 1.6f;
        private const float PassingLookAheadMeters = 1.25f;
        private const float CrossingLookAheadMeters = .6f;
        private const float RequiredRedSeconds = TrafficSignalCycle.RequiredCrossingSeconds;
        private const float SignalRadiusMeters = 8f;
        private NavMeshPath path;

        private Passerby passing;
        private Vector3 passingForward;
        private bool passingOnRight;
        private float passingClearance;

        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }

        /// <summary>횡단보도 위에서는 대화나 새 제안으로 걸음을 멈추지 않는다.</summary>
        public bool IsCrossing => IsRoadSurface(transform.position, .1f);

        internal static int WalkingAreas => WithoutArea(NavMesh.AllAreas, "Road");
        internal static int StandingAreas => WithoutArea(WalkingAreas, "Crosswalk");

        private void Awake() => path = new NavMeshPath();

        private void OnDisable() => passing = null;

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
            if (direction.magnitude <= ArrivalToleranceMeters || Time.deltaTime <= 0f)
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
                MaximumWalkSpeedMetersPerSecond,
                direction.magnitude / Time.deltaTime
            );
            body.WalkToward(PassingDirection(body, direction), speed);
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
            bool crossesMainRoad = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            return HasNearbySignal(lights, ahead)
                && !lights.CanCross(crossesMainRoad, RequiredRedSeconds);
        }

        internal static bool HasNearbySignal(TrafficLights lights, Vector3 position)
        {
            if (lights == null)
                return false;
            foreach (TrafficSignalHead head in lights.Heads)
            {
                if (head == null)
                    continue;
                Vector3 offset = head.transform.position - position;
                offset.y = 0;
                if (offset.sqrMagnitude <= SignalRadiusMeters * SignalRadiusMeters)
                    return true;
            }
            foreach (PedestrianSignalHead head in lights.PedestrianHeads)
            {
                if (head == null)
                    continue;
                Vector3 offset = head.CrossingPoint - position;
                offset.y = 0;
                if (offset.sqrMagnitude <= SignalRadiusMeters * SignalRadiusMeters)
                    return true;
            }
            return false;
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

        private Vector3 PassingDirection(Passerby body, Vector3 destinationOffset)
        {
            Vector3 forward = destinationOffset.normalized;
            if (passing != null)
            {
                Vector3 toOther = passing.transform.position - transform.position;
                // 상대를 지난 뒤에만 원래 경로로 돌아간다. 매 프레임 좌우를 다시 고르면 몸이 흔들린다.
                if (
                    !passing.isActiveAndEnabled
                    || Vector3.Dot(toOther, passingForward) < -passingClearance * .5f
                    || Vector3.Dot(forward, passingForward) < 0
                    || DestinationIsOccupied(passing, passingClearance)
                )
                    passing = null;
            }

            if (passing == null)
            {
                var other = NpcWalkingSpace.FindBlocker(
                    body,
                    forward,
                    Mathf.Min(PassingLookAheadMeters, destinationOffset.magnitude),
                    out _,
                    out float clearance
                );
                // 목적지 자체가 차 있으면 그 둘레를 계속 맴돌지 않고 간격을 두고 기다린다.
                if (other == null || DestinationIsOccupied(other, clearance))
                    return destinationOffset;
                passingForward = forward;
                passingOnRight = true;
                passingClearance = clearance;
                if (!TryPassingDirection(other, out _))
                {
                    passingOnRight = false;
                    if (!TryPassingDirection(other, out _))
                        return destinationOffset;
                }
                passing = other;
            }

            return TryPassingDirection(passing, out Vector3 direction)
                ? direction
                : destinationOffset;
        }

        private bool DestinationIsOccupied(Passerby other, float clearance)
        {
            // 서로 자리를 바꾸며 걷는 상대를 고정된 목적지 점유자로 보면 가까운 거리에서 둘 다 멈춘다.
            bool leaving =
                other.TryGetComponent(out NpcTownWalker walker)
                && walker.isActiveAndEnabled
                && (other.Phase == PasserbyPhase.Waiting || other.Phase == PasserbyPhase.Resting)
                && Vector3
                    .ProjectOnPlane(walker.Destination - other.transform.position, Vector3.up)
                    .sqrMagnitude > .01f;
            return !leaving
                && Vector3
                    .ProjectOnPlane(Destination - other.transform.position, Vector3.up)
                    .sqrMagnitude
                    < clearance * clearance;
        }

        private bool TryPassingDirection(Passerby other, out Vector3 direction)
        {
            Vector3 offset = Vector3.ProjectOnPlane(
                other.transform.position - transform.position,
                Vector3.up
            );
            Vector3 towardOther = offset.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, towardOther);
            if (!passingOnRight)
                side = -side;
            // 상대 뒤의 지점을 바로 향하면 여유 공간을 가로질러 다시 막힌다. 바깥 원의 접선으로 걷는다.
            float sine = Mathf.Clamp01(
                (passingClearance + .12f) / Mathf.Max(offset.magnitude, .001f)
            );
            direction = towardOther * Mathf.Sqrt(1f - sine * sine) + side * sine;
            Vector3 waypoint = transform.position + direction * PassingLookAheadMeters;
            if (
                !NavMesh.SamplePosition(transform.position, out NavMeshHit start, .3f, WalkingAreas)
                || !NavMesh.SamplePosition(waypoint, out NavMeshHit end, .2f, WalkingAreas)
                || NavMesh.Raycast(start.position, end.position, out _, WalkingAreas)
            )
                return false;
            return direction.sqrMagnitude > .001f;
        }
    }
}
