using System.Collections.Generic;
using System.Linq;
using A305.Characters;
using A305.ObjectHover;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>
    /// 다가와 말을 걸지 않는 동안 NPC가 게임 시각의 일과와 욕구에 따라 자기 공간에서 지내게 한다(<see cref="NpcLife"/>).
    /// 고른 행동의 사물까지 NavMesh 경로(<see cref="NpcWalkArea"/>)를 따라 걸어가 이용하고, 의자·벤치·소파면 빈 자리에 앉는다.
    /// 급한 일이 없어도 한곳에 서 있지 않고 자기 공간의 다른 사물로 옮겨 다니며 둘러본다.
    /// 카운터를 맡은 직원은 손님(플레이어)이 카운터 가까이 오면 하던 일을 그만두고 카운터 뒤 자리로 돌아가 응대한다.
    /// 근무(WORK) 중인 직원만 응대하고(<see cref="NpcDuty"/>), 같은 카운터를 맡은 직원이 여럿 근무 중이면 하는 일이 없는 가까운 한 명이 응대한다.
    /// 장면을 다시 불러오면 그 시각의 시작값에서 다시 지낸다. 다가오기·대화는 <see cref="Passerby"/>가 맡고, 그동안 하던 일을 그만둔다.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(Passerby))]
    public class NpcRoutine : MonoBehaviour
    {
        // 사물 둘레에서 이만큼 떨어져 선다(몸 반지름 0.3m와 여유).
        private const float StandOffMeters = .45f;

        // 도착 오차와 모델 머리 폭까지 포함해 작업·응대 자리 사이를 띄운다.
        private const float StandingSpaceMeters = 1.25f;

        // 선 자리 후보를 NavMesh 위로 옮길 때 찾는 거리
        private const float SpotSnapMeters = .6f;
        private const int SpotDirections = 12;

        // 사물 콜라이더 둘레에서 이만큼 안에 있는 앉을 자리를 그 사물의 자리로 본다.
        private const float SeatMatchMeters = .3f;

        // 앉을 자리 앞 모서리에서 이만큼 앞에 섰다가 앉는다.
        private const float SeatApproachMeters = .35f;

        private const float ArrivedMeters = .15f;
        private const float CornerReachedMeters = .2f;

        // 이 시간 동안 남은 거리가 줄지 않으면 막힌 것으로 보고, 그 사물은 잠시 고르지 않는다(플레이어가 길을 막고 있었을 수 있다).
        private const float StuckSeconds = 2.5f;
        private const float StuckProgressMeters = .2f;
        private const float RetryUnreachableSeconds = 20f;

        // 직원이 손님을 맞는 사물(카페 카운터, 호텔 리셉션 데스크). 처음 놓인 자리에서 이 거리 안에 있으면 그 직원의 카운터다.
        private static readonly string[] ServiceCounterKeys = { "counter", "reception_desk" };
        private const float CounterNearPostMeters = 1.5f;

        // 카운터 뒤 자리에서 이 거리 안이면 자리에 선 것으로 본다.
        private const float AtPostMeters = .3f;

        // 급한 일이 없을 때 둘러볼 사물: 처음 놓인 자리에서 이 반경 안이고 지금 있는 곳에서 이보다 먼 사물
        private const float LookAroundRadiusMeters = 8f;
        private const float LookAroundStepMeters = 1.5f;
        private const int LookAroundTries = 4;
        private const float LookAroundMinSeconds = 2f;
        private const float LookAroundMaxSeconds = 5f;

        [SerializeField]
        [Tooltip("ai/tools/npc_life_export.py가 내보낸 Data/Npc/NpcLife.json")]
        private TextAsset lifeData;

        [SerializeField]
        [Min(.1f)]
        [Tooltip(
            "생활하며 걷는 속도. 다가와 말을 거는 속도(PasserbySettings)와 따로 두어, 다가오지 않는 카페 직원도 사물까지는 걷는다."
        )]
        private float walkSpeedMetersPerSecond = 1.1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip(
            "손님(플레이어)이 카운터에서 이 거리 안에 오면 카운터를 맡은 직원은 하던 일을 그만두고 카운터 뒤 자리로 돌아가 응대한다."
        )]
        private float serveRadiusMeters = 2f;

        [SerializeField]
        [Tooltip(
            "생활 동작(일하기·물건 집기·말하기·앉기)이 있는 NPC 전용 Animator Controller. 생활이 켜지면 Humanoid 모델에 바꿔 끼운다."
        )]
        private RuntimeAnimatorController lifeAnimator;

        [SerializeField]
        [Tooltip(
            "켜면 생활하며 고르는 사물·자리를 아래 구역 안에서만 고른다. 자기 건물 안에서만 지내는 직원에게 켠다."
        )]
        private bool stayInArea;

        [SerializeField]
        [Tooltip("월드 좌표 구역. 건물 1층 실내처럼 NPC가 지낼 공간을 감싼다.")]
        private Bounds area;

        private readonly Dictionary<string, List<HoverableObject>> placed =
            new Dictionary<string, List<HoverableObject>>();
        private readonly List<INpcSeat> seats = new List<INpcSeat>();
        private readonly Dictionary<string, float> unreachableUntil =
            new Dictionary<string, float>();
        private readonly List<Vector3> route = new List<Vector3>();

        // 켜져 있는 NPC 생활. 같은 카운터를 맡은 직원 중 누가 응대할지 고를 때 쓴다.
        private static readonly List<NpcRoutine> Living = new List<NpcRoutine>();
        private static readonly List<CounterPost> CounterPosts = new List<CounterPost>();

        // 카운터마다 지금 손님을 응대하는 직원 NPC 키. 손님이 카운터를 떠나면 지운다.
        private static readonly Dictionary<HoverableObject, string> CounterServers =
            new Dictionary<HoverableObject, string>();

        private NavMeshPath path;
        private Passerby body;
        private CharacterController characterController;
        private CharacterAnimationDriver animationDriver;
        private Transform visual;
        private NpcLife life;
        private string npcCode;
        private string role;
        private bool hasStarted;

        // 지금 고른 행동과 그 행동을 할 자리
        private NpcActionRule spotAction;
        private Spot spot;

        private bool hasRoute;
        private Vector3 routeDestination;
        private int routeCorner;
        private float closestRemaining;
        private float stuckSeconds;

        // 맡은 카운터와 처음 놓인 자리에서 카운터까지의 거리. 카운터가 없는 NPC(행인)는 어디서든 다가와 말을 건다.
        private bool hasCounter;
        private HoverableObject counter;
        private Bounds counterBounds;
        private float postMetersFromCounter;

        // 둘러보는 중인 사물과 그 앞에서 머무를 시각(Time.time). 걸어가는 중이면 음수
        private bool lookingAround;
        private HoverableObject lookAroundTarget;
        private Spot lookAroundSpot;
        private float lingerUntil;

        private INpcSeat seat;
        private SeatedLegs legs;
        private StandingPoseCorrections standingCorrections;
        private Vector3 standingPosition;
        private Vector3 standingVisualPosition;

        private enum Progress
        {
            Walking,
            Arrived,
            Stuck,
        }

        /// <summary>하려고 고른 행동. 걸어가는 중이거나 이용 중이다. 할 일이 없으면 null</summary>
        public NpcActionRule Doing => life?.Doing;

        public bool IsUsing => life != null && life.IsUsing;

        public bool IsSeated => seat != null;

        /// <summary>
        /// 교대 밖(쉬는 시간·퇴근)이라 일하지 않는 직원인지. 먼저 다가와 제안하지 않고, 플레이어가 먼저 말을 걸면 일상 대화로 이어진다.
        /// 행인과 첫날 미션이 찾아가라고 한 NPC는 false다.
        /// </summary>
        public bool IsOffDuty => life != null && NpcDuty.IsOffDuty(npcCode, role, life.Activity);

        private void Awake()
        {
            body = GetComponent<Passerby>();
            characterController = GetComponent<CharacterController>();
            animationDriver = GetComponent<CharacterAnimationDriver>();
            path = new NavMeshPath();

            npcCode = GetComponent<NpcIdentity>().Code;
            NpcLifeData data = lifeData != null ? NpcLifeData.Parse(lifeData.text) : null;
            NpcLifeEntry entry = data?.FindNpc(npcCode);
            if (entry == null)
            {
                // 생활 데이터가 없는 NPC는 전처럼 제자리로 돌아간다.
                enabled = false;
                return;
            }

            role = entry.Role;
            life = new NpcLife(data, entry, GameClock.Shared.Minutes);
            UseLifeAnimator();
        }

        private void OnEnable()
        {
            Living.Add(this);
            if (hasStarted)
                RefreshSceneObjects();
        }

        private void Start()
        {
            hasStarted = true;
            RefreshSceneObjects();
        }

        private void RefreshSceneObjects()
        {
            if (life == null)
                return;
            // 매장을 다시 방문하면 의자와 카운터는 새로 로드한 실내 씬의 인스턴스다.
            placed.Clear();
            seats.Clear();
            unreachableUntil.Clear();
            spotAction = null;
            hasRoute = false;
            hasCounter = false;
            counter = null;
            lookingAround = false;
            lookAroundTarget = null;
            CollectSceneObjects();
            FindCounter();
        }

        private void OnDisable()
        {
            Living.Remove(this);
            if (
                hasCounter
                && CounterServers.TryGetValue(counter, out string server)
                && server == npcCode
            )
            {
                CounterServers.Remove(counter);
            }

            StandUp();
        }

        // 장면에는 플레이어와 같은 이동 컨트롤러로 저장해 두고, 생활하는 동안만 동작 상태가 더해진 컨트롤러를 쓴다.
        private void UseLifeAnimator()
        {
            Animator model = GetComponentsInChildren<Animator>()
                .FirstOrDefault(animator => animator.avatar != null && animator.avatar.isHuman);
            if (lifeAnimator == null || model == null)
            {
                return;
            }

            model.runtimeAnimatorController = NpcLifeAnimator.KeepCharacterClips(
                lifeAnimator,
                model.runtimeAnimatorController
            );
            // 바꿔 끼운 직후에는 새 파라미터가 아직 묶이지 않아 다시 묶는다.
            model.Rebind();
            if (animationDriver != null)
            {
                animationDriver.BindAnimator(model);
            }
        }

        /// <summary>
        /// 먼저 다가와 제안해도 되는지. 교대 밖의 직원은 제안하지 않는다. 카운터를 맡은 직원은 그 카운터를 응대하는 한 명이고
        /// 손님이 카운터 가까이 와서 카운터 뒤 자리로 돌아갔을 때만 제안한다. 그 밖에는 플레이어가 먼저 [말 걸기]로 대화할 수 있다.
        /// </summary>
        internal bool CanOfferTo(Vector3 playerPosition)
        {
            if (IsOffDuty)
            {
                return false;
            }

            return !hasCounter || (ServesCounter(playerPosition) && IsAtPost());
        }

        /// <summary>다가오지 않는 동안(기다림·쉬기) <see cref="Passerby"/>가 매 프레임 부른다.</summary>
        internal void Live(Vector3 playerPosition)
        {
            Decide(playerPosition);
            // 사물을 쓰는 동안에만 그 행동의 동작을 재생하고, 걷거나 서 있을 때는 이동 동작으로 돌아온다.
            NpcAnimation animation = life.IsUsing
                ? NpcAnimations.WhileUsing(life.Doing.Action, seat != null)
                : NpcAnimation.None;
            if (animationDriver != null)
            {
                animationDriver.SetAction((int)animation);
            }
        }

        private void Decide(Vector3 playerPosition)
        {
            double now = GameClock.Shared.Minutes;
            life.Advance(now);
            if (ServesCounter(playerPosition))
            {
                ServeAtCounter(playerPosition);
                return;
            }

            if (spotAction != null && IsReservedForAnotherServer(spot.StandPoint))
                Interrupt();

            if (life.Doing == null)
            {
                life.ChooseNext(IsPlaced);
            }

            if (life.Doing != null)
            {
                lookingAround = false;
                DoChosenAction(now);
                return;
            }

            spotAction = null;
            LookAround();
        }

        /// <summary>말을 걸러 가거나 대화하는 동안 <see cref="Passerby"/>가 부른다. 하던 일을 그만두고 앉아 있었으면 일어난다.</summary>
        internal void Interrupt()
        {
            life?.GiveUp();
            spotAction = null;
            lookingAround = false;
            hasRoute = false;
            StandUp();
        }

        // 하던 일을 그만두고 카운터 뒤 자리로 돌아가 손님 쪽으로 선다. 제안은 자리에 선 뒤 Passerby가 띄운다.
        private void ServeAtCounter(Vector3 customerPosition)
        {
            if (life.Doing != null)
            {
                life.GiveUp();
                spotAction = null;
                hasRoute = false;
            }

            lookingAround = false;
            StandUp();
            Pose post = ServicePose();
            Vector3 toCustomer = Flat(customerPosition - post.position);
            Quaternion facing =
                toCustomer.sqrMagnitude > .0001f
                    ? Quaternion.LookRotation(toCustomer)
                    : post.rotation;
            if (GoTo(post.position, facing) == Progress.Stuck)
            {
                // 길을 막은 사람이 비키면 다시 간다.
                hasRoute = false;
            }
        }

        // 급한 일이 없으면 둘러볼 사물을 골라 걸어가 잠시 머문 뒤 다른 사물로 옮겨 간다. 앉아 있었으면 일어난다.
        private void LookAround()
        {
            if (!lookingAround)
            {
                StandUp();
                if (!TryPickLookAroundSpot())
                {
                    body.StandFacing(transform.rotation);
                    return;
                }

                lookingAround = true;
                lingerUntil = -1f;
                hasRoute = false;
            }

            if (lingerUntil < 0f)
            {
                Progress progress = GoTo(lookAroundSpot.StandPoint, lookAroundSpot.Facing);
                if (progress == Progress.Stuck)
                {
                    lookingAround = false;
                }
                else if (progress == Progress.Arrived)
                {
                    lingerUntil =
                        Time.time + Random.Range(LookAroundMinSeconds, LookAroundMaxSeconds);
                }

                return;
            }

            body.StandFacing(lookAroundSpot.Facing);
            if (Time.time >= lingerUntil)
            {
                lookingAround = false;
            }
        }

        private bool TryPickLookAroundSpot()
        {
            var candidates = new List<HoverableObject>();
            Vector3 home = body.HomePose.position;
            foreach (List<HoverableObject> sameKey in placed.Values)
            {
                foreach (HoverableObject hoverable in sameKey)
                {
                    Vector3 center = SolidBounds(hoverable).center;
                    if (
                        hoverable != lookAroundTarget
                        && Flat(center - home).magnitude <= LookAroundRadiusMeters
                        && Flat(center - transform.position).magnitude >= LookAroundStepMeters
                    )
                    {
                        candidates.Add(hoverable);
                    }
                }
            }

            bool hasWalkArea = HasWalkArea();
            for (int i = 0; i < LookAroundTries && candidates.Count > 0; i++)
            {
                int pick = Random.Range(0, candidates.Count);
                HoverableObject target = candidates[pick];
                candidates.RemoveAt(pick);
                if (TryStandBeside(SolidBounds(target), hasWalkArea, out lookAroundSpot))
                {
                    lookAroundTarget = target;
                    return true;
                }
            }

            return false;
        }

        // 손님이 카운터 앞에 왔을 때 같은 카운터를 맡은 직원 중 응대할 한 명인지. 근무 중이고 하는 일이 없는 가까운 직원이 맡고,
        // 맡은 직원은 손님이 떠날 때까지 바뀌지 않는다. 근무 중이 아니면 카운터를 맡지 않고, 고르지 않은 직원은 하던 일을 계속한다.
        private bool ServesCounter(Vector3 playerPosition)
        {
            if (!hasCounter)
            {
                return false;
            }

            if (!IsCustomerAtCounter(playerPosition))
            {
                CounterServers.Remove(counter);
                return false;
            }

            if (IsOffDuty)
            {
                return false;
            }

            CounterPosts.Clear();
            foreach (NpcRoutine other in Living)
            {
                if (other.hasCounter && other.counter == counter)
                {
                    CounterPosts.Add(
                        new CounterPost(
                            other.npcCode,
                            !other.IsOffDuty,
                            other.Doing != null,
                            other.MetersFromCounter()
                        )
                    );
                }
            }

            CounterServers.TryGetValue(counter, out string currentServer);
            string server = NpcDuty.PickServer(CounterPosts, currentServer);
            CounterServers[counter] = server;
            return server == npcCode;
        }

        // 지금 서 있는 곳에서 맡은 카운터까지의 수평 거리
        private float MetersFromCounter()
        {
            Vector3 closest = counterBounds.ClosestPoint(
                new Vector3(transform.position.x, counterBounds.center.y, transform.position.z)
            );
            return Flat(closest - transform.position).magnitude;
        }

        private bool IsCustomerAtCounter(Vector3 playerPosition)
        {
            Vector3 closest = counterBounds.ClosestPoint(
                new Vector3(playerPosition.x, counterBounds.center.y, playerPosition.z)
            );
            return Flat(closest - playerPosition).magnitude <= serveRadiusMeters;
        }

        private bool IsAtPost()
        {
            return Flat(ServicePose().position - transform.position).magnitude <= AtPostMeters;
        }

        // 카운터에서 손님을 맞는 자리. 같은 카운터를 맡은 직원 자리 중 카운터에 가장 가까운 곳(카운터 뒤 계산대 자리)이라,
        // 누가 응대하든 손님 바로 앞에 선다. 거리가 같으면 NPC 키 순서다.
        private Pose ServicePose()
        {
            NpcRoutine register = this;
            foreach (NpcRoutine other in Living)
            {
                if (
                    other.hasCounter
                    && other.counter == counter
                    && (
                        other.postMetersFromCounter < register.postMetersFromCounter
                        || (
                            other.postMetersFromCounter.Equals(register.postMetersFromCounter)
                            && string.CompareOrdinal(other.npcCode, register.npcCode) < 0
                        )
                    )
                )
                {
                    register = other;
                }
            }

            return register.body.HomePose;
        }

        // 처음 놓인 자리 바로 앞의 카운터. 카운터 없이 놓인 NPC는 카운터를 맡지 않는다.
        private void FindCounter()
        {
            float nearest = CounterNearPostMeters;
            foreach (string key in ServiceCounterKeys)
            {
                if (!placed.TryGetValue(key, out List<HoverableObject> counters))
                {
                    continue;
                }

                foreach (HoverableObject candidate in counters)
                {
                    Bounds bounds = SolidBounds(candidate);
                    Vector3 closest = bounds.ClosestPoint(
                        new Vector3(transform.position.x, bounds.center.y, transform.position.z)
                    );
                    float distance = Flat(closest - transform.position).magnitude;
                    if (distance <= nearest)
                    {
                        nearest = distance;
                        hasCounter = true;
                        counter = candidate;
                        counterBounds = bounds;
                        postMetersFromCounter = distance;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (seat != null)
            {
                // Animator가 이번 프레임의 기본 자세를 갱신한 뒤 다리를 구부린다.
                legs.Apply(
                    visual,
                    standingVisualPosition,
                    seat.SeatPoint,
                    seat.CushionTopMeters,
                    seat.FrontEdgeMeters
                );
            }
        }

        private void DoChosenAction(double now)
        {
            if (spotAction != life.Doing)
            {
                StandUp();
                spotAction = life.Doing;
                hasRoute = false;
                if (!TryFindSpot(spotAction.ObjectKey, out spot))
                {
                    GiveUpUnreachable();
                    return;
                }
            }

            if (life.IsUsing)
            {
                if (seat == null)
                {
                    body.StandFacing(spot.Facing);
                }

                return;
            }

            // 같은 행동을 다음 일과에서도 고르면 Doing 참조가 같아도 이전 앉기는 끝내야 한다.
            StandUp();
            Progress progress = GoTo(spot.StandPoint, spot.Facing);
            if (progress == Progress.Stuck)
            {
                GiveUpUnreachable();
                return;
            }

            if (progress == Progress.Arrived)
            {
                // 자리가 그새 찼거나 앉을 수 없는 모델이면 선 채로 쉰다.
                if (spot.Seat != null)
                {
                    SitOn(spot.Seat);
                }

                life.BeginUsing(now);
            }
        }

        private void GiveUpUnreachable()
        {
            unreachableUntil[spotAction.ObjectKey] = Time.time + RetryUnreachableSeconds;
            life.GiveUp();
            spotAction = null;
            hasRoute = false;
        }

        private bool IsPlaced(string objectKey)
        {
            return placed.ContainsKey(objectKey)
                && (
                    !unreachableUntil.TryGetValue(objectKey, out float until) || Time.time >= until
                );
        }

        // 걸어서 destination에 닿으면 그 방향으로 선다.
        private Progress GoTo(Vector3 destination, Quaternion facing)
        {
            if (!hasRoute || routeDestination != destination)
            {
                PlanRoute(destination);
            }

            if (Flat(destination - transform.position).magnitude <= ArrivedMeters)
            {
                body.StandFacing(facing);
                return Progress.Arrived;
            }

            while (
                routeCorner < route.Count - 1
                && Flat(route[routeCorner] - transform.position).magnitude <= CornerReachedMeters
            )
            {
                routeCorner++;
            }

            float remaining = RemainingRouteMeters();
            if (remaining < closestRemaining - StuckProgressMeters)
            {
                closestRemaining = remaining;
                stuckSeconds = 0f;
            }
            else
            {
                stuckSeconds += Time.deltaTime;
                if (stuckSeconds > StuckSeconds)
                {
                    body.StandFacing(transform.rotation);
                    return Progress.Stuck;
                }
            }

            body.WalkToward(
                Flat(route[routeCorner] - transform.position),
                walkSpeedMetersPerSecond
            );
            return Progress.Walking;
        }

        private void PlanRoute(Vector3 destination)
        {
            route.Clear();
            routeCorner = 0;
            routeDestination = destination;
            hasRoute = true;
            closestRemaining = float.PositiveInfinity;
            stuckSeconds = 0f;
            if (
                NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit start,
                    1f,
                    NavMesh.AllAreas
                )
                && NavMesh.CalculatePath(start.position, destination, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete
            )
            {
                Vector3[] corners = path.corners;
                for (int i = 1; i < corners.Length; i++)
                {
                    route.Add(corners[i]);
                }
            }

            if (route.Count == 0)
            {
                // NavMesh가 없는 장면이면 곧장 걷는다. 막히면 그 사물은 포기한다.
                route.Add(destination);
            }
        }

        private float RemainingRouteMeters()
        {
            float remaining = Flat(route[routeCorner] - transform.position).magnitude;
            for (int i = routeCorner + 1; i < route.Count; i++)
            {
                remaining += Flat(route[i] - route[i - 1]).magnitude;
            }

            return remaining;
        }

        // 사물 키가 같은 사물 중 걸어서 가장 가까운 자리. 앉을 자리가 있는 사물(의자·벤치·소파)이면 빈 자리에 앉고, 없으면 사물 옆에 선다.
        private bool TryFindSpot(string objectKey, out Spot found)
        {
            bool hasWalkArea = HasWalkArea();
            found = default;
            float best = float.PositiveInfinity;
            var withoutSeat = new List<Bounds>();
            foreach (HoverableObject hoverable in placed[objectKey])
            {
                Bounds bounds = SolidBounds(hoverable);
                bool hasSeat = false;
                foreach (INpcSeat candidate in seats)
                {
                    if (!Contains(bounds, candidate.SeatPoint.position))
                    {
                        continue;
                    }

                    hasSeat = true;
                    if (candidate.IsTaken)
                    {
                        continue;
                    }

                    Transform point = candidate.SeatPoint;
                    Vector3 front =
                        point.position
                        + point.forward * (candidate.FrontEdgeMeters + SeatApproachMeters);
                    if (
                        TryReach(front, hasWalkArea, out Vector3 standPoint, out float length)
                        && length < best
                    )
                    {
                        best = length;
                        found = new Spot(standPoint, point.rotation, candidate);
                    }
                }

                if (!hasSeat)
                {
                    withoutSeat.Add(bounds);
                }
            }

            if (best < float.PositiveInfinity)
            {
                return true;
            }

            foreach (Bounds bounds in withoutSeat)
            {
                if (
                    TryStandBeside(bounds, hasWalkArea, out Spot beside, out float length)
                    && length < best
                )
                {
                    best = length;
                    found = beside;
                }
            }

            return best < float.PositiveInfinity;
        }

        private bool TryStandBeside(Bounds bounds, bool hasWalkArea, out Spot found)
        {
            return TryStandBeside(bounds, hasWalkArea, out found, out float _);
        }

        private bool IsReservedForAnotherServer(Vector3 point)
        {
            return hasCounter
                && CounterServers.TryGetValue(counter, out string server)
                && !string.IsNullOrEmpty(server)
                && server != npcCode
                && Flat(ServicePose().position - point).sqrMagnitude
                    < StandingSpaceMeters * StandingSpaceMeters;
        }

        // 도착한 뒤뿐 아니라 이동 중에도 목적지를 예약해 같은 위치를 고르지 않는다.
        private bool StandingPlaceTaken(Vector3 point)
        {
            if (IsReservedForAnotherServer(point))
                return true;
            float spacingSquared = StandingSpaceMeters * StandingSpaceMeters;
            foreach (NpcRoutine other in Living)
            {
                if (other == this || other.gameObject.scene != gameObject.scene)
                    continue;
                if (
                    other.spotAction != null
                    && Flat(other.spot.StandPoint - point).sqrMagnitude < spacingSquared
                )
                    return true;
                if (
                    other.lookingAround
                    && Flat(other.lookAroundSpot.StandPoint - point).sqrMagnitude < spacingSquared
                )
                    return true;
            }
            return false;
        }

        // 사물 둘레 여러 방향에서 콜라이더 바깥으로 StandOffMeters 떨어진 바닥 중 걸어서 가장 가까운 곳에 사물을 보고 선다.
        private bool TryStandBeside(
            Bounds bounds,
            bool hasWalkArea,
            out Spot found,
            out float bestLength
        )
        {
            found = default;
            bestLength = float.PositiveInfinity;
            // 작은 사물 바로 옆이 차 있으면 조금 물러선 자리도 찾는다.
            for (int i = 0; i < SpotDirections * 2; i++)
            {
                if (i == SpotDirections && bestLength < float.PositiveInfinity)
                    break;
                float angle = (i % SpotDirections) * Mathf.PI * 2f / SpotDirections;
                float standOff = StandOffMeters * (1 + i / SpotDirections);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float toEdge = Mathf.Min(
                    Mathf.Abs(direction.x) > .0001f
                        ? bounds.extents.x / Mathf.Abs(direction.x)
                        : float.PositiveInfinity,
                    Mathf.Abs(direction.z) > .0001f
                        ? bounds.extents.z / Mathf.Abs(direction.z)
                        : float.PositiveInfinity
                );
                Vector3 point =
                    new Vector3(bounds.center.x, transform.position.y, bounds.center.z)
                    + direction * (toEdge + standOff);
                if (
                    !TryReach(point, hasWalkArea, out Vector3 standPoint, out float length)
                    || length >= bestLength
                    || StandingPlaceTaken(standPoint)
                )
                {
                    continue;
                }

                bestLength = length;
                Vector3 toObject = Flat(bounds.center - standPoint);
                found = new Spot(
                    standPoint,
                    toObject.sqrMagnitude > .0001f
                        ? Quaternion.LookRotation(toObject)
                        : transform.rotation,
                    null
                );
            }

            return bestLength < float.PositiveInfinity;
        }

        private bool HasWalkArea()
        {
            return NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit _,
                1f,
                NavMesh.AllAreas
            );
        }

        // 그 점 가까운 NavMesh 위 자리와 거기까지 걷는 경로 길이. NavMesh가 없는 장면이면 곧은 거리
        private bool TryReach(
            Vector3 point,
            bool hasWalkArea,
            out Vector3 standPoint,
            out float length
        )
        {
            standPoint = point;
            length = Flat(point - transform.position).magnitude;
            if (!hasWalkArea)
            {
                return true;
            }

            if (
                !NavMesh.SamplePosition(point, out NavMeshHit hit, SpotSnapMeters, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit start,
                    1f,
                    NavMesh.AllAreas
                )
                || !NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
            )
            {
                return false;
            }

            standPoint = hit.position;
            length = 0f;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return true;
        }

        // 다리를 구부릴 Humanoid 모델이 아니면 앉지 않고 자리 앞에 선 채로 쉰다.
        private void SitOn(INpcSeat target)
        {
            Animator animator = GetComponentInChildren<Animator>();
            legs = SeatedLegs.Find(animator);
            if (legs == null || !target.TryTake(this))
            {
                return;
            }

            visual = ModelRoot(animator.transform);
            seat = target;
            standingPosition = transform.position;
            standingVisualPosition = visual.localPosition;
            standingCorrections = StandingPoseCorrections.Suspend(visual);
            characterController.enabled = false;
            Transform point = target.SeatPoint;
            transform.SetPositionAndRotation(
                new Vector3(point.position.x, standingPosition.y, point.position.z),
                point.rotation
            );
            if (animationDriver != null)
            {
                animationDriver.SetLocomotion(0f, true, 0f);
            }
        }

        // 앉기 전에 서 있던 곳으로 일어난다. 그곳은 몸이 들어가 있던 빈 자리라 의자·탁자에 끼지 않는다.
        private void StandUp()
        {
            if (seat == null)
            {
                return;
            }

            seat.Release(this);
            seat = null;
            visual.localPosition = standingVisualPosition;
            standingCorrections.Restore();
            transform.position = standingPosition;
            characterController.enabled = true;
        }

        // 모델은 NPC 바로 아래 어느 자식에든 있을 수 있다(Visual 자리 표시를 끄고 따로 둔 모델 등). 그 자식을 통째로 옮긴다.
        private Transform ModelRoot(Transform model)
        {
            while (model.parent != transform)
            {
                model = model.parent;
            }

            return model;
        }

        // 구역이 정해진 NPC는 구역 밖 사물·자리를 처음부터 모으지 않아, 일과·둘러보기 모두 구역 안에서만 고른다.
        private void CollectSceneObjects()
        {
            foreach (
                HoverableObject hoverable in FindObjectsByType<HoverableObject>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    hoverable.gameObject.scene != gameObject.scene
                    || string.IsNullOrEmpty(hoverable.UnityKey)
                    || !NpcStayArea.Allows(stayInArea, area, SolidBounds(hoverable).center)
                )
                {
                    continue;
                }

                if (!placed.TryGetValue(hoverable.UnityKey, out List<HoverableObject> sameKey))
                {
                    sameKey = new List<HoverableObject>();
                    placed.Add(hoverable.UnityKey, sameKey);
                }

                sameKey.Add(hoverable);
            }

            foreach (
                MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                )
            )
            {
                if (
                    behaviour is INpcSeat candidate
                    && behaviour.gameObject.scene == gameObject.scene
                    && NpcStayArea.Allows(stayInArea, area, candidate.SeatPoint.position)
                )
                {
                    seats.Add(candidate);
                }
            }
        }

        private static Bounds SolidBounds(HoverableObject hoverable)
        {
            var bounds = new Bounds(hoverable.transform.position, Vector3.zero);
            bool found = false;
            foreach (Collider collider in hoverable.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            return bounds;
        }

        private static bool Contains(Bounds bounds, Vector3 point)
        {
            return Mathf.Abs(point.x - bounds.center.x) <= bounds.extents.x + SeatMatchMeters
                && Mathf.Abs(point.z - bounds.center.z) <= bounds.extents.z + SeatMatchMeters;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private readonly struct Spot
        {
            public Spot(Vector3 standPoint, Quaternion facing, INpcSeat seat)
            {
                StandPoint = standPoint;
                Facing = facing;
                Seat = seat;
            }

            public Vector3 StandPoint { get; }
            public Quaternion Facing { get; }

            /// <summary>앉을 자리. 사물 옆에 서서 쓰면 null</summary>
            public INpcSeat Seat { get; }
        }
    }
}
