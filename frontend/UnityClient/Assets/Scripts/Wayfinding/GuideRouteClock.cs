using UnityEngine;

namespace A305.Wayfinding
{
    /// <summary>
    /// 도로 위 길을 언제 다시 찾고 얼마나 깔아 보일지 정한다. 플레이어가 움직이거나 시간이 지나면 길을 다시 찾되 깔린 길은 그대로 두고,
    /// 목적지가 바뀌거나 숨겼던 길을 다시 보일 때만 플레이어 발밑에서 출입문까지 새로 깐다.
    /// </summary>
    public sealed class GuideRouteClock
    {
        private readonly float replanMeters;
        private readonly float replanSeconds;
        private readonly float revealSeconds;

        private bool hasPath;
        private string placeCode;
        private Vector3 plannedFrom;
        private float plannedAt;
        private float revealStartedAt;

        /// <param name="replanMeters">길을 찾은 자리에서 이만큼 움직이면 다시 찾는다.</param>
        /// <param name="replanSeconds">길을 찾은 뒤 이만큼 지나면 다시 찾는다.</param>
        /// <param name="revealSeconds">새 길을 처음부터 끝까지 까는 데 걸리는 시간</param>
        public GuideRouteClock(float replanMeters, float replanSeconds, float revealSeconds)
        {
            this.replanMeters = replanMeters;
            this.replanSeconds = replanSeconds;
            this.revealSeconds = revealSeconds;
        }

        public bool NeedsPath(string placeCode, Vector3 player, float now) =>
            !hasPath
            || placeCode != this.placeCode
            || Vector3.Distance(player, plannedFrom) >= replanMeters
            || now - plannedAt >= replanSeconds;

        public void PathFound(string placeCode, Vector3 player, float now)
        {
            if (!hasPath || placeCode != this.placeCode)
                revealStartedAt = now;
            hasPath = true;
            this.placeCode = placeCode;
            plannedFrom = player;
            plannedAt = now;
        }

        /// <summary>길을 거뒀다. 다시 보일 때는 새로 찾아 처음부터 깐다.</summary>
        public void Hide()
        {
            hasPath = false;
            placeCode = null;
        }

        /// <summary>길 전체 중 지금까지 깔린 비율(0~1)</summary>
        public float Revealed(float now) =>
            revealSeconds > 0f ? Mathf.Clamp01((now - revealStartedAt) / revealSeconds) : 1f;
    }
}
