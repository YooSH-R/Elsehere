using UnityEngine;

namespace A305.Npc
{
    /// <summary>이동하려는데 제자리인 시간만 센다. 신호·대화처럼 의도한 대기는 포함하지 않는다.</summary>
    public sealed class NpcStuckRecovery
    {
        private Vector3 anchor;
        private Vector3 goal;
        private float blockedSeconds;
        private bool observing;

        public void Reset()
        {
            observing = false;
            blockedSeconds = 0;
        }

        public bool ShouldRecover(
            Vector3 position,
            Vector3 destination,
            bool tryingToMove,
            bool intentionallyWaiting,
            float elapsedSeconds,
            float timeoutSeconds
        )
        {
            if (
                !tryingToMove
                || intentionallyWaiting
                || elapsedSeconds <= 0
                || Vector3.ProjectOnPlane(destination - position, Vector3.up).magnitude < .4f
            )
            {
                Reset();
                return false;
            }
            if (
                !observing
                || Vector3.ProjectOnPlane(goal - destination, Vector3.up).magnitude > 1
                || Vector3.ProjectOnPlane(anchor - position, Vector3.up).magnitude > .15f
            )
            {
                anchor = position;
                goal = destination;
                blockedSeconds = 0;
                observing = true;
            }
            blockedSeconds += elapsedSeconds;
            if (blockedSeconds < timeoutSeconds)
                return false;
            Reset();
            return true;
        }
    }
}
