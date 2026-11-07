using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>복습 방문 동안만 담당 NPC의 근무지 모습을 유지하는 요청.</summary>
    public sealed class NpcReviewPresence : IDisposable
    {
        private static readonly Dictionary<string, int> requests = new Dictionary<string, int>();
        private readonly string npcCode;
        private bool disposed;

        public NpcReviewPresence(string npcCode)
        {
            if (string.IsNullOrWhiteSpace(npcCode))
                throw new ArgumentException("복습할 NPC 코드가 필요합니다.", nameof(npcCode));
            this.npcCode = npcCode;
            requests.TryGetValue(npcCode, out int count);
            requests[npcCode] = count + 1;
        }

        public static bool IsRequested(string npcCode) =>
            npcCode != null && requests.ContainsKey(npcCode);

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (!requests.TryGetValue(npcCode, out int count))
                return;
            if (count == 1)
                requests.Remove(npcCode);
            else
                requests[npcCode] = count - 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared() => requests.Clear();
    }
}
