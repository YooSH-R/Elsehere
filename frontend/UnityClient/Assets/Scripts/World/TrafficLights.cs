using System.Collections.Generic;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 교차로 신호등이 같은 주기로 바뀌게 한다. 본길의 차는 <see cref="Main"/>이 초록이 아니면 정지선에서 선다.
    /// 주기는 Inspector에서 조절한다.
    /// </summary>
    public sealed class TrafficLights : MonoBehaviour
    {
        [SerializeField]
        private float mainGreenSeconds = 12f;

        [SerializeField]
        private float crossGreenSeconds = 7f;

        [SerializeField]
        private float yellowSeconds = 3f;

        [SerializeField]
        private List<TrafficSignalHead> heads = new List<TrafficSignalHead>();

        private TrafficSignalCycle cycle;
        private float elapsedSeconds;

        public IReadOnlyList<TrafficSignalHead> Heads => heads;

        public SignalLight Main => Cycle.MainAt(elapsedSeconds);

        /// <summary>해당 차로가 충분한 시간 동안 빨간불이라 보행자가 횡단을 시작할 수 있는지.</summary>
        public bool CanCross(bool crossesMainRoad, float secondsNeeded)
        {
            float cycleLength = mainGreenSeconds + crossGreenSeconds + 2f * yellowSeconds;
            float phase = Mathf.Repeat(elapsedSeconds, cycleLength);
            if (crossesMainRoad)
                return Main == SignalLight.Red && cycleLength - phase >= secondsNeeded;
            return Cycle.CrossAt(elapsedSeconds) == SignalLight.Red
                && mainGreenSeconds + yellowSeconds - phase >= secondsNeeded;
        }

        private TrafficSignalCycle Cycle =>
            cycle ??= new TrafficSignalCycle(mainGreenSeconds, crossGreenSeconds, yellowSeconds);

        private void Start()
        {
            Show();
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            Show();
        }

        private void Show()
        {
            SignalLight main = Cycle.MainAt(elapsedSeconds);
            SignalLight cross = Cycle.CrossAt(elapsedSeconds);
            foreach (TrafficSignalHead head in heads)
            {
                if (head != null)
                {
                    head.Show(main, cross);
                }
            }
        }
    }
}
