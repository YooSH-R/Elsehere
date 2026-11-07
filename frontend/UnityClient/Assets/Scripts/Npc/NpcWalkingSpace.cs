using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>캡슐보다 넓은 리틀 멍의 머리까지 고려해 등장 위치와 보행 간격을 확보한다.</summary>
    internal static class NpcWalkingSpace
    {
        private const float GapMeters = .06f;
        private const float ArrivalSearchRadiusMeters = 9f;
        private static readonly List<Passerby> Bodies = new List<Passerby>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Bodies.Clear();

        internal static void Register(Passerby body)
        {
            if (!Bodies.Contains(body))
                Bodies.Add(body);
        }

        internal static void Unregister(Passerby body) => Bodies.Remove(body);

        private static float Radius(Passerby body)
        {
            var capsule = body.GetComponent<CharacterController>();
            Vector3 scale = body.transform.lossyScale;
            // 서비스 캐릭터는 머리 폭이 키의 약 0.6배다. 몸통 캡슐만 쓰면 얼굴끼리 먼저 겹친다.
            return Mathf.Max(capsule.radius, capsule.height * .32f)
                * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        internal static bool TryFindArrival(Passerby body, Vector3 target, out Vector3 position)
        {
            float spacing = Radius(body) * 2f + GapMeters;
            for (int ring = 0; ring * spacing <= ArrivalSearchRadiusMeters; ring++)
            {
                int count = ring == 0 ? 1 : ring * 12;
                for (int slot = 0; slot < count; slot++)
                {
                    float angle = slot * Mathf.PI * 2f / count;
                    Vector3 candidate =
                        target
                        + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * spacing);
                    if (
                        !NavMesh.SamplePosition(
                            candidate,
                            out NavMeshHit hit,
                            .2f,
                            NavMesh.AllAreas
                        )
                    )
                        continue;
                    if (NavMesh.Raycast(target, hit.position, out _, NavMesh.AllAreas))
                        continue;
                    if (!IsFree(body, hit.position))
                        continue;
                    position = hit.position;
                    return true;
                }
            }
            position = target;
            return false;
        }

        private static bool IsFree(Passerby body, Vector3 position)
        {
            float radius = Radius(body);
            foreach (var other in Bodies)
            {
                if (
                    other == null
                    || other == body
                    || !SameFloor(position, other.transform.position)
                )
                    continue;
                Vector3 offset = Vector3.ProjectOnPlane(
                    position - other.transform.position,
                    Vector3.up
                );
                float clearance = radius + Radius(other) + GapMeters;
                if (offset.sqrMagnitude < clearance * clearance)
                    return false;
            }
            return true;
        }

        internal static float LimitStep(Passerby body, Vector3 direction, float distance)
        {
            FindBlocker(body, direction, distance, out float step, out _);
            return step;
        }

        internal static Passerby FindBlocker(
            Passerby body,
            Vector3 direction,
            float distance,
            out float step,
            out float clearance
        )
        {
            float radius = Radius(body);
            step = distance;
            clearance = 0;
            Passerby blocker = null;
            foreach (var other in Bodies)
            {
                if (
                    other == null
                    || other == body
                    || !SameFloor(body.transform.position, other.transform.position)
                )
                    continue;
                Vector3 offset = Vector3.ProjectOnPlane(
                    body.transform.position - other.transform.position,
                    Vector3.up
                );
                float along = Vector3.Dot(offset, direction);
                if (along >= 0f)
                    continue; // 이미 가까워도 상대에게서 멀어지는 움직임은 허용한다.
                float requiredSpace = radius + Radius(other) + GapMeters;
                float sidewaysSquared = Mathf.Max(0, offset.sqrMagnitude - along * along);
                if (sidewaysSquared >= requiredSpace * requiredSpace)
                    continue;
                float entry = Mathf.Max(
                    0,
                    -along - Mathf.Sqrt(requiredSpace * requiredSpace - sidewaysSquared)
                );
                if (entry <= step)
                {
                    blocker = other;
                    step = entry;
                    clearance = requiredSpace;
                }
            }
            return blocker;
        }

        private static bool SameFloor(Vector3 first, Vector3 second) =>
            Mathf.Abs(first.y - second.y) < 1f;
    }
}
