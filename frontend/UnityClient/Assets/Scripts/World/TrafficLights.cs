using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace A305.World
{
    /// <summary>
    /// 교차로 신호등이 같은 주기로 바뀌게 한다. 본길의 차는 <see cref="Main"/>이 초록이 아니면 정지선에서 선다.
    /// 주기는 Inspector에서 조절한다.
    /// </summary>
    public sealed class TrafficLights : MonoBehaviour
    {
        /// <summary>
        /// 가로길 초록. 이 동안(과 노랑) 본길이 빨간불이라 보행자가 본길을 건넌다. 7초에서는 본길(약 6.6m)을 건너기 시작할 수 있는 때가
        /// 주기마다 약 5초뿐이라 횡단보도 앞에 줄이 밀려 10초로 늘렸다(본길 차는 그만큼 더 기다린다).
        /// </summary>
        public const float CrossGreenSeconds = 10f;

        [SerializeField]
        private float mainGreenSeconds = 12f;

        [SerializeField]
        private float crossGreenSeconds = CrossGreenSeconds;

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
                        Cycle.PedestrianAt(
                            elapsedSeconds,
                            head.CrossesMainRoad,
                            SecondsToCross(head)
                        ),
                        elapsedSeconds
                    );
        }

        // 보행 신호는 자기 횡단보도를 건너는 데 드는 시간만큼 남았을 때까지 걷기를 보인다. NPC도 같은 기준으로 출발한다.
        private readonly Dictionary<PedestrianSignalHead, float> crossingSeconds =
            new Dictionary<PedestrianSignalHead, float>();

        private float SecondsToCross(PedestrianSignalHead head)
        {
            if (crossingSeconds.TryGetValue(head, out float seconds))
                return seconds;
            if (!TryMeasureCrosswalk(head, out float meters))
                // 길 지도가 아직 없으면 넉넉한 시간을 쓰고, 다음에 다시 잰다.
                return TrafficSignalCycle.RequiredCrossingSeconds;
            seconds = TrafficSignalCycle.SecondsToCross(meters);
            crossingSeconds[head] = seconds;
            return seconds;
        }

        // 신호가 맡은 횡단보도의 길이. 건너는 방향(본길이면 동서, 가로길이면 남북)으로 횡단보도 구역의 양 끝까지 잰다.
        private static bool TryMeasureCrosswalk(PedestrianSignalHead head, out float meters)
        {
            meters = 0;
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            if (
                crossing < 0
                || !NavMesh.SamplePosition(
                    head.CrossingPoint,
                    out NavMeshHit middle,
                    1f,
                    1 << crossing
                )
            )
                return false;
            Vector3 axis = head.CrossesMainRoad ? Vector3.right : Vector3.forward;
            if (
                !NavMesh.Raycast(
                    middle.position,
                    middle.position + axis * 100f,
                    out NavMeshHit far,
                    1 << crossing
                )
                || !NavMesh.Raycast(
                    middle.position,
                    middle.position - axis * 100f,
                    out NavMeshHit near,
                    1 << crossing
                )
            )
                return false;
            meters = Vector3.ProjectOnPlane(far.position - near.position, Vector3.up).magnitude;
            return meters > .5f;
        }
    }
}
