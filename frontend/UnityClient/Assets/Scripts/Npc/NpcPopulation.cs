using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.Npc
{
    /// <summary>근무지의 직원과 타운의 출퇴근 모습을 같은 게임 시각으로 관리한다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class NpcPopulation : MonoBehaviour
    {
        [SerializeField]
        private TextAsset lifeData;

        [SerializeField]
        private Resident[] residents = Array.Empty<Resident>();

        [SerializeField]
        private Vector3 dormDoor;

        [SerializeField]
        private Vector3 park;

        [SerializeField]
        private Transform townCharacters;

        private DialogueController dialogue;
        private PlayerMover player;
        private PasserbyOfferView offerView;
        private GameClock observedClock;
        private double lastMinutes;
        private NpcOutingCatalog outings;

        [Serializable]
        public sealed class Resident
        {
            public GameObject Worker;
            public GameObject TownPrefab;

            [NonSerialized]
            internal GameObject TownBody;
            public Vector3 WorkplaceDoor;
            public string InteriorScene;

            [NonSerialized]
            internal NpcLifeEntry Life;

            [NonSerialized]
            internal NpcDayPlan Plan;

            [NonSerialized]
            internal NpcDayPhase? LastPhase;

            [NonSerialized]
            internal Vector3[] Route;

            [NonSerialized]
            internal bool Warned;

            [NonSerialized]
            internal bool EnteringDoor;

            [NonSerialized]
            internal Vector3 EntryDoor;

            [NonSerialized]
            internal NpcOutingVenue LeisureVenue;
        }

        /// <summary>같은 코드의 거리 모습을 고르지 않고 근무지의 직원을 찾는다.</summary>
        public NpcIdentity FindWorker(string npcCode)
        {
            foreach (Resident resident in residents)
            {
                if (resident.Worker == null)
                    continue;
                var identity = resident.Worker.GetComponent<NpcIdentity>();
                if (identity != null && identity.Code == npcCode)
                    return identity;
            }
            return null;
        }

        private void Start()
        {
            outings = Resources.Load<NpcOutingCatalog>("NpcOutings");
            NpcOutings.Shared.Synchronize(GameClock.Shared);
            NpcFacilityVisitors.Ensure(outings);
            dialogue = FindFirstObjectByType<DialogueController>();
            player = FindFirstObjectByType<PlayerMover>();
            offerView = FindFirstObjectByType<PasserbyOfferView>();
            NpcLifeData data = NpcLifeData.Parse(lifeData.text);
            foreach (Resident resident in residents)
            {
                if (resident.TownPrefab != null)
                {
                    resident.TownBody = Instantiate(resident.TownPrefab, townCharacters);
                    resident
                        .TownBody.GetComponent<Passerby>()
                        .ConnectToTown(player, dialogue, offerView);
                }
                GameObject body = resident.Worker != null ? resident.Worker : resident.TownBody;
                resident.Life = data.FindNpc(body.GetComponent<NpcIdentity>().Code);
            }
            Update();
        }

        private void Update()
        {
            // 장면을 내릴 때 대화 처리가 먼저 사라질 수 있다. 연결이 끊긴 출퇴근 NPC를 그 사이에 새로 깨우지 않는다.
            if (dialogue == null)
                return;
            NpcOutings.Shared.Synchronize(GameClock.Shared);
            // 초기 불러오기·시각을 크게 건너뛴 경우에는 그 시각의 위치를 복원한다.
            // 연속 플레이에서는 시각표가 바뀌어도 실제 문에 도착하기 전에 모습을 끄지 않는다.
            double minutes = GameClock.Shared.Minutes;
            bool continuous =
                observedClock == GameClock.Shared && Math.Abs(minutes - lastMinutes) < 5;
            observedClock = GameClock.Shared;
            lastMinutes = minutes;
            foreach (Resident resident in residents)
            {
                if (resident.Life == null)
                    continue;
                var workerBody =
                    resident.Worker != null ? resident.Worker.GetComponent<Passerby>() : null;
                bool needed =
                    resident.Life.NpcId == NpcDuty.MissionNpcCode
                    || (
                        resident.Worker != null
                        && NpcReviewPresence.IsRequested(resident.Life.NpcId)
                    )
                    || (
                        workerBody != null
                        && (workerBody.IsGuidingTutorial || workerBody.IsWaitingForQuest)
                    );
                NpcDayPlan plan = NpcDayPlan.At(resident.Life, GameClock.Shared.Minutes, needed);
                resident.Plan = plan;
                if (needed)
                {
                    NpcFacilityVisitors.Recall(resident.Life.NpcId);
                    resident.LeisureVenue = null;
                }
                bool entering = false;
                if (resident.TownBody != null && !IsTalking(resident.TownBody))
                {
                    if (continuous && resident.TownBody.activeSelf && !plan.IsInTown)
                    {
                        resident.EntryDoor =
                            plan.Phase == NpcDayPhase.Working ? resident.WorkplaceDoor : dormDoor;
                        entering =
                            Vector3
                                .ProjectOnPlane(
                                    resident.EntryDoor - resident.TownBody.transform.position,
                                    Vector3.up
                                )
                                .magnitude > .4f;
                    }
                    resident.EnteringDoor = entering;
                }
                if (resident.Worker != null && !IsTalking(resident.Worker))
                {
                    bool roomLoaded =
                        string.IsNullOrEmpty(resident.InteriorScene)
                        || SceneManager.GetSceneByName(resident.InteriorScene).isLoaded;
                    // 순찰차를 몰고 나간 경찰관은 돌아와 주차할 때까지 창구에 두지 않는다(PoliceCar).
                    resident.Worker.SetActive(
                        plan.Phase == NpcDayPhase.Working
                            && !entering
                            && roomLoaded
                            && !PoliceCar.IsOutOnPatrol(resident.Life.NpcId)
                    );
                }
                if (resident.TownBody == null || IsTalking(resident.TownBody))
                    continue;
                if (
                    (!plan.IsInTown && !entering) || PoliceDispatchOrder.IsAway(resident.Life.NpcId)
                )
                {
                    resident.TownBody.SetActive(false);
                    resident.LastPhase = null;
                }
            }

            // 같은 프레임에 떠나는 사람을 먼저 끈 뒤 새 출근자의 자리를 찾는다.
            foreach (Resident resident in residents)
            {
                if (
                    resident.Life == null
                    || resident.TownBody == null
                    || IsTalking(resident.TownBody)
                    || (!resident.Plan.IsInTown && !resident.EnteringDoor)
                    || PoliceDispatchOrder.IsAway(resident.Life.NpcId)
                )
                    continue;
                if (player == null || offerView == null)
                    continue;
                NpcDayPlan plan = resident.Plan;
                if (UpdateOuting(resident, plan, minutes))
                    continue;
                if (resident.EnteringDoor)
                {
                    var enteringWalker = resident.TownBody.GetComponent<NpcTownWalker>();
                    enteringWalker.Destination = resident.EntryDoor;
                    enteringWalker.HasScheduleDeadline = true;
                    enteringWalker.SchedulePhase = plan.Phase;
                    enteringWalker.IsStaff = NpcDuty.IsStaff(resident.Life.Role);
                    enteringWalker.ScheduleDoor = resident.EntryDoor;
                    continue;
                }
                bool appeared = !resident.TownBody.activeSelf;
                if (resident.LastPhase != plan.Phase)
                {
                    var endpoints = Endpoints(resident, plan);
                    resident.Route = RouteBetween(endpoints.From, endpoints.To);
                    resident.LastPhase = plan.Phase;
                }
                if (resident.Route == null)
                {
                    resident.TownBody.SetActive(false);
                    if (!resident.Warned)
                    {
                        Debug.LogWarning(
                            "NPC 출퇴근 경로를 찾지 못했습니다: " + resident.Life.NpcId,
                            this
                        );
                        resident.Warned = true;
                    }
                    // 다른 실내가 내려간 다음 프레임에는 타운 NavMesh만으로 다시 찾는다.
                    resident.LastPhase = null;
                    continue;
                }
                float progress = (float)plan.Progress;
                if (plan.Phase == NpcDayPhase.Leisure && resident.LeisureVenue != null)
                    progress = resident.LeisureVenue.IsOutdoor
                        ? Mathf.PingPong(progress * 6, 1)
                        : 1;
                Vector3 target = PositionOnRoute(resident.Route, progress);
                if (appeared)
                {
                    if (
                        !NpcWalkingSpace.TryFindArrival(
                            resident.TownBody.GetComponent<Passerby>(),
                            target,
                            out Vector3 arrival
                        )
                    )
                        continue;
                    resident.TownBody.transform.position = arrival;
                }
                var walker = resident.TownBody.GetComponent<NpcTownWalker>();
                walker.Destination = target;
                walker.SchedulePhase = plan.Phase;
                walker.HasScheduleDeadline =
                    plan.Phase == NpcDayPhase.GoingToWork || plan.Phase == NpcDayPhase.GoingHome;
                walker.ScheduleDoor = null;
                if (plan.Phase == NpcDayPhase.GoingToWork)
                    walker.ScheduleDoor = resident.WorkplaceDoor;
                else if (plan.Phase == NpcDayPhase.GoingHome)
                    walker.ScheduleDoor = dormDoor;
                walker.IsStaff = NpcDuty.IsStaff(resident.Life.Role);
                resident.TownBody.SetActive(true);
            }
        }

        private (Vector3 From, Vector3 To) Endpoints(Resident resident, NpcDayPlan plan)
        {
            // 같은 공원을 쓰더라도 서로 겹쳐 서지 않도록 NPC 코드에서 일정한 자리를 고른다.
            int slot = 0;
            foreach (char letter in resident.Life.NpcId)
                slot = (slot * 31 + letter) % 25;
            Vector3 leisure = park + new Vector3(slot % 5 - 2, 0, slot / 5 - 2);
            if (resident.LeisureVenue != null)
                leisure = resident.LeisureVenue.Entrance;
            if (plan.IsShortBreak)
                leisure = resident.WorkplaceDoor + new Vector3(0, 0, 2);
            switch (plan.Phase)
            {
                case NpcDayPhase.GoingToWork:
                    return (plan.IsShortBreak ? leisure : dormDoor, resident.WorkplaceDoor);
                case NpcDayPhase.LeavingWork:
                    return (resident.WorkplaceDoor, leisure);
                case NpcDayPhase.GoingHome:
                    return (leisure, dormDoor);
                default:
                    if (resident.LeisureVenue != null)
                        return (
                            leisure,
                            resident.LeisureVenue.IsOutdoor ? resident.LeisureVenue.WalkTo : leisure
                        );
                    // 공원 안에서도 한 자리에 서 있지 않고 짧은 산책을 한다.
                    return (leisure, leisure + Vector3.forward * 2);
            }
        }

        // 실제 문에 도착하기 전에는 시설 안으로 옮기지 않는다. 정원은 이곳으로 걷는 사람까지 포함한다.
        private bool UpdateOuting(Resident resident, NpcDayPlan plan, double now)
        {
            if (outings == null)
                return false;
            string code = resident.Life.NpcId;
            NpcOuting trip = NpcOutings.Shared.Find(code);
            if (NpcOutings.CanGoOut(plan) && code != NpcDuty.MissionNpcCode)
            {
                trip = NpcOutings.Shared.Reserve(
                    code,
                    resident.Life.Workplace,
                    now,
                    NpcOutings.UntilHome(plan, now),
                    outings.Venues,
                    venue =>
                        OpeningHours.IsOpenNow(venue.Code)
                        && RouteBetween(resident.WorkplaceDoor, venue.Entrance) != null
                        && (!venue.IsOutdoor || RouteBetween(venue.Entrance, venue.WalkTo) != null)
                );
                if (resident.LeisureVenue != trip?.Venue)
                    resident.LastPhase = null;
                resident.LeisureVenue = trip?.Venue;
            }
            if (trip == null)
                return false;
            bool visiting =
                NpcOutings.CanGoOut(plan)
                && now < trip.Until
                && OpeningHours.IsOpenNow(trip.Venue.Code)
                && !trip.ReadyToLeave;
            if (!trip.Venue.IsOutdoor && trip.Arrived)
            {
                if (
                    visiting
                    || (
                        !trip.ReadyToLeave && SceneManager.GetSceneByName(trip.Venue.Scene).isLoaded
                    )
                )
                {
                    resident.TownBody.SetActive(false);
                    return true;
                }
                var capsule = resident.TownBody.GetComponent<CharacterController>();
                capsule.enabled = false;
                resident.TownBody.transform.position = trip.Venue.Entrance;
                capsule.enabled = true;
                resident.TownBody.SetActive(true);
                trip.Arrived = false;
                NpcOutings.Shared.Release(code);
            }
            if (
                visiting
                && !trip.Venue.IsOutdoor
                && resident.TownBody.activeSelf
                && Vector3
                    .ProjectOnPlane(
                        resident.TownBody.transform.position - trip.Venue.Entrance,
                        Vector3.up
                    )
                    .magnitude <= .4f
            )
            {
                trip.Arrived = true;
                resident.TownBody.SetActive(false);
                return true;
            }
            if (!visiting && !trip.Arrived)
                NpcOutings.Shared.Release(code);
            return false;
        }

        private static bool IsTalking(GameObject npc)
        {
            return npc.activeInHierarchy
                && npc.TryGetComponent(out Passerby body)
                && (body.Phase == PasserbyPhase.Talking || body.Phase == PasserbyPhase.Offering);
        }

        private static Vector3[] RouteBetween(Vector3 from, Vector3 to)
        {
            int walkableAreas = NpcTownWalker.WalkingAreas;
            if (
                !NavMesh.SamplePosition(from, out NavMeshHit start, 2, walkableAreas)
                || !NavMesh.SamplePosition(to, out NavMeshHit end, 2, walkableAreas)
            )
                return null;
            var path = new NavMeshPath();
            if (
                !NavMesh.CalculatePath(start.position, end.position, walkableAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
            )
                return null;
            return path.corners.Length > 0 ? path.corners : new[] { start.position };
        }

        internal static Vector3 PositionOnRoute(IReadOnlyList<Vector3> route, float progress)
        {
            float total = 0;
            for (int i = 1; i < route.Count; i++)
                total += Vector3.Distance(route[i - 1], route[i]);
            float distance = total * Mathf.Clamp01(progress);
            Vector3 point = PointAtDistance(route, distance);
            int road = NavMesh.GetAreaFromName("Road");
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            int unsafeAreas = (road < 0 ? 0 : 1 << road) | (crossing < 0 ? 0 : 1 << crossing);
            // 시계가 멈춰도 횡단 중인 NPC는 다음 인도까지 계속 움직인다.
            while (
                unsafeAreas != 0
                && NavMesh.SamplePosition(point, out _, .1f, unsafeAreas)
                && distance < total
            )
            {
                distance = Mathf.Min(total, distance + .2f);
                point = PointAtDistance(route, distance);
            }
            if (
                unsafeAreas != 0
                && NavMesh.SamplePosition(point, out _, .1f, unsafeAreas)
                && NavMesh.SamplePosition(
                    point,
                    out NavMeshHit safe,
                    3f,
                    NavMesh.AllAreas & ~unsafeAreas
                )
            )
                point = safe.position;
            return point;
        }

        private static Vector3 PointAtDistance(IReadOnlyList<Vector3> route, float distance)
        {
            float remaining = distance;
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                if (remaining <= length)
                    return Vector3.Lerp(
                        route[i - 1],
                        route[i],
                        length > 0 ? remaining / length : 0
                    );
                remaining -= length;
            }
            return route[route.Count - 1];
        }
    }
}
