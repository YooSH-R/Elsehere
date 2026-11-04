using System.Collections.Generic;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 앉기 제안을 띄울 자리를 고른다. 소파·벤치처럼 한 사물에 자리가 붙어 있어도 플레이어에게 가장 가까운 자리 하나만 고른다.
    /// </summary>
    public static class RestSeatPicker
    {
        /// <returns>닿는 거리 안에서 가장 가까운 자리의 순번. 없으면 -1이다.</returns>
        public static int Nearest(
            Vector3 playerPosition,
            IReadOnlyList<Vector3> seatPositions,
            float reachMeters
        )
        {
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < seatPositions.Count; index++)
            {
                float distance = Vector3.Distance(playerPosition, seatPositions[index]);
                if (distance <= reachMeters && distance < nearestDistance)
                {
                    nearest = index;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
