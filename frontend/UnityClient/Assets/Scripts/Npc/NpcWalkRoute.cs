using UnityEngine;
using UnityEngine.AI;

namespace A305.Npc
{
    /// <summary>
    /// 마을 NPC가 다음에 향할 길 지점과, 그 길이 곧 횡단보도로 들어가는지를 정한다.
    /// 인도만으로 갈 수 있으면 횡단보도를 밟지 않는 인도 길을 쓰고, 길을 건너야 할 때만 횡단보도를 지난다.
    /// 횡단보도 끝은 인도 안쪽까지 들어와 있어서, 인도를 따라 걷다 끝자락을 스치는 걸음을 길을 건너는 것으로 보지 않게 한다.
    /// </summary>
    public sealed class NpcWalkRoute
    {
        // NPC가 걸을 바닥과 같은 사람 크기 요원(Humanoid)으로 구운 바닥을 쓴다.
        private const int HumanoidAgentTypeId = 0;

        // 길을 건너야 할 때도 건너기 전후에는 횡단보도 끝자락보다 인도를 밟게 한다.
        private const float CrosswalkCost = 2f;

        // 인도 길이 건너는 길보다 이만큼 길지 않으면 인도로 간다. 그보다 길면(같은 쪽 인도로 블록을 도는 길 등) 건넌다.
        private const float SidewalkDetourRatio = 1.25f;
        private const float SidewalkDetourMeters = 1.5f;

        // 길을 건너는 방향은 횡단보도에 들어간 뒤 이만큼 나아간 쪽으로 본다. 연석 모서리에서 꺾이는 짧은 구간은 건너는 방향이 아니다.
        private const float AcrossProbeMeters = 1.5f;

        // 횡단보도에 발을 디딜 때 향하는 지점은 들어선 곳에서 길을 따라 이만큼 더 간 곳이다.
        private const float StepInMeters = .3f;

        // 인도 바닥 바로 위에 있다고 볼 수평 거리. 횡단보도 위에 서면 가장 가까운 인도가 경계만큼 떨어진다.
        private const float SidewalkFootMeters = .02f;

        private const int MaxCorners = 32;
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly NavMeshPath sidewalkPath = new NavMeshPath();
        private readonly Vector3[] buffer = new Vector3[MaxCorners];

        // 0번은 NPC의 현재 위치, 그다음부터 걸을 경로 지점.
        private readonly Vector3[] points = new Vector3[MaxCorners];
        private int count;
        private int areas;

        /// <summary>다음에 향할 지점.</summary>
        public Vector3 Next => points[1];

        /// <summary><see cref="Next"/> 바로 다음 경로 지점. 걸어서 곧장 갈 수 있을 때만 있다.</summary>
        public Vector3? AfterNext { get; private set; }

        /// <summary>정한 길이 어디선가 길을 건너는지. 인도만으로 가는 길이면 false.</summary>
        public bool CrossesRoad { get; private set; }

        /// <summary>현재 위치에서 목적지까지 걸을 길을 정한다. 갈 수 없으면 false.</summary>
        public bool TryPlan(Vector3 position, Vector3 destination)
        {
            AfterNext = null;
            count = 0;
            CrossesRoad = false;
            if (TryPlanOnSidewalk(position, destination))
                return true;
            CrossesRoad = true;
            areas = NpcTownWalker.WalkingAreas;
            if (
                !NavMesh.SamplePosition(position, out NavMeshHit from, 1f, areas)
                || !NavMesh.SamplePosition(destination, out NavMeshHit to, 1f, areas)
            )
                return false;
            if (!NavMesh.Raycast(from.position, to.position, out _, areas))
            {
                SetStraight(position, destination);
                return true;
            }
            if (!CalculateWalkingPath(from.position, to.position, path))
                return false;
            SetCorners(position, path, from.position);
            return true;
        }

