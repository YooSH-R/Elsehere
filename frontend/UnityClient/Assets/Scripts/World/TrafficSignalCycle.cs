using UnityEngine;

namespace A305.World
{
    public enum SignalLight
    {
        Green,
        Yellow,
        Red,
    }

    /// <summary>
    /// 교차로 신호 주기. 본길 초록 → 노랑 → 빨강을 돌고, 본길이 빨강인 동안 가로길이 초록 → 노랑이 된다.
    /// </summary>
    public sealed class TrafficSignalCycle
    {
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
