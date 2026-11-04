namespace A305.World
{
    public enum PolicePhase
    {
        /// <summary>경찰서 옆 주차 칸에 서 있다.</summary>
        Parked,

        /// <summary>주차 칸에서 본길로 나가는 중</summary>
        LeavingBay,

        /// <summary>신고한 장소로 가는 중</summary>
        Responding,

        /// <summary>장소 없이 신고를 받아 본길을 한 바퀴 도는 중</summary>
        Patrolling,

        /// <summary>신고한 장소 앞에 서 있다.</summary>
        OnScene,

        /// <summary>본길을 따라 경찰서 쪽으로 돌아오는 중</summary>
        Returning,

        /// <summary>본길에서 주차 칸으로 들어가는 중</summary>
        EnteringBay,
    }

    /// <summary>
    /// 순찰차의 출동 흐름. 신고를 받으면 경광등을 켜고 주차 칸을 나와, 장소가 있으면 그 앞에서 잠시 서 있다가 돌아오고,
    /// 장소가 없으면 본길을 한 바퀴 순찰하고 돌아온다. 돌아오는 길에는 경광등을 끈다.
    /// 단계마다 가야 할 곳(진입로 끝, 장소 앞, 돌아올 지점)에 닿았는지는 차를 움직이는 쪽이 알려 준다.
    /// </summary>
    public sealed class PoliceDispatch
    {
        /// <summary>신고한 장소 앞에 서 있는 시간</summary>
        public const float OnSceneSeconds = 20f;

        private float onSceneElapsed;

        public PolicePhase Phase { get; private set; } = PolicePhase.Parked;

        /// <summary>출동한 장소 코드. 순찰이거나 서 있을 때는 null이다.</summary>
        public string SceneCode { get; private set; }

        public bool LightsOn =>
            Phase == PolicePhase.LeavingBay
            || Phase == PolicePhase.Responding
            || Phase == PolicePhase.Patrolling
            || Phase == PolicePhase.OnScene;

        /// <param name="sceneCode">신고한 장소 코드. null이면 순찰한다.</param>
        /// <returns>서 있다가 출동했으면 true. 이미 나가 있으면 새 신고는 받지 않는다.</returns>
        public bool TryDispatch(string sceneCode)
        {
            if (Phase != PolicePhase.Parked)
                return false;
            SceneCode = sceneCode;
            Phase = PolicePhase.LeavingBay;
            return true;
        }

        /// <param name="reachedLegEnd">지금 단계에서 가야 할 곳에 닿았는지</param>
        public void Tick(bool reachedLegEnd, float deltaSeconds)
        {
            switch (Phase)
            {
                case PolicePhase.LeavingBay when reachedLegEnd:
                    Phase = SceneCode != null ? PolicePhase.Responding : PolicePhase.Patrolling;
                    break;
                case PolicePhase.Responding when reachedLegEnd:
                    Phase = PolicePhase.OnScene;
                    onSceneElapsed = 0;
                    break;
                case PolicePhase.OnScene:
                    onSceneElapsed += deltaSeconds;
                    if (onSceneElapsed >= OnSceneSeconds)
                        Phase = PolicePhase.Returning;
                    break;
                case PolicePhase.Patrolling when reachedLegEnd:
                case PolicePhase.Returning when reachedLegEnd:
                    Phase = PolicePhase.EnteringBay;
                    break;
                case PolicePhase.EnteringBay when reachedLegEnd:
                    Phase = PolicePhase.Parked;
                    SceneCode = null;
                    break;
            }
        }
    }
}
