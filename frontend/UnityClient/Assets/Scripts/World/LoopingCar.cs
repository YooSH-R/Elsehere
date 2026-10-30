using System.Collections.Generic;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 마을 본길을 천천히 도는 차. 본길 신호가 초록이 아니면 교차로 정지선에서 서고,
    /// 차 앞을 사람이나 다른 차가 막고 있으면 선다. 택시처럼 손님 옆에 서야 할 때는 <see cref="PullOverAt"/>으로
    /// 설 자리를 정하고 <see cref="Release"/>로 다시 보낸다. 길 계산은 <see cref="CarLoopRoute"/>가 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoopingCar : MonoBehaviour
    {
        private static readonly List<LoopingCar> Driving = new List<LoopingCar>();

        [SerializeField]
        [Tooltip("남쪽 유턴 지점 z")]
        private float southZ = -6f;

        [SerializeField]
        [Tooltip("북쪽 유턴 지점 z")]
        private float northZ = 66f;

        [SerializeField]
        [Tooltip("도로 가운데에서 차로 가운데까지의 거리(m)")]
        private float laneOffset = 1.5f;

        [SerializeField]
        private float speedMetersPerSecond = 2.5f;

        [SerializeField]
        [Tooltip("길 위 출발 거리(m). 차마다 달리해 떨어져 달리게 한다.")]
        private float startDistance;

        [SerializeField]
        [Tooltip(
            "남쪽 주차 트럭을 피해 중앙 쪽으로 비켜가는 거리. 남쪽까지 운행하는 택시에만 설정한다."
        )]
        private float parkedTruckBypassOffset;

        [SerializeField]
        private TrafficLights lights;

        [SerializeField]
        [Tooltip("본길 신호를 보고 서는 정지선. 길 위 거리(m)로 적는다.")]
        private List<float> signalStops = new List<float>();

        [SerializeField]
        private PlayerMover player;

        private readonly List<float> stops = new List<float>();
        private float? pullOverDistance;
        private float height;
        private bool passengerOnBoard;

        public void SetPassengerOnBoard(bool onBoard) => passengerOnBoard = onBoard;

        public TrafficLights Lights => lights;

        public CarLoopRoute Route { get; private set; }

        /// <summary>출발점에서 잰 지금 위치(m)</summary>
        public float Distance { get; private set; }

        /// <summary>정한 자리에 세웠고 그 자리에 섰는지</summary>
        public bool HasPulledOver =>
            pullOverDistance.HasValue
            && Mathf.Repeat(pullOverDistance.Value - Distance, Route.Length) < .01f;

        public void PullOverAt(float distance)
        {
            pullOverDistance = distance;
        }

        public void Release()
        {
            pullOverDistance = null;
        }

        private void Awake()
        {
            Route = new CarLoopRoute(southZ, northZ, laneOffset);
            Distance = startDistance;
            height = transform.position.y;
        }

        private void OnEnable()
        {
            Driving.Add(this);
        }

        private void OnDisable()
        {
            Driving.Remove(this);
        }

        private void Update()
        {
            CarLoopRoute.Pose pose = CarLaneBypass.PoseAt(Route, Distance, parkedTruckBypassOffset);
            if (IsBlocked(pose))
            {
                return;
            }

            stops.Clear();
            if (lights != null && lights.Main != SignalLight.Green)
            {
                stops.AddRange(signalStops);
            }

            if (pullOverDistance.HasValue)
            {
                stops.Add(pullOverDistance.Value);
            }

            bool isPassing = Mathf.Abs(pose.Position.x - Route.PoseAt(Distance).Position.x) > .01f;
            float speed = isPassing ? Mathf.Min(3, speedMetersPerSecond) : speedMetersPerSecond;
            Distance = Route.Advance(Distance, speed * Time.deltaTime, stops);
            Place();
        }

        private bool IsBlocked(CarLoopRoute.Pose pose)
        {
            if (
                !passengerOnBoard
                && player != null
                && CarLoopRoute.ShouldYield(pose.Position, pose.Forward, player.transform.position)
            )
            {
                return true;
            }

            foreach (LoopingCar other in Driving)
            {
                if (
                    other != this
                    && CarLoopRoute.ShouldYield(
                        pose.Position,
                        pose.Forward,
                        other.transform.position
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private void Place()
        {
            CarLoopRoute.Pose pose = CarLaneBypass.PoseAt(Route, Distance, parkedTruckBypassOffset);
            transform.SetPositionAndRotation(
                pose.Position + Vector3.up * height,
                Quaternion.LookRotation(pose.Forward)
            );
        }
    }
}
