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
        public const float RequiredCrossingSeconds = 8f;
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
