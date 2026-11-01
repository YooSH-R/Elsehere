using System.Collections.Generic;
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
    /// 근무 중에 할 일이 없으면 처음 놓인 자리로 돌아가고, 쉬는 시간·자유 시간·퇴근 뒤에는 하던 곳에 머문다.
    /// 장면을 다시 불러오면 그 시각의 시작값에서 다시 지낸다. 다가오기·대화는 <see cref="Passerby"/>가 맡고, 그동안 하던 일을 그만둔다.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(Passerby))]
    public class NpcRoutine : MonoBehaviour
    {
        // 사물 둘레에서 이만큼 떨어져 선다(몸 반지름 0.3m와 여유).
        private const float StandOffMeters = .45f;

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

        [SerializeField]
        [Tooltip("ai/tools/npc_life_export.py가 내보낸 Data/Npc/NpcLife.json")]
        private TextAsset lifeData;

        [SerializeField]
        [Min(.1f)]
        [Tooltip(
            "생활하며 걷는 속도. 다가와 말을 거는 속도(PasserbySettings)와 따로 두어, 다가오지 않는 카페 직원도 사물까지는 걷는다."
        )]
        private float walkSpeedMetersPerSecond = 1.1f;

        private readonly Dictionary<string, List<HoverableObject>> placed =
            new Dictionary<string, List<HoverableObject>>();
        private readonly List<INpcSeat> seats = new List<INpcSeat>();
        private readonly Dictionary<string, float> unreachableUntil =
            new Dictionary<string, float>();
        private readonly List<Vector3> route = new List<Vector3>();
        private NavMeshPath path;
        private Passerby body;
        private CharacterController characterController;
        private CharacterAnimationDriver animationDriver;
        private Transform visual;
        private NpcLife life;

        // 지금 고른 행동과 그 행동을 할 자리
        private NpcActionRule spotAction;
        private Spot spot;

        private bool hasRoute;
        private Vector3 routeDestination;
        private int routeCorner;
        private float closestRemaining;
        private float stuckSeconds;

        private INpcSeat seat;
        private SeatedLegs legs;
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

        private void Awake()
        {
            body = GetComponent<Passerby>();
            characterController = GetComponent<CharacterController>();
            animationDriver = GetComponent<CharacterAnimationDriver>();
            path = new NavMeshPath();

            NpcLifeData data = lifeData != null ? NpcLifeData.Parse(lifeData.text) : null;
            NpcLifeEntry entry = data?.FindNpc(GetComponent<NpcIdentity>().Code);
            if (entry == null)
            {
                // 생활 데이터가 없는 NPC는 전처럼 제자리로 돌아간다.
                enabled = false;
                return;
            }

            life = new NpcLife(data, entry, GameClock.Shared.Minutes);
            CollectSceneObjects();
        }

        private void OnDisable()
        {
            StandUp();
        }

        /// <summary>다가오지 않는 동안(기다림·쉬기) <see cref="Passerby"/>가 매 프레임 부른다.</summary>
        internal void Live()
        {
            double now = GameClock.Shared.Minutes;
            life.Advance(now);
            if (life.Doing == null)
            {
                life.ChooseNext(IsPlaced);
            }

            if (life.Doing != null)
            {
                DoChosenAction(now);
                return;
            }

            spotAction = null;
            if (life.BelongsAtPost)
            {
                StandUp();
                Pose post = body.HomePose;
                if (GoTo(post.position, post.rotation) == Progress.Stuck)
                {
                    // 길을 막은 사람이 비키면 다시 간다.
                    hasRoute = false;
                }
            }
            else if (seat == null)
            {
                // 쉬는 시간·자유 시간에는 하던 곳에 머문다. 앉아 있었으면 계속 앉아 있다.
                body.StandFacing(transform.rotation);
            }
        }

        /// <summary>말을 걸러 가거나 대화하는 동안 <see cref="Passerby"/>가 부른다. 하던 일을 그만두고 앉아 있었으면 일어난다.</summary>
        internal void Interrupt()
        {
            life?.GiveUp();
            spotAction = null;
            hasRoute = false;
            StandUp();
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
            bool hasWalkArea = NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit _,
                1f,
                NavMesh.AllAreas
            );
            found = default;
            float best = float.PositiveInfinity;
            var standing = new List<(Vector3 Point, Vector3 Center)>();
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
                    AddAround(bounds, standing);
                }
            }

            if (best < float.PositiveInfinity)
            {
                return true;
            }

            foreach ((Vector3 point, Vector3 center) in standing)
            {
                if (
                    TryReach(point, hasWalkArea, out Vector3 standPoint, out float length)
                    && length < best
                )
                {
                    best = length;
                    Vector3 toObject = Flat(center - standPoint);
                    found = new Spot(
                        standPoint,
                        toObject.sqrMagnitude > .0001f
                            ? Quaternion.LookRotation(toObject)
                            : transform.rotation,
                        null
                    );
                }
            }

            return best < float.PositiveInfinity;
        }

        // 사물 둘레 여러 방향에서 콜라이더 바깥으로 StandOffMeters 떨어진 바닥 높이의 점
        private void AddAround(Bounds bounds, List<(Vector3 Point, Vector3 Center)> standing)
        {
            for (int i = 0; i < SpotDirections; i++)
            {
                float angle = i * Mathf.PI * 2f / SpotDirections;
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
                    + direction * (toEdge + StandOffMeters);
                standing.Add((point, bounds.center));
            }
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
