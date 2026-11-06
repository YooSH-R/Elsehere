using UnityEngine;

namespace A305.Npc
{
    /// <summary>행인이 대화하지 않을 때 출퇴근 경로를 따른다. 대화 중에는 Passerby가 이동을 멈춘다.</summary>
    [RequireComponent(typeof(Passerby))]
    public sealed class NpcTownWalker : MonoBehaviour
    {
        private const float ArrivalToleranceMeters = .0001f;

        // 일터에서 말을 걸러 다가오는 속도는 0인 직원도 있으므로 출퇴근 속도와 분리한다.
        private const float MaximumWalkSpeedMetersPerSecond = 1.6f;

        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }

        internal void Walk()
        {
            var body = GetComponent<Passerby>();
            Vector3 direction = Destination - transform.position;
            direction.y = 0;
            float distance = direction.magnitude;
            if (distance > ArrivalToleranceMeters && Time.deltaTime > 0f)
                body.WalkToward(
                    direction,
                    Mathf.Min(distance / Time.deltaTime, MaximumWalkSpeedMetersPerSecond)
                );
            else
                body.StandFacing(transform.rotation);
        }
    }
}