        /// <summary>정한 길이 인도를 벗어나 횡단보도로 들어서는 곳.</summary>
        public readonly struct CrossingApproach
        {
            public CrossingApproach(Vector3 entry, Vector3 stepIn, Vector3 across)
            {
                Entry = entry;
                StepIn = stepIn;
                Across = across;
            }

            /// <summary>인도를 벗어나는 지점.</summary>
            public Vector3 Entry { get; }

            /// <summary>길을 따라 횡단보도 안쪽으로 한 걸음 들어간 지점. 이곳을 향해 걸어 횡단보도에 발을 디딘다.</summary>
            public Vector3 StepIn { get; }

            /// <summary>길을 건너는 방향. 연석 모서리에서 꺾이는 짧은 구간이 아니라 횡단보도를 지나는 방향이다.</summary>
            public Vector3 Across { get; }
        }

        /// <summary>
        /// 정한 길을 따라 meters 안에서 인도를 벗어나 횡단보도로 들어가는지.
        /// 이미 차도·횡단보도 위에 있으면 건너는 중이므로 false.
        /// </summary>
        public bool TryFindCrossingAhead(float meters, out CrossingApproach approach)
        {
            approach = default;
            int crosswalk = NavMesh.GetAreaFromName("Crosswalk");
            int standing = NpcTownWalker.StandingAreas;
            if (count < 2 || crosswalk < 0 || !TryStandOnSidewalk(points[0], out Vector3 source))
                return false;
            float walked = 0;
            for (int i = 1; i < count && walked <= meters; i++)
            {
                Vector3 target = points[i];
                float length = Flat(target - source).magnitude;
                if (NavMesh.Raycast(source, target, out NavMeshHit hit, standing))
                {
                    float reached = walked + Flat(hit.position - source).magnitude;
                    Vector3 inward = Flat(target - source).normalized * .05f;
                    // 장애물 둘레의 빈 곳에 닿은 것은 길을 건너는 것이 아니다.
                    if (
                        reached <= meters
                        && NavMesh.SamplePosition(hit.position + inward, out _, .1f, 1 << crosswalk)
                    )
                    {
                        // 경계선을 따라 붙어 가는 구간은 횡단보도로 들어가는 것이 아니다. 한 걸음 더 간 곳이 횡단보도여야 들어간다.
                        Vector3 stepIn = PointAlong(reached + StepInMeters);
                        Vector3 entry = hit.position;
                        Vector3 across = Flat(PointAlong(reached + AcrossProbeMeters) - entry);
                        if (!IsOnSidewalk(stepIn) && across.sqrMagnitude > .0001f)
                        {
                            approach = new CrossingApproach(entry, stepIn, across);
                            return true;
                        }
                    }
                }
                walked += length;
                source = target;
            }
            return false;
        }

        /// <summary>
        /// 발밑이 인도(차도·횡단보도가 아닌 바닥)인지. 교차로 모서리 인도는 횡단보도와 거의 맞닿아 있어서,
        /// 경계에서 조금 떨어졌는지가 아니라 발밑 바닥으로 건너기 전과 건너는 중을 나눈다.
        /// </summary>
        public static bool IsOnSidewalk(Vector3 position) => TryStandOnSidewalk(position, out _);

        private static bool TryStandOnSidewalk(Vector3 position, out Vector3 ground)
        {
            ground = position;
            if (
                !NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    .5f,
                    NpcTownWalker.StandingAreas
                )
                || Flat(hit.position - position).magnitude > SidewalkFootMeters
            )
                return false;
            ground = hit.position;
            return true;
        }

        /// <summary>두 지점 사이에 NPC가 걸을 경로 지점들. 갈 수 없으면 null.</summary>
        public static Vector3[] Between(Vector3 from, Vector3 to)
        {
            int standing = NpcTownWalker.StandingAreas;
            int walking = NpcTownWalker.WalkingAreas;
            if (
                !NavMesh.SamplePosition(from, out NavMeshHit start, 2, walking)
                || !NavMesh.SamplePosition(to, out NavMeshHit end, 2, walking)
            )
                return null;
            var walkingPath = new NavMeshPath();
            bool walkable = CalculateWalkingPath(start.position, end.position, walkingPath);
            var sidewalk = new NavMeshPath();
            if (
                NavMesh.SamplePosition(from, out NavMeshHit sidewalkStart, 2, standing)
                && NavMesh.SamplePosition(to, out NavMeshHit sidewalkEnd, 2, standing)
                && NavMesh.CalculatePath(
                    sidewalkStart.position,
                    sidewalkEnd.position,
                    standing,
                    sidewalk
                )
                && sidewalk.status == NavMeshPathStatus.PathComplete
                && (
                    !walkable
                    || IsShortEnough(Length(sidewalk.corners), Length(walkingPath.corners))
                )
            )
                return sidewalk.corners.Length > 0
                    ? sidewalk.corners
                    : new[] { sidewalkStart.position };
            if (!walkable)
                return null;
            return walkingPath.corners.Length > 0 ? walkingPath.corners : new[] { start.position };
        }

