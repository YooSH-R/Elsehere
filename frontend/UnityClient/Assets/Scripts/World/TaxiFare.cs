using UnityEngine;

namespace A305.World
{
    public enum TaxiPhase
    {
        /// <summary>손님 없이 본길을 돈다.</summary>
        Cruising,

        /// <summary>손님이 세워 그 옆 길가로 가는 중</summary>
        PullingOver,

        /// <summary>손님 옆에 서서 기다린다.</summary>
        Waiting,
    }

    /// <summary>
    /// 택시를 세우고 보내는 흐름. 가까운 택시에 타면 현재 위치에 서서 기다리고,
    /// 기사와 대화를 마치거나 손님이 멀리 떠나면 다시 달린다.
    /// </summary>
    public sealed class TaxiFare
    {
        /// <summary>손님이 이보다 멀어지면 기다리지 않고 떠난다.</summary>
        public const float LeaveDistanceMeters = 8f;

        /// <summary>택시 중심에서 방향과 관계없이 탈 수 있는 수평 반경</summary>
        public const float BoardingRadiusMeters = 8f;

        /// <summary>
        /// 손님 발이 택시보다 이만큼 넘게 높으면(바깥 계단 위·2층) 탈 수 없다. 보도 턱이나 계단 한두 칸은 탈 수 있다.
        /// </summary>
        public const float MaxBoardingRiseMeters = 1f;

        public TaxiPhase Phase { get; private set; } = TaxiPhase.Cruising;

        public static bool CanHail(Vector3 taxiPosition, Vector3 customer)
        {
            Vector3 offset = customer - taxiPosition;
            if (offset.y > MaxBoardingRiseMeters)
                return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= BoardingRadiusMeters * BoardingRadiusMeters;
        }

        public bool TryHail()
        {
            if (Phase != TaxiPhase.Cruising)
                return false;
            Phase = TaxiPhase.PullingOver;
            return true;
        }

        public void Arrive()
        {
            if (Phase == TaxiPhase.PullingOver)
                Phase = TaxiPhase.Waiting;
        }

        /// <param name="customerDistance">택시와 손님 사이 거리</param>
        /// <param name="isTalking">기사와 대화 중인지</param>
        public void Tick(float customerDistance, bool isTalking)
        {
            if (Phase == TaxiPhase.Cruising || isTalking)
                return;
            // 세우러 가는 동안에는 세운 자리까지의 거리만큼 더 떨어져 있어도 기다린다.
            float leaveDistance =
                Phase == TaxiPhase.PullingOver
                    ? BoardingRadiusMeters + LeaveDistanceMeters
                    : LeaveDistanceMeters;
            if (customerDistance > leaveDistance)
                Phase = TaxiPhase.Cruising;
        }

        /// <summary>기사와 대화를 마치면 손님을 내려 준 것으로 보고 다시 달린다.</summary>
        public void EndRide()
        {
            Phase = TaxiPhase.Cruising;
        }
    }
}
