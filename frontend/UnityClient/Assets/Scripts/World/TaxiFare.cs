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
    /// 택시를 세우고 보내는 흐름. 인도에서 다가오는 택시를 세우면 손님 옆 길가에 서서 기다리고,
    /// 기사와 대화를 마치거나 손님이 멀리 떠나면 다시 달린다.
    /// </summary>
    public sealed class TaxiFare
    {
        /// <summary>손님이 이보다 멀어지면 기다리지 않고 떠난다.</summary>
        public const float LeaveDistanceMeters = 8f;

        // 택시 가운데에서 앞으로 이 범위 안에 있는 손님만 세울 수 있다. 너무 가까우면 설 거리가 모자란다.
        private const float MinAheadMeters = 4f;
        private const float MaxAheadMeters = 18f;

        // 손님은 택시가 달리는 차로 쪽 인도(차로 오른쪽)에 서 있어야 한다.
        private const float MinCurbsideMeters = 1f;
        private const float MaxCurbsideMeters = 5f;

        public TaxiPhase Phase { get; private set; } = TaxiPhase.Cruising;

        public static bool CanHail(CarLoopRoute route, float taxiDistance, Vector3 customer)
        {
            CarLoopRoute.Pose taxi = route.PoseAt(taxiDistance);
            Vector3 offset = customer - taxi.Position;
            offset.y = 0f;
            float ahead = Vector3.Dot(offset, taxi.Forward);
            float curbside = Vector3.Dot(offset, Vector3.Cross(Vector3.up, taxi.Forward));
            if (ahead < MinAheadMeters || ahead > MaxAheadMeters)
                return false;
            if (curbside < MinCurbsideMeters || curbside > MaxCurbsideMeters)
                return false;
            // 설 자리가 유턴 구간이면 세우지 않는다. 같은 직선 차로에 있어야 방향이 같다.
            Vector3 stopForward = route.PoseAt(taxiDistance + ahead).Forward;
            return Vector3.Dot(stopForward, taxi.Forward) > .999f;
        }

        /// <summary>손님 바로 옆에 택시 가운데가 오도록 설 거리</summary>
        public static float PullOverDistance(
            CarLoopRoute route,
            float taxiDistance,
            Vector3 customer
        )
        {
            CarLoopRoute.Pose taxi = route.PoseAt(taxiDistance);
            Vector3 offset = customer - taxi.Position;
            offset.y = 0f;
            return taxiDistance + Vector3.Dot(offset, taxi.Forward);
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
                    ? MaxAheadMeters + LeaveDistanceMeters
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
