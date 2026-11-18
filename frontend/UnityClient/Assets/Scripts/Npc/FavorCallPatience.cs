namespace A305.Npc
{
    /// <summary>
    /// 말을 걸러 온 NPC가 플레이어를 기다리는 시간. 플레이어가 다가갈 수 있는 거리 밖으로 가거나 제안을 무시하고 가면 그 자리에서 기다리고,
    /// <see cref="WaitSeconds"/> 안에 돌아오지 않으면 말 걸기를 해지한다. 돌아오면 처음부터 다시 센다.
    /// </summary>
    public sealed class FavorCallPatience
    {
        /// <summary>플레이어가 멀어진 뒤 다시 오기를 기다리는 시간(초)</summary>
        public const double WaitSeconds = 30;

        private double? leftAt;

        /// <param name="nowSeconds">지금 실제 시각(초)</param>
        /// <param name="isPlayerNear">플레이어가 다가갈 수 있는 거리 안에 있는지</param>
        /// <returns>기다리다 그만둘 때가 되었으면 true</returns>
        public bool GivesUp(double nowSeconds, bool isPlayerNear)
        {
            if (isPlayerNear)
            {
                leftAt = null;
                return false;
            }

            leftAt ??= nowSeconds;
            return nowSeconds - leftAt.Value >= WaitSeconds;
        }

        public void Reset() => leftAt = null;
    }
}
