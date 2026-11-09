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

        [SerializeField]
        private List<PedestrianSignalHead> pedestrianHeads = new List<PedestrianSignalHead>();

        private TrafficSignalCycle cycle;
        private float elapsedSeconds;

        public IReadOnlyList<TrafficSignalHead> Heads => heads;

        public IReadOnlyList<PedestrianSignalHead> PedestrianHeads => pedestrianHeads;

#if UNITY_EDITOR
        /// <summary>일과 관찰에서 대기 시간만 제거한다. 횡단 경로·기둥·차량 조건은 유지하며 빌드에는 포함하지 않는다.</summary>
        public bool ObserveWithoutPedestrianWait { get; set; }
#endif

        public SignalLight Main => Cycle.MainAt(elapsedSeconds);

        public float MaximumPedestrianWaitSeconds(bool crossesMainRoad) =>
            Cycle.MaximumPedestrianWaitSeconds(crossesMainRoad);

        /// <summary>해당 차로가 충분한 시간 동안 빨간불이라 보행자가 횡단을 시작할 수 있는지.</summary>
        public bool CanCross(bool crossesMainRoad, float secondsNeeded)
        {
#if UNITY_EDITOR
            if (ObserveWithoutPedestrianWait)
                return true;
#endif
            return Cycle.CanCross(elapsedSeconds, crossesMainRoad, secondsNeeded);
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
            foreach (PedestrianSignalHead head in pedestrianHeads)
                if (head != null)
                    head.Show(
                        Cycle.PedestrianAt(elapsedSeconds, head.CrossesMainRoad),
                        elapsedSeconds
                    );
        }
    }
}
