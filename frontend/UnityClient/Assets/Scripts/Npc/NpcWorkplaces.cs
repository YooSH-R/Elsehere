using System.Collections.Generic;
using A305.World;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// NPC가 일하는 장소 장면. 서버는 마을 매장 직원을 마을(TOWN)에 두지만 매장은 따로 장면이므로,
    /// 복습·장소 연습으로 직원을 찾아갈 때 생활 데이터의 일터로 매장 장면을 찾는다.
    /// 생활 데이터는 여가 목적지 카탈로그(Resources/NpcOutings)가 가진 것을 쓴다.
    /// </summary>
    public static class NpcWorkplaces
    {
        private static Dictionary<string, string> workplacesByNpc;

        /// <returns>일터의 장면 이름. 모르는 NPC면 null</returns>
        public static string SceneOf(string npcCode)
        {
            if (npcCode == null)
                return null;
            if (workplacesByNpc == null)
                workplacesByNpc = Load();
            return workplacesByNpc.TryGetValue(npcCode, out string workplace)
                ? PlaceScenes.SceneOf(workplace)
                : null;
        }

        private static Dictionary<string, string> Load()
        {
            var workplaces = new Dictionary<string, string>();
            var catalog = Resources.Load<NpcOutingCatalog>("NpcOutings");
            if (catalog == null || catalog.LifeData == null)
                return workplaces;
            foreach (NpcLifeEntry entry in NpcLifeData.Parse(catalog.LifeData.text).Npcs)
                workplaces[entry.NpcId] = entry.Workplace;
            return workplaces;
        }
    }
}
