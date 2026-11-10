using System;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// [이웃]에서 [지도에서 보기]로 고른 NPC. 지도는 그 NPC의 핀을, 이름표 층은 머리 위 표시를 이 값을 보고 띄운다.
    /// 한 번에 한 명만 찾고, 다른 이웃을 고르면 바뀌며, 플레이어가 그 NPC 3m 안에 오면 찾기를 끝낸다.
    /// 장면을 옮겨도 남도록 정적으로 둔다.
    /// </summary>
    public sealed class NpcTracking
    {
        /// <summary>NPC에게 이만큼 다가오면 찾았다고 보고 핀과 머리 위 표시를 거둔다.</summary>
        public const float ArrivalMeters = 3f;

        public static NpcTracking Shared { get; private set; } = new NpcTracking();

        /// <summary>찾는 NPC 키. 찾는 NPC가 없으면 null</summary>
        public string Code { get; private set; }

        /// <summary>찾는 NPC 이름(지도 핀 글자)</summary>
        public string Name { get; private set; }

        /// <summary>찾는 NPC의 생활 데이터. 몸이 없을 때 지도 자리를 일과로 정한다. 없으면 null</summary>
        public NpcLifeEntry Life { get; private set; }

        /// <summary>찾는 NPC가 바뀌었다(고르거나 찾기를 끝냄).</summary>
        public event Action Changed;

        public bool IsTracking(string npcCode) => Code != null && Code == npcCode;

        public void Track(string npcCode, string name, NpcLifeEntry life)
        {
            Code = npcCode;
            Name = name;
            Life = life;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (Code == null)
                return;
            Code = null;
            Name = null;
            Life = null;
            Changed?.Invoke();
        }

        public static bool HasArrived(float meters) => meters <= ArrivalMeters;

        /// <summary>지금 장면에 켜진 그 NPC의 몸. 없으면 null</summary>
        public static NpcIdentity FindBody(string npcCode)
        {
            if (npcCode == null)
                return null;
            foreach (NpcIdentity npc in NpcIdentity.Active)
            {
                if (npc.Code == npcCode)
                    return npc;
            }

            return null;
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 지난번에 찾던 NPC를 지운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared() => Shared = new NpcTracking();
    }
}