        private bool TryPlanOnSidewalk(Vector3 position, Vector3 destination)
        {
            int standing = NpcTownWalker.StandingAreas;
            if (
                !NavMesh.SamplePosition(position, out NavMeshHit from, 1f, standing)
                || !NavMesh.SamplePosition(destination, out NavMeshHit to, 1f, standing)
            )
                return false;
            areas = standing;
            if (!NavMesh.Raycast(from.position, to.position, out _, standing))
            {
                SetStraight(position, destination);
                return true;
            }
            if (
                !NavMesh.CalculatePath(from.position, to.position, standing, sidewalkPath)
                || sidewalkPath.status != NavMeshPathStatus.PathComplete
            )
                return false;
            int corners = sidewalkPath.GetCornersNonAlloc(buffer);
            if (corners < 2)
                return false;
            // 인도 길이 크게 돌아가면 건너는 길과 견준다. 건너는 길이 없으면 인도 길을 쓴다.
            float sidewalkLength = Length(buffer, corners);
            if (
                sidewalkLength > Flat(to.position - from.position).magnitude * SidewalkDetourRatio
                && NavMesh.SamplePosition(
                    position,
                    out NavMeshHit walkFrom,
                    1f,
                    NpcTownWalker.WalkingAreas
                )
                && NavMesh.SamplePosition(
                    destination,
                    out NavMeshHit walkTo,
                    1f,
                    NpcTownWalker.WalkingAreas
                )
                && CalculateWalkingPath(walkFrom.position, walkTo.position, path)
                && !IsShortEnough(sidewalkLength, Length(path.corners))
            )
                return false;
            SetCorners(position, sidewalkPath, from.position);
            return true;
        }

        private static bool CalculateWalkingPath(Vector3 from, Vector3 to, NavMeshPath result)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = HumanoidAgentTypeId,
                areaMask = NpcTownWalker.WalkingAreas,
            };
            int crosswalk = NavMesh.GetAreaFromName("Crosswalk");
            if (crosswalk >= 0)
                filter.SetAreaCost(crosswalk, CrosswalkCost);
            return NavMesh.CalculatePath(from, to, filter, result)
                && result.status == NavMeshPathStatus.PathComplete
                && result.corners.Length >= 2;
        }

        private void SetStraight(Vector3 position, Vector3 destination)
        {
            points[0] = position;
            points[1] = destination;
            count = 2;
        }

        private void SetCorners(Vector3 position, NavMeshPath route, Vector3 start)
        {
            int corners = route.GetCornersNonAlloc(buffer);
            points[0] = position;
            count = 1;
            for (int i = 1; i < corners && count < MaxCorners; i++)
                points[count++] = buffer[i];
            if (count > 2 && !NavMesh.Raycast(start, points[2], out _, areas))
                AfterNext = points[2];
        }

        private Vector3 PointAlong(float distance)
        {
            float remaining = distance;
            for (int i = 1; i < count; i++)
            {
                float length = Flat(points[i] - points[i - 1]).magnitude;
                if (remaining <= length)
                    return Vector3.Lerp(
                        points[i - 1],
                        points[i],
                        length > 0 ? remaining / length : 0
                    );
                remaining -= length;
            }
            return points[count - 1];
        }

        private static bool IsShortEnough(float sidewalk, float walking) =>
            sidewalk <= walking * SidewalkDetourRatio + SidewalkDetourMeters;

        private static float Length(Vector3[] corners) => Length(corners, corners.Length);

        private static float Length(Vector3[] corners, int cornersCount)
        {
            float meters = 0;
            for (int i = 1; i < cornersCount; i++)
                meters += Flat(corners[i] - corners[i - 1]).magnitude;
            return meters;
        }

        private static Vector3 Flat(Vector3 direction) =>
            Vector3.ProjectOnPlane(direction, Vector3.up);
    }
}
