using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>
    /// 시연 퀘스트가 순서를 지키려고 일부러 막아 둔 대화와 택시(NPC 키 → 화면에 보일 까닭). 막은 NPC나 택시를 누르면 대화 대신 까닭을 보인다.
    /// 퀘스트 쪽이 진행 상태를 읽을 때마다 정하고, 장면을 옮겨도 남는다(<see cref="NpcDuty.MissionNpcCode"/>와 같다).
    /// 시연 거리의 배경 주민은 퀘스트가 정하지 않아도 늘 막혀 있다(<see cref="PasserbyOfferText.ShowcaseBackgroundResident"/>).
    /// </summary>
    public static class ShowcaseTalkLock
    {
        private static readonly Dictionary<string, string> reasons =
            new Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => reasons.Clear();

        public static void Set(IReadOnlyDictionary<string, string> locked)
        {
            reasons.Clear();
            foreach (KeyValuePair<string, string> entry in locked)
                reasons[entry.Key] = entry.Value;
        }

        public static void Clear() => reasons.Clear();

        /// <summary>막아 둔 까닭. 막지 않았으면 null</summary>
        public static string ReasonFor(string npcCode) =>
            npcCode != null && reasons.TryGetValue(npcCode, out string reason) ? reason : null;
    }
}
