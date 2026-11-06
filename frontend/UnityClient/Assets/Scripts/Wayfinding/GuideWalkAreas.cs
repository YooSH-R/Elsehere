using UnityEngine.AI;

namespace A305.Wayfinding
{
    /// <summary>
    /// 마을 바닥(NavMesh)을 구울 때 차도는 Road, 횡단보도는 Crosswalk 구역으로 나눈다. 두 구역의 기본 비용은 보통 바닥과 같아
    /// NPC는 차도 구역을 제외하고, 길 안내는 <see cref="Filter"/>로 차도에 큰 비용을 매겨 횡단보도로 돌아가게 한다.
    /// </summary>
    public static class GuideWalkAreas
    {
        public const string Road = "Road";
        public const string Crosswalk = "Crosswalk";

        // NPC가 걸을 바닥과 같은 사람 크기 요원(Humanoid)으로 구운 바닥을 쓴다.
        private const int HumanoidAgentTypeId = 0;

        /// <param name="roadCost">차도 1m를 인도 몇 m로 칠지</param>
        public static NavMeshQueryFilter Filter(float roadCost)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = HumanoidAgentTypeId,
                areaMask = NavMesh.AllAreas,
            };
            int road = NavMesh.GetAreaFromName(Road);
            if (road >= 0)
                filter.SetAreaCost(road, roadCost);
            return filter;
        }
    }
}
