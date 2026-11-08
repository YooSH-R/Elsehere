using UnityEngine;

namespace A305.Npc
{
    /// <summary>이동하려는데 제자리인 시간만 센다. 신호·대화처럼 의도한 대기는 포함하지 않는다.</summary>
    public sealed class NpcStuckRecovery
    {
        public void Reset() { }

        public bool ShouldRecover(
            Vector3 position,
            Vector3 destination,
            bool tryingToMove,
            bool intentionallyWaiting,
            float elapsedSeconds,
            float timeoutSeconds
        ) => false;
    }
}
