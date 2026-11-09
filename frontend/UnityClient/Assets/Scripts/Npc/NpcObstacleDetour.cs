using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>보행 데이터에 없는 물리 장애물도 몸이 지나갈 수 있는 쪽으로 우회한다.</summary>
    internal sealed class NpcObstacleDetour
    {
        private const float LookAheadMeters = 1.25f;
        private const float ClearanceMeters = .15f;
        private const float WaypointToleranceMeters = .12f;
        private const float RetrySeconds = .5f;

        // 길 지도에 없는 새 장애물은 표면 바로 앞에도 길 지도가 깔려 있고, 길 지도에 든 장애물은 둘레가 0.2m 넘게 비어 있다.
        private const float KnownObstacleGapMeters = .1f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Collider obstacle;
        private Vector3 first;
        private Vector3 second;
        private Vector3 forward;
        private bool secondLeg;
        private float nextAttempt;

        internal void Clear()
        {
            obstacle = null;
            nextAttempt = 0;
        }

        internal bool IsClearAhead(CharacterController body, Vector3 direction, float distance) =>
            IsClear(
                body,
                body.transform.position,
                body.transform.position + direction.normalized * distance,
                out _
            );

        internal Vector3 Direction(CharacterController body, Vector3 desired)
        {
            Vector3 position = body.transform.position;
            Vector3 ahead =
                position + desired.normalized * Mathf.Min(desired.magnitude, LookAheadMeters);
            if (IsClear(body, position, ahead, out Collider blocker))
            {
                // 횡단·새 경로로 장애물을 지난 뒤에도 이전 우회점으로 되돌아가지 않는다.
                obstacle = null;
                return desired;
            }
            if (obstacle != blocker)
                obstacle = null;
            if (
                obstacle != null
                && obstacle.enabled
                && obstacle.gameObject.activeInHierarchy
                && Vector3.Dot(desired.normalized, forward) >= -.5f
            )
            {
                Vector3 waypoint = secondLeg ? second : first;
                if (Flat(waypoint - position).magnitude <= WaypointToleranceMeters)
                {
                    if (secondLeg)
                        obstacle = null;
                    else
                    {
                        secondLeg = true;
                        waypoint = second;
                    }
                }
                if (obstacle != null)
                {
                    if (IsClear(body, position, waypoint, out _))
                        return Flat(waypoint - position);
                    // 우회하던 통로까지 새로 막히면 이전 우회점을 계속 바라보지 않고 다시 고른다.
                    obstacle = null;
                }
            }
            obstacle = null;
            if (Time.time < nextAttempt)
                return Vector3.zero;
            nextAttempt = Time.time + RetrySeconds;

            Bounds bounds = blocker.bounds;
            float padding =
                Mathf.Max(body.bounds.extents.x, body.bounds.extents.z) + ClearanceMeters;
            bounds.Expand(new Vector3(padding * 2, 0, padding * 2));
            bool alongX = Mathf.Abs(desired.x) >= Mathf.Abs(desired.z);
            bool positive = (alongX ? desired.x : desired.z) >= 0;
            float best = float.PositiveInfinity;
            for (int side = 0; side < 2; side++)
            {
                Vector3 near = position;
                Vector3 far = position;
                if (alongX)
                {
                    near.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
                    far.x = positive ? bounds.max.x : bounds.min.x;
                    near.z = far.z = side == 0 ? bounds.min.z : bounds.max.z;
                }
                else
                {
                    near.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
                    far.z = positive ? bounds.max.z : bounds.min.z;
                    near.x = far.x = side == 0 ? bounds.min.x : bounds.max.x;
                }
                if (
                    !OnWalkway(position, near, out near)
                    || !OnWalkway(near, far, out far)
                    || !IsClear(body, position, near, out _)
                    || !IsClear(body, near, far, out _)
                )
                {
                    continue;
                }
                float distance =
                    Flat(near - position).magnitude
                    + Flat(far - near).magnitude
                    + Flat(position + desired - far).magnitude;
                if (distance >= best)
                    continue;
                best = distance;
                first = near;
                second = far;
            }
            if (float.IsPositiveInfinity(best))
                return Vector3.zero;
            obstacle = blocker;
            forward = desired.normalized;
            secondLeg = false;
            return Flat(first - position);
        }

        private static bool OnWalkway(Vector3 from, Vector3 point, out Vector3 found)
        {
            found = point;
            int areas = NpcTownWalker.WalkingAreas;
            if (
                !NavMesh.SamplePosition(from, out NavMeshHit start, .4f, areas)
                || !NavMesh.SamplePosition(point, out NavMeshHit end, .3f, areas)
                || NavMesh.Raycast(start.position, end.position, out _, areas)
            )
                return false;
            found = end.position;
            return true;
        }

        private bool IsClear(
            CharacterController body,
            Vector3 from,
            Vector3 to,
            out Collider blocker
        )
        {
            blocker = null;
            Vector3 offset = Flat(to - from);
            if (offset.sqrMagnitude < .0001f)
                return true;
            Bounds bounds = body.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            // 몸체가 허용하는 접촉 여유까지 장애물로 판정하면 모서리에 나란히 서서 빠져나오지 못한다.
            float scale = Mathf.Max(
                Mathf.Abs(body.transform.lossyScale.x),
                Mathf.Abs(body.transform.lossyScale.z)
            );
            radius = Mathf.Max(.01f, radius - body.skinWidth * scale * .5f);
            Vector3 shift = from - body.transform.position;
            Vector3 top =
                bounds.center + shift + Vector3.up * Mathf.Max(0, bounds.extents.y - radius);
            Vector3 bottom = top;
            bottom.y = Mathf.Min(
                top.y,
                bounds.min.y + shift.y + Passerby.WalkableStepMeters + radius
            );
            int count = Physics.CapsuleCastNonAlloc(
                bottom,
                top,
                radius,
                offset.normalized,
                hits,
                offset.magnitude,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            float groundY = NavMesh.SamplePosition(
                from,
                out NavMeshHit ground,
                .4f,
                NavMesh.AllAreas
            )
                ? ground.position.y
                : from.y;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i].collider;
                if (
                    hit == body
                    || hit.GetComponentInParent<Passerby>() != null
                    || hit.GetComponentInParent<A305.Player.PlayerMover>() != null
                    || hit.bounds.max.y <= bounds.min.y + shift.y + Passerby.WalkableStepMeters
                )
                    continue;
                if (hits[i].distance >= closest)
                    continue;
                Vector3 center = bounds.center + shift;
                if (IsAvoidedByWalkingMap(hits[i], center, groundY))
                    continue;
                // 이미 몸체 가장자리에 닿은 지지대도 그 반대쪽으로 빠져나오는 걸음은 허용한다.
                if (hits[i].distance <= .001f && !(hit is MeshCollider mesh && !mesh.convex))
                {
                    Vector3 outward = Flat(center - hit.ClosestPoint(center));
                    if (Vector3.Dot(offset, outward) > .0001f)
                        continue;
                }
                closest = hits[i].distance;
                blocker = hit;
            }
            return blocker == null;
        }

        // 가로등·건물 벽처럼 길 지도에 이미 들어 있는 장애물인지. 길 지도는 장애물 둘레를 비워 두므로(굽는 격자 오차를 빼도 0.2m 넘게)
        // 장애물 표면 바로 앞 바닥에 길 지도가 없다. 이런 장애물은 길 지도의 경로가 이미 비켜 가므로 다시 막지 않는다.
        // 몸 크기 검사는 길 지도의 여유보다 조금 넓어, 경로가 붙어 지나가는 가로등 옆에서 막혔다고 보고 돌아갈 길을 못 찾아 멈췄다.
        private static bool IsAvoidedByWalkingMap(RaycastHit hit, Vector3 center, float groundY)
        {
            Vector3 surface;
            if (hit.distance > .001f)
                surface = hit.point;
            else if (hit.collider is MeshCollider mesh && !mesh.convex)
                return false;
            else
                surface = hit.collider.ClosestPoint(center);
            surface.y = groundY;
            return !NavMesh.SamplePosition(
                surface,
                out _,
                KnownObstacleGapMeters,
                NavMesh.AllAreas
            );
        }

        private static Vector3 Flat(Vector3 direction) =>
            Vector3.ProjectOnPlane(direction, Vector3.up);
    }
}
