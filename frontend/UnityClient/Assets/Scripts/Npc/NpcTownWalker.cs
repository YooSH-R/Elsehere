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

        private Passerby passing;
        private Vector3 passingForward;
        private bool passingOnRight;
        private float passingClearance;

        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }

        private void OnDisable() => passing = null;

        internal void Walk()
        {
            var body = GetComponent<Passerby>();
            Vector3 direction = Destination - transform.position;
            direction.y = 0;
            float distance = direction.magnitude;
            if (distance > ArrivalToleranceMeters && Time.deltaTime > 0f)
                body.WalkToward(
                    PassingDirection(body, direction),
                    Mathf.Min(distance / Time.deltaTime, MaximumWalkSpeedMetersPerSecond)
                );
            else
                body.StandFacing(transform.rotation);
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

        private bool DestinationIsOccupied(Passerby other, float clearance) =>
            Vector3.ProjectOnPlane(Destination - other.transform.position, Vector3.up).sqrMagnitude
            < clearance * clearance;

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
                !NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit start,
                    .3f,
                    NavMesh.AllAreas
                )
                || !NavMesh.SamplePosition(waypoint, out NavMeshHit end, .2f, NavMesh.AllAreas)
                || NavMesh.Raycast(start.position, end.position, out _, NavMesh.AllAreas)
            )
                return false;
            return direction.sqrMagnitude > .001f;
        }
    }
}
