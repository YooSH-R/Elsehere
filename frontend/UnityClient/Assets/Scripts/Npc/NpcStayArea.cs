using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 생활하며 고르는 사물·자리를 한 구역(자기 건물 1층 실내 등)으로 제한한다.
    /// 높이도 보므로 같은 건물 2층의 다른 가게 사물은 고르지 않는다.
    /// </summary>
    public static class NpcStayArea
    {
        /// <param name="limited">구역 제한을 켰는지. 끄면 어디든 고른다.</param>
        public static bool Allows(bool limited, Bounds area, Vector3 point)
        {
            return !limited || area.Contains(point);
        }
    }
}
