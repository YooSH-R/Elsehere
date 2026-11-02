using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// NPC도 앉을 수 있는 자리. 플레이어가 앉는 의자·벤치·소파 자리(Mission의 RestSeat)가 구현한다.
    /// 한 자리에는 한 명만 앉으며, NPC가 앉은 자리에는 플레이어에게 앉기를 권하지 않는다.
    /// </summary>
    public interface INpcSeat
    {
        /// <summary>앉을 자리. 원점은 바닥, 앞(+z)은 앉아서 보는 방향이다.</summary>
        Transform SeatPoint { get; }

        /// <summary>자리 원점에서 쿠션 윗면까지의 높이</summary>
        float CushionTopMeters { get; }

        /// <summary>자리 원점에서 앉는 면 앞 모서리까지의 거리</summary>
        float FrontEdgeMeters { get; }

        /// <summary>플레이어나 NPC가 앉아 있는지</summary>
        bool IsTaken { get; }

        /// <summary>비어 있으면 NPC가 앉는다.</summary>
        bool TryTake(Object npc);

        /// <summary>그 NPC가 일어난다.</summary>
        void Release(Object npc);
    }
}
