using A305.Characters;
using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>행인이 대화하지 않을 때 출퇴근 경로를 따른다. 대화 중에는 Passerby가 이동을 멈춘다.</summary>
    [RequireComponent(typeof(Passerby))]
    public sealed class NpcTownWalker : MonoBehaviour
    {
        private const float ArrivalToleranceMeters = .0001f;

        // 일터에서 말을 걸러 다가오는 속도는 0인 직원도 있으므로 출퇴근 속도와 분리한다.
        private const float PassingLookAheadMeters = 1.25f;
        private const float PassingRetrySeconds = .5f;
        private const float CrossingLookAheadMeters = .6f;
        private const float SignalRadiusMeters = 8f;

        // 다가가던 횡단보도에서 이만큼 멀어지면(목적지가 바뀌었거나 밀려남) 정해 둔 횡단을 푼다.
        private const float CrossingReleaseMeters = 1.5f;

        // 발 디딜 곳에 이만큼 다가가면 닿은 것으로 본다.
        private const float StepInReachedMeters = .05f;

        private NpcWalkRoute route;

        // 횡단보도로 들어가는 길을 따라 다가왔는지. 인도를 걷다 끝자락을 밟은 걸음과 구분한다.
        private bool approachedCrossing;
        private NpcWalkRoute.CrossingApproach committedCrossing;

        private Passerby passing;
        private Vector3 passingForward;
        private bool passingOnRight;
        private float nextPassingRetryTime;
        private float passingClearance;
        private readonly NpcHurry hurry = new NpcHurry();
        private readonly NpcStuckRecovery stuckRecovery = new NpcStuckRecovery();
        private AnimatorOverrideController paceOverride;

        public Vector3 Destination { get; set; }
        public bool IsStaff { get; set; }
        public bool HasScheduleDeadline { get; set; }
        public NpcDayPhase? SchedulePhase { get; set; }
        public Vector3? ScheduleDoor { get; set; }
        public bool IsHurrying =>
            HasScheduleDeadline
            && hurry.IsHurrying
            && !GetComponent<Passerby>().IsGuidingTutorial
            && !GetComponent<Passerby>().IsWaitingForQuest;
        public bool IsRunning { get; private set; }
        public int StallRecoveryCount { get; private set; }
        public Vector3 LastRecoveryFrom { get; private set; }
        public Vector3 LastRecoveryTo { get; private set; }

        internal bool TryRecoverToDestination()
        {
            var body = GetComponent<Passerby>();
            if (
                !NpcWalkingSpace.TryFindRecovery(
                    body,
                    ScheduleDoor ?? Destination,
                    out Vector3 safe
                )
            )
                return false;
            LastRecoveryFrom = transform.position;
            body.RestoreWalkingPosition(safe);
            LastRecoveryTo = transform.position;
            passing = null;
            approachedCrossing = false;
            stuckRecovery.Reset();
            StallRecoveryCount++;
            return true;
        }

        private void LateUpdate()
        {
            var body = GetComponent<Passerby>();
            bool trying =
                body.Phase == PasserbyPhase.Waiting || body.Phase == PasserbyPhase.Resting;
            bool waiting =
                body.IsWaitingAtSignal
                || body.IsGuidingTutorial
                || body.IsWaitingForQuest
                || GameClock.Shared.IsHeld
                || GameClock.Shared.Current?.Paused == true;
            if (
                stuckRecovery.ShouldRecover(
                    transform.position,
                    ScheduleDoor ?? Destination,
                    trying,
                    waiting,
                    Time.deltaTime,
                    30f / (float)GameClock.GameMinutesPerRealSecond
                )
            )
                TryRecoverToDestination();
        }

        public bool DeclinesConversation =>
            IsStaff && SchedulePhase == NpcDayPhase.Working && IsHurrying;

        /// <summary>횡단보도 위에서는 대화나 새 제안으로 걸음을 멈추지 않는다.</summary>
        public bool IsCrossing => IsRoadSurface(transform.position, .1f);

        internal static int WalkingAreas => WithoutArea(NavMesh.AllAreas, "Road");
        internal static int StandingAreas => WithoutArea(WalkingAreas, "Crosswalk");

        private void Awake()
        {
            // NavMeshPath는 필드 초기화에서 만들 수 없다.
            route = new NpcWalkRoute();
        }

        private void OnDisable()
        {
            stuckRecovery.Reset();
            passing = null;
            approachedCrossing = false;
            hurry.Update(false, 0);
            IsRunning = false;
        }

        private void Start()
        {
            var controller = Resources.Load<RuntimeAnimatorController>("NpcCommuter");
            if (controller == null)
                return;
            foreach (Animator model in GetComponentsInChildren<Animator>(true))
            {
                if (model.avatar == null || !model.avatar.isHuman)
                    continue;
                model.runtimeAnimatorController = NpcLifeAnimator.KeepCharacterClips(
                    controller,
                    model.runtimeAnimatorController
                );
                paceOverride = model.runtimeAnimatorController as AnimatorOverrideController;
                model.Rebind();
                var animationDriver = GetComponent<CharacterAnimationDriver>();
                if (animationDriver != null)
                    animationDriver.BindAnimator(model);
                break;
            }
        }

        private void OnDestroy()
        {
            if (paceOverride != null)
                Destroy(paceOverride);
        }

        internal void RefreshHurry()
        {
            var body = GetComponent<Passerby>();
            bool eligible =
                HasScheduleDeadline
                && !body.IsGuidingTutorial
                && !body.IsWaitingForQuest
                && (body.Phase == PasserbyPhase.Waiting || body.Phase == PasserbyPhase.Resting);
            hurry.Update(
                eligible,
                Vector3.ProjectOnPlane(Destination - transform.position, Vector3.up).magnitude
            );
            if (!IsHurrying)
                IsRunning = false;
        }

        private static int WithoutArea(int areas, string name)
        {
            int area = NavMesh.GetAreaFromName(name);
            return area < 0 ? areas : areas & ~(1 << area);
        }

        internal void Walk()
        {
            var body = GetComponent<Passerby>();
            RefreshHurry();
            IsRunning = false;
            Vector3 direction = Destination - transform.position;
            direction.y = 0;
            if (direction.magnitude <= ArrivalToleranceMeters || Time.deltaTime <= 0f)
            {
                body.StandFacing(transform.rotation);
                return;
            }

            if (
                !route.TryPlan(transform.position, Destination)
                || !NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit start,
                    1f,
                    WalkingAreas
                )
            )
            {
                body.StandFacing(transform.rotation);
                return;
            }
            direction = Vector3.ProjectOnPlane(route.Next - transform.position, Vector3.up);
            if (
                direction.sqrMagnitude < .01f
                && route.AfterNext is Vector3 afterNext
                && body.HasWalkingRoom(afterNext - transform.position, .35f)
            )
                direction = Vector3.ProjectOnPlane(afterNext - transform.position, Vector3.up);

            // 인도를 따라 걷다 횡단보도 끝을 스치는 걸음은 신호를 기다리거나 횡단을 시작하지 않는다.
            // 길이 실제로 횡단보도로 들어갈 때만 그 횡단보도를 정해 신호를 보고, 발밑이 횡단보도가 된 뒤에만 횡단을 시작한다.
            bool onSidewalk = NpcWalkRoute.IsOnSidewalk(transform.position);
            if (onSidewalk)
                direction = FollowCommittedCrossing(route, direction);
            bool waitForSignal =
                approachedCrossing
                && onSidewalk
                && body.ShouldWaitAtCrossing(committedCrossing.StepIn, committedCrossing.Across);

            // 횡단 방향은 연석 모서리까지의 짧은 구간이 아니라 길이 횡단보도를 지나는 방향으로 정해, 기다린 신호와 같은 신호를 보고 건넌다.
            direction = body.PrepareWalkDirection(
                direction,
                approachedCrossing && !onSidewalk,
                approachedCrossing ? committedCrossing.Across : null
            );
            Vector3 travel = PassingDirection(
                body,
                direction,
                stayOnSidewalk: onSidewalk && !route.CrossesRoad
            );
            bool clear =
                IsHurrying
                && !IsCrossing
                && !IsRoadSurface(transform.position + travel.normalized * 1.25f, .3f)
                && (
                    !ScheduleDoor.HasValue
                    || Vector3
                        .ProjectOnPlane(ScheduleDoor.Value - transform.position, Vector3.up)
                        .magnitude > 2.5f
                )
                && Vector3.Dot(transform.forward, travel.normalized) > .8f
                && !NavMesh.Raycast(
                    start.position,
                    start.position + travel.normalized * 2.5f,
                    out _,
                    StandingAreas
                )
                && body.HasRunningRoom(travel);
            IsRunning = IsHurrying && clear;
            float speed = Mathf.Min(hurry.Speed(clear), direction.magnitude / Time.deltaTime);
            body.WalkToward(travel, speed, prepared: true, waitForSignal);
        }

        /// <summary>
        /// 인도 위에서 횡단보도로 들어가는 길을 따라 다가가면 그 횡단보도(들어설 지점·건너는 방향)를 정해 두고 그쪽으로 걷는다.
        /// 모서리에서는 몇 cm 차이로 경로가 다른 횡단보도로 바뀌는데, 정해 둔 횡단보도로 건너야 기다린 신호를 놓치지 않는다.
        /// </summary>
        private Vector3 FollowCommittedCrossing(NpcWalkRoute plan, Vector3 direction)
        {
            Vector3 toStepIn = Vector3.ProjectOnPlane(
                committedCrossing.StepIn - transform.position,
                Vector3.up
            );
            // 길이 더는 건너지 않거나, 정해 둔 곳에서 멀어졌거나, 발 디딜 곳에 닿았는데도 인도 위면 정해 둔 횡단을 푼다.
            if (
                approachedCrossing
                && (
                    !plan.CrossesRoad
                    || Vector3
                        .ProjectOnPlane(committedCrossing.Entry - transform.position, Vector3.up)
                        .magnitude > CrossingReleaseMeters
                    || toStepIn.sqrMagnitude <= StepInReachedMeters * StepInReachedMeters
                )
            )
                approachedCrossing = false;
            if (
                !approachedCrossing
                && plan.TryFindCrossingAhead(
                    CrossingLookAheadMeters,
                    out NpcWalkRoute.CrossingApproach approach
                )
            )
            {
                approachedCrossing = true;
                committedCrossing = approach;
                toStepIn = Vector3.ProjectOnPlane(approach.StepIn - transform.position, Vector3.up);
            }
            return approachedCrossing ? toStepIn : direction;
        }

        /// <param name="crossingHeading">
        /// 모퉁이까지 짧게 걷는 중일 때 실제로 건널 방향(경로의 다음 구간). 없으면 지금 걸음 방향으로 정한다.
        /// </param>
        internal static bool ShouldWaitForSignal(
            TrafficLights lights,
            Vector3 position,
            Vector3 direction,
            Vector3? crossingHeading = null
        )
        {
            if (lights == null || IsRoadSurface(position, .1f) || direction.sqrMagnitude < .0001f)
                return false;
            Vector3 ahead = position + direction.normalized * CrossingLookAheadMeters;
            if (!OnCrosswalk(ahead, .2f))
                return false;
            Vector3 across =
                crossingHeading.HasValue && crossingHeading.Value.sqrMagnitude > .0001f
                    ? crossingHeading.Value
                    : direction;
            return ShouldWaitAtCrossing(lights, position, ahead, across);
        }

        /// <summary>
        /// position에서 횡단보도 안 onCrosswalk를 지나 across 쪽으로 길을 건너려 할 때, 건너편 끝까지 걸을 시간만큼
        /// 차량 빨간불이 남지 않았으면 기다린다.
        /// </summary>
        internal static bool ShouldWaitAtCrossing(
            TrafficLights lights,
            Vector3 position,
            Vector3 onCrosswalk,
            Vector3 across
        )
        {
            if (lights == null)
                return false;
            bool crossesMainRoad = Mathf.Abs(across.x) >= Mathf.Abs(across.z);
            // 앞에서 기다리던 사람이 출발한 뒤 연석에 닿은 사람도, 자기가 건널 시간만 남았으면 따라 건넌다.
            return HasNearbySignal(lights, onCrosswalk)
                && !lights.CanCross(
                    crossesMainRoad,
                    SecondsToCrossFrom(position, onCrosswalk, across)
                );
        }

        /// <summary>
        /// 지금 자리에서 횡단보도 건너편 끝까지 걸어서 건너는 시간. 횡단보도 구역을 벗어나는 곳까지 재고,
        /// 재지 못하면 예전처럼 넉넉한 시간(<see cref="TrafficSignalCycle.RequiredCrossingSeconds"/>)을 쓴다.
        /// </summary>
        private static float SecondsToCrossFrom(Vector3 position, Vector3 ahead, Vector3 direction)
        {
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (
                crossing < 0
                || !NavMesh.SamplePosition(ahead, out NavMeshHit onCrosswalk, .3f, 1 << crossing)
                || !NavMesh.Raycast(
                    onCrosswalk.position,
                    onCrosswalk.position + forward * 100f,
                    out NavMeshHit farEdge,
                    1 << crossing
                )
            )
                return TrafficSignalCycle.RequiredCrossingSeconds;
            float meters = Vector3
                .ProjectOnPlane(farEdge.position - position, Vector3.up)
                .magnitude;
            return TrafficSignalCycle.SecondsToCross(meters);
        }

        internal static bool HasNearbySignal(TrafficLights lights, Vector3 position)
        {
            if (lights == null)
                return false;
            foreach (TrafficSignalHead head in lights.Heads)
            {
                if (head == null)
                    continue;
                Vector3 offset = head.transform.position - position;
                offset.y = 0;
                if (offset.sqrMagnitude <= SignalRadiusMeters * SignalRadiusMeters)
                    return true;
            }
            foreach (PedestrianSignalHead head in lights.PedestrianHeads)
            {
                if (head == null)
                    continue;
                Vector3 offset = head.CrossingPoint - position;
                offset.y = 0;
                if (offset.sqrMagnitude <= SignalRadiusMeters * SignalRadiusMeters)
                    return true;
            }
            return false;
        }

        internal static bool IsRoadSurface(Vector3 point, float distance)
        {
            int road = NavMesh.GetAreaFromName("Road");
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            int areas = (road < 0 ? 0 : 1 << road) | (crossing < 0 ? 0 : 1 << crossing);
            return areas != 0 && NavMesh.SamplePosition(point, out _, distance, areas);
        }

        private static bool OnCrosswalk(Vector3 point, float distance)
        {
            int crossing = NavMesh.GetAreaFromName("Crosswalk");
            return crossing >= 0 && NavMesh.SamplePosition(point, out _, distance, 1 << crossing);
        }

        private Vector3 PassingDirection(
            Passerby body,
            Vector3 destinationOffset,
            bool stayOnSidewalk
        )
        {
            Vector3 forward = destinationOffset.normalized;
            if (passing != null)
            {
                Vector3 toOther = passing.transform.position - transform.position;
                // 상대를 지난 뒤에만 원래 경로로 돌아간다. 매 프레임 좌우를 다시 고르면 몸이 흔들린다.
                if (
                    !passing.isActiveAndEnabled
                    || toOther.magnitude > PassingLookAheadMeters + passingClearance
                    || Vector3.Dot(toOther, forward) <= 0
                    || Vector3.Dot(toOther, passingForward) < -passingClearance * .5f
                    || Vector3.Dot(forward, passingForward) < 0
                    || DestinationIsOccupied(passing, passingClearance)
                )
                    passing = null;
            }

            if (passing == null)
            {
                var other = NpcWalkingSpace.FindBlocker(
                    body,
                    forward,
                    Mathf.Min(PassingLookAheadMeters, destinationOffset.magnitude),
                    out _,
                    out float clearance
                );
                // 목적지 자체가 차 있으면 그 둘레를 계속 맴돌지 않고 간격을 두고 기다린다.
                if (other == null || DestinationIsOccupied(other, clearance))
                    return destinationOffset;
                passingForward = forward;
                passingOnRight = true;
                passingClearance = clearance;
                if (!TryPassingDirection(other, stayOnSidewalk, out _))
                {
                    passingOnRight = false;
                    if (!TryPassingDirection(other, stayOnSidewalk, out _))
                        return destinationOffset;
                }
                passing = other;
            }

            if (TryPassingDirection(passing, stayOnSidewalk, out Vector3 direction))
                return direction;

            // 앞을 막은 NPC도 누군가를 피하는 중이면 여러 사람이 서로 기다릴 수 있다.
            // 이때만 시간을 두고 다른 길을 확인해, 한 사람 뒤에서 좌우로 떨리는 것을 막는다.
            if (
                Time.time >= nextPassingRetryTime
                && passing.TryGetComponent(out NpcTownWalker otherWalker)
                && otherWalker.passing != null
            )
            {
                nextPassingRetryTime = Time.time + PassingRetrySeconds;
                passingOnRight = !passingOnRight;
                if (TryPassingDirection(passing, stayOnSidewalk, out direction))
                    return direction;
                passing = null;
                return destinationOffset;
            }
            return Vector3.zero;
        }

        private bool DestinationIsOccupied(Passerby other, float clearance)
        {
            // 서로 자리를 바꾸며 걷는 상대를 고정된 목적지 점유자로 보면 가까운 거리에서 둘 다 멈춘다.
            bool leaving =
                other.TryGetComponent(out NpcTownWalker walker)
                && walker.isActiveAndEnabled
                && (other.Phase == PasserbyPhase.Waiting || other.Phase == PasserbyPhase.Resting)
                && Vector3
                    .ProjectOnPlane(walker.Destination - other.transform.position, Vector3.up)
                    .sqrMagnitude > .01f;
            return !leaving
                && Vector3
                    .ProjectOnPlane(Destination - other.transform.position, Vector3.up)
                    .sqrMagnitude
                    < clearance * clearance;
        }

        private bool TryPassingDirection(Passerby other, bool stayOnSidewalk, out Vector3 direction)
        {
            Vector3 offset = Vector3.ProjectOnPlane(
                other.transform.position - transform.position,
                Vector3.up
            );
            Vector3 towardOther = offset.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, towardOther);
            if (!passingOnRight)
                side = -side;
            // 상대 뒤의 지점을 바로 향하면 여유 공간을 가로질러 다시 막힌다. 바깥 원의 접선으로 걷는다.
            float sine = Mathf.Clamp01(
                (passingClearance + .12f) / Mathf.Max(offset.magnitude, .001f)
            );
            direction = towardOther * Mathf.Sqrt(1f - sine * sine) + side * sine;
            // 좁은 보도에서 멀리 떨어진 접선 끝점까지 요구하면 양옆 모두 막혔다고 판단한다.
            // 실제 한 걸음으로 갈 수 있는 구간부터 확인해 서로 반대쪽으로 조금씩 비켜 간다.
            Vector3 waypoint = transform.position + direction * .35f;
            // 좁은 인도 가장자리는 가까운 보행 지점으로 보정하되 차도·횡단보도로는 돌아가지 않는다.
            if (
                stayOnSidewalk
                && IsRoadSurface(waypoint, .1f)
                && !NpcWalkRoute.IsOnSidewalk(waypoint)
            )
                return false;
            int passingAreas = stayOnSidewalk ? StandingAreas : WalkingAreas;
            if (
                !NavMesh.SamplePosition(transform.position, out NavMeshHit start, .3f, passingAreas)
                || !NavMesh.SamplePosition(waypoint, out NavMeshHit end, .2f, passingAreas)
                || NavMesh.Raycast(start.position, end.position, out _, passingAreas)
                || !GetComponent<Passerby>().HasWalkingRoom(direction, .35f)
                || NpcWalkingSpace.LimitStep(GetComponent<Passerby>(), direction.normalized, .1f)
                    < .005f
            )
                return false;
            return direction.sqrMagnitude > .001f;
        }
    }
}
