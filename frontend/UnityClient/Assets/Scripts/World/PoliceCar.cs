using System;
using System.Collections.Generic;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 경찰서 옆 주차 칸에 서 있다가 신고를 받으면 출동하는 순찰차. 주차 칸과 본길 사이는 진입로(<see cref="DrivePath"/>)로,
    /// 본길은 다른 차처럼 <see cref="LoopingCar"/>로 신호를 지키며 달린다. 출동 흐름은 <see cref="PoliceDispatch"/>가 정하고,
    /// 경광등은 출동해서 돌아오기 전까지 빨강·파랑을 번갈아 켠다. 경찰서 창구가 남긴 신고(<see cref="PoliceDispatchOrder"/>)는 마을이 열릴 때 가져가 출동한다.
    /// 진입로 조절점은 Builder가 마을 배치에 맞춰 넣는다. 나가는 길은 본길 북행 차로 시작점에서, 들어오는 길은 남행 차로 끝에서 이어진다.
    /// 신고를 받은 경찰관이 직접 운전한다. 출동하는 동안 창구의 그 경찰관을 끄고 운전석에 앉힌 같은 경찰관 모습을 켰다가,
    /// 주차 칸에 다시 서면 되돌린다. 운전석 모습은 Builder가 경찰관마다 미리 앉혀 둔다.
    /// </summary>
    [RequireComponent(typeof(LoopingCar))]
    public sealed class PoliceCar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "주차 칸에서 본길 북행 차로 시작점까지. 시작점, 손잡이 둘, 끝점 순서의 3차 곡선을 이어 적는다(4, 7, 10…개)."
        )]
        private List<Vector3> exitCurve = new List<Vector3>();

        [SerializeField]
        [Tooltip("본길 남행 차로 끝에서 주차 칸까지. 적는 방법은 나가는 길과 같다.")]
        private List<Vector3> entryCurve = new List<Vector3>();

        [SerializeField]
        [Tooltip("진입로에서 달리는 속도(m/s)")]
        private float bayPathSpeed = 3f;

        [SerializeField]
        [Tooltip("출동할 수 있는 장소. 택시 목적지와 같은 정차 지점을 쓴다.")]
        private List<TaxiDestination> destinations = new List<TaxiDestination>();

        [SerializeField]
        [Tooltip("경찰관마다 운전석에 앉힌 모습. 출동한 경찰관의 모습만 켠다.")]
        private List<PatrolDriver> drivers = new List<PatrolDriver>();

        [SerializeField]
        private GameObject redLights;

        [SerializeField]
        private GameObject blueLights;

        [SerializeField]
        [Tooltip("경광등 한쪽이 켜져 있는 시간(초)")]
        private float flashSeconds = .25f;

        [SerializeField]
        private PlayerMover player;

        private readonly PoliceDispatch dispatch = new PoliceDispatch();
        private LoopingCar car;
        private DrivePath exitPath;
        private DrivePath entryPath;
        private float pathDistance;
        private float height;

        public PolicePhase Phase => dispatch.Phase;

        /// <summary>출동한 장소 코드. 순찰 중이거나 서 있으면 null</summary>
        public string SceneCode => dispatch.SceneCode;

        public bool LightsOn => dispatch.LightsOn;

        /// <summary>이 경찰관이 지금 순찰차를 몰고 나가 창구를 비웠는지</summary>
        public static bool IsOutOnPatrol(string npcCode) => PoliceDispatchOrder.IsAway(npcCode);

        /// <summary>장소를 정할 수 있는 장소 코드면 그 장소의 화면 이름, 아니면 null</summary>
        public string DisplayNameOf(string sceneCode)
        {
            TaxiDestination destination = Find(sceneCode);
            return destination != null ? destination.DisplayName : null;
        }

        /// <param name="sceneCode">신고한 장소 코드. null이거나 출동할 수 없는 장소면 본길을 순찰한다.</param>
        /// <param name="officerCode">신고를 받아 직접 운전할 경찰관의 NPC 코드. 운전석 모습이 없으면 경찰관 없이 나간다.</param>
        /// <returns>서 있다가 출동했으면 true</returns>
        public bool Dispatch(string sceneCode, string officerCode = null)
        {
            string driverCode = SeatOf(officerCode) != null ? officerCode : null;
            if (!dispatch.TryDispatch(Find(sceneCode)?.Code, driverCode))
                return false;
            pathDistance = 0;
            ShowDriver();
            return true;
        }

        private void Awake()
        {
            car = GetComponent<LoopingCar>();
            height = transform.position.y;
            if (exitCurve.Count < 4 || entryCurve.Count < 4)
            {
                Debug.LogError($"{name}의 PoliceCar 진입로가 비어 있습니다.", this);
                enabled = false;
                return;
            }
            exitPath = new DrivePath(Curves(exitCurve));
            entryPath = new DrivePath(Curves(entryCurve));
            ShowDriver();
            // 주차 칸·진입로에서는 본길을 따라 움직이지 않지만, 본길 차들은 이 차를 피한다.
            car.IsHeld = true;
            Place(entryPath.PoseAt(entryPath.Length));
        }

        // 경찰서는 마을과 다른 장면이라, 플레이어가 경찰서에서 신고하고 나와 마을이 열리면 남겨 둔 신고로 출동한다.
        private void Start() => StartNextReport();

        private void StartNextReport()
        {
            if (Phase == PolicePhase.Parked && PoliceDispatchOrder.TryBegin(out var report))
                Dispatch(report.PlaceCode, report.OfficerCode);
        }

        private void Update()
        {
            StartNextReport();
            PolicePhase before = dispatch.Phase;
            dispatch.Tick(ReachedLegEnd(), Time.deltaTime);
            if (dispatch.Phase != before)
            {
                BeginLeg();
                if (dispatch.Phase == PolicePhase.Parked)
                    BackToTheDesk();
            }
            // 진입로는 트럭 옆·남쪽 유턴의 한 대씩 구간에 이어진다. 다른 차가 지나는 중이면 주차 칸에서 기다렸다 나간다.
            if (
                dispatch.Phase == PolicePhase.LeavingBay
                && (pathDistance > 0 || !car.IsSingleLaneTaken)
            )
                Drive(exitPath);
            else if (dispatch.Phase == PolicePhase.EnteringBay)
                Drive(entryPath);
            car.HoldsSingleLane =
                (dispatch.Phase == PolicePhase.LeavingBay && pathDistance > 0)
                || dispatch.Phase == PolicePhase.EnteringBay;
            ShowLights();
        }

        private bool ReachedLegEnd()
        {
            switch (dispatch.Phase)
            {
                case PolicePhase.LeavingBay:
                    return pathDistance >= exitPath.Length;
                case PolicePhase.EnteringBay:
                    return pathDistance >= entryPath.Length;
                case PolicePhase.Responding:
                case PolicePhase.Patrolling:
                case PolicePhase.Returning:
                    return car.HasPulledOver;
                default:
                    return false;
            }
        }

        private void BeginLeg()
        {
            switch (dispatch.Phase)
            {
                case PolicePhase.Responding:
                    JoinRoute();
                    car.PullOverAt(StopDistance(Find(dispatch.SceneCode).Stop));
                    break;
                case PolicePhase.Patrolling:
                    JoinRoute();
                    car.PullOverAt(ReturnDistance);
                    break;
                case PolicePhase.Returning:
                    car.PullOverAt(ReturnDistance);
                    break;
                case PolicePhase.EnteringBay:
                    car.IsHeld = true;
                    pathDistance = 0;
                    break;
            }
        }

        // 나가는 길 끝은 북행 차로 시작점(길 위 거리 0)이다.
        private void JoinRoute()
        {
            car.StopAt(0);
            car.IsHeld = false;
        }

        // 남행 차로를 다 내려온 곳. 들어오는 길이 여기서 시작한다.
        private float ReturnDistance => car.Route.SouthboundDistance(SouthEndZ);

        private float SouthEndZ => entryCurve[0].z;

        private float StopDistance(Vector3 curb) =>
            curb.x > 0
                ? car.Route.NorthboundDistance(curb.z)
                : car.Route.SouthboundDistance(curb.z);

        // 진입로에서는 사람만 피한다. 본길 차는 이 차를 피하므로, 서로 기다리다 둘 다 멈추지 않도록 차는 피하지 않는다.
        private void Drive(DrivePath path)
        {
            DrivePath.Pose next = path.PoseAt(pathDistance + bayPathSpeed * Time.deltaTime);
            if (
                player != null
                && CarLoopRoute.ShouldYield(next.Position, next.Forward, player.transform.position)
            )
                return;
            pathDistance += bayPathSpeed * Time.deltaTime;
            Place(next);
        }

        private void Place(DrivePath.Pose pose)
        {
            transform.SetPositionAndRotation(
                pose.Position + Vector3.up * height,
                Quaternion.LookRotation(pose.Forward)
            );
        }

        private void ShowLights()
        {
            bool red = false;
            bool blue = false;
            if (dispatch.LightsOn)
            {
                red = Mathf.FloorToInt(Time.time / flashSeconds) % 2 == 0;
                blue = !red;
            }
            if (redLights != null && redLights.activeSelf != red)
                redLights.SetActive(red);
            if (blueLights != null && blueLights.activeSelf != blue)
                blueLights.SetActive(blue);
        }

        private GameObject SeatOf(string officerCode) =>
            officerCode == null
                ? null
                : drivers.Find(driver => driver.OfficerCode == officerCode)?.Seat;

        private void ShowDriver()
        {
            foreach (PatrolDriver driver in drivers)
                if (driver.Seat != null)
                    driver.Seat.SetActive(driver.OfficerCode == dispatch.DriverCode);
        }

        private void BackToTheDesk()
        {
            if (PoliceDispatchOrder.Current != null)
                PoliceDispatchOrder.Complete(PoliceDispatchOrder.Current.TurnId);
            ShowDriver();
        }

        private TaxiDestination Find(string sceneCode) =>
            sceneCode == null
                ? null
                : destinations.Find(destination => destination.Code == sceneCode);

        private static DrivePath.Curve[] Curves(List<Vector3> points)
        {
            var curves = new DrivePath.Curve[(points.Count - 1) / 3];
            for (int index = 0; index < curves.Length; index++)
                curves[index] = new DrivePath.Curve(
                    points[index * 3],
                    points[index * 3 + 1],
                    points[index * 3 + 2],
                    points[index * 3 + 3]
                );
            return curves;
        }
    }

    /// <summary>순찰차 운전석에 앉힌 경찰관 한 명의 모습</summary>
    [Serializable]
    public sealed class PatrolDriver
    {
        [SerializeField]
        private string officerCode;

        [SerializeField]
        private GameObject seat;

        public PatrolDriver(string officerCode, GameObject seat)
        {
            this.officerCode = officerCode;
            this.seat = seat;
        }

        public string OfficerCode => officerCode;

        public GameObject Seat => seat;
    }
}
