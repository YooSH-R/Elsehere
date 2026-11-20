using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 정해진 순서대로만 하는 퀘스트(비 오는 저녁)를 하는 동안의 대화 막기. 지금 단계에 나오는 사람만 말을 걸 수 있고, 나머지 NPC와 지나가는
    /// 택시는 누르면 대화 대신 지금 차례를 까닭으로 보인다. 퀘스트 쪽이 진행 상태를 읽을 때마다 정하고, 장면을 옮겨도 남는다
    /// (<see cref="NpcDuty.MissionNpcCode"/>와 같다).
    /// </summary>
    public static class ShowcaseTalkLock
    {
        private static readonly HashSet<string> allowed = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();

        /// <param name="reason">막은 NPC를 누르면 보일 까닭(지금 차례)</param>
        /// <param name="allowedNpcs">지금 단계에 말을 걸 수 있는 NPC 키</param>
        public static void Set(string reason, IEnumerable<string> allowedNpcs)
        {
            Reason = reason;
            allowed.Clear();
            foreach (string code in allowedNpcs)
                allowed.Add(code);
        }

        public static void Clear()
        {
            Reason = null;
            allowed.Clear();
        }

        /// <summary>순서대로만 하는 퀘스트를 하는 중인지</summary>
        public static bool IsActive => Reason != null;

        /// <summary>지금 차례. 막지 않으면 null</summary>
        public static string Reason { get; private set; }

        /// <summary>이 NPC를 막은 까닭. 막지 않았거나 지금 단계에 나오는 사람이면 null</summary>
        public static string ReasonFor(string npcCode) =>
            Reason != null && !allowed.Contains(npcCode) ? Reason : null;
    }
}
