using UnityEngine;

namespace A305.World
{
    public enum SignalLight
    {
        Green,
        Yellow,
        Red,
    }

    public enum PedestrianLight
    {
        Stop,
        Walk,
        Clearance,
    }

    /// <summary>
    /// 교차로 신호 주기. 본길 초록 → 노랑 → 빨강을 돌고, 본길이 빨강인 동안 가로길이 초록 → 노랑이 된다.
    /// </summary>
    public sealed class TrafficSignalCycle
    {
        /// <summary>횡단보도 길이를 잴 수 없을 때 쓰는 넉넉한 횡단 시간. 출발 예상 시각(최대 대기)도 이 값으로 넉넉히 잡는다.</summary>
        public const float RequiredCrossingSeconds = 8f;

        /// <summary>NPC 기본 보행 속도. 급한 NPC도 횡단보도에서는 걷는다.</summary>
        public const float PedestrianWalkMetersPerSecond = 1.6f;

        /// <summary>건너는 시간에 더하는 여유</summary>
        public const float CrossingMarginSeconds = 1f;
        private readonly float mainGreenSeconds;
        private readonly float crossGreenSeconds;
        private readonly float yellowSeconds;

        public TrafficSignalCycle(
            float mainGreenSeconds,
            float crossGreenSeconds,
            float yellowSeconds
        )
        {
            this.mainGreenSeconds = mainGreenSeconds;
            this.crossGreenSeconds = crossGreenSeconds;
            this.yellowSeconds = yellowSeconds;
        }

        private float Length => mainGreenSeconds + crossGreenSeconds + 2f * yellowSeconds;

        public float MaximumPedestrianWaitSeconds(bool crossesMainRoad)
        {
            float redSeconds =
                (crossesMainRoad ? crossGreenSeconds : mainGreenSeconds) + yellowSeconds;
            float startWindow = redSeconds - RequiredCrossingSeconds;
            return startWindow > 0 ? Length - startWindow : float.PositiveInfinity;
        }

        /// <summary>
        /// 길이가 <paramref name="lengthMeters"/>인 횡단보도를 NPC 보행 속도로 건너는 시간에 여유를 더한 것. 이만큼 빨간불이 남아 있으면 출발한다.
        /// 예전처럼 모든 횡단보도에 8초를 요구하면, 본길(약 6.6m, 4초 거리)은 빨간불 10초 중 앞 2초에만 출발할 수 있어 줄이 밀렸다.
        /// </summary>
        public static float SecondsToCross(float lengthMeters) =>
            lengthMeters / PedestrianWalkMetersPerSecond + CrossingMarginSeconds;

        /// <summary>건너는 데 <paramref name="secondsNeeded"/>초가 드는 횡단보도의 보행 신호. 그만큼 남았을 때까지 걷기, 그 뒤는 깜빡임이다.</summary>
        public PedestrianLight PedestrianAt(
            float seconds,
            bool crossesMainRoad,
            float secondsNeeded
        )
        {
            if (CanCross(seconds, crossesMainRoad, secondsNeeded))
                return PedestrianLight.Walk;
            return CanCross(seconds, crossesMainRoad, 0)
                ? PedestrianLight.Clearance
                : PedestrianLight.Stop;
        }

        public PedestrianLight PedestrianAt(float seconds, bool crossesMainRoad)
        {
            if (CanCross(seconds, crossesMainRoad, RequiredCrossingSeconds))
                return PedestrianLight.Walk;
            return CanCross(seconds, crossesMainRoad, 0)
                ? PedestrianLight.Clearance
                : PedestrianLight.Stop;
        }

        public bool CanCross(float seconds, bool crossesMainRoad, float secondsNeeded)
        {
            float phase = Mathf.Repeat(seconds, Length);
            if (crossesMainRoad)
                return MainAt(seconds) == SignalLight.Red && Length - phase >= secondsNeeded;
            return CrossAt(seconds) == SignalLight.Red
                && mainGreenSeconds + yellowSeconds - phase >= secondsNeeded;
        }

        public SignalLight MainAt(float seconds)
        {
            float t = Mathf.Repeat(seconds, Length);
            if (t < mainGreenSeconds)
                return SignalLight.Green;
            return t < mainGreenSeconds + yellowSeconds ? SignalLight.Yellow : SignalLight.Red;
        }

        public SignalLight CrossAt(float seconds)
        {
            float t = Mathf.Repeat(seconds, Length) - (mainGreenSeconds + yellowSeconds);
            if (t < 0f)
                return SignalLight.Red;
            return t < crossGreenSeconds ? SignalLight.Green : SignalLight.Yellow;
        }
    }
}
