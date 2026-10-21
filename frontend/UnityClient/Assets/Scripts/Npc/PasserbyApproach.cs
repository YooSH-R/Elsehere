namespace A305.Npc
{
    /// <summary>
    /// 먼저 말을 거는 행인의 상태를 시간·거리로 바꾼다. 플레이어가 가까이 오면 다가가 대화를 제안하고,
    /// 거절·무시·대화가 끝나면 정해진 시간 동안 쉰 뒤 다시 다가올 수 있다.
    /// 쉬는 시간은 절대 시각으로 다뤄 다른 공간에 다녀와도 이어진다.
    /// </summary>
    public sealed class PasserbyApproach
    {
        private readonly PasserbyRules rules;

        /// <param name="restUntilSeconds">이전에 거절·무시해 쉬는 중이면 다시 다가올 수 있는 시각</param>
        public PasserbyApproach(PasserbyRules rules, double restUntilSeconds = 0d)
        {
            this.rules = rules;
            RestUntilSeconds = restUntilSeconds;
            Phase = restUntilSeconds > 0d ? PasserbyPhase.Resting : PasserbyPhase.Waiting;
        }

        public PasserbyPhase Phase { get; private set; }

        /// <summary>쉬는 중이면 다시 다가올 수 있는 시각</summary>
        public double RestUntilSeconds { get; private set; }

        public bool CanDecline => rules.CanDecline;

        /// <param name="isPlayerBusy">플레이어가 다른 대화나 다른 행인의 제안 중이어서 끼어들면 안 되는지</param>
        public void Tick(double nowSeconds, float distanceToPlayer, bool isPlayerBusy)
        {
            switch (Phase)
            {
                case PasserbyPhase.Resting:
                    if (nowSeconds >= RestUntilSeconds)
                    {
                        Phase = PasserbyPhase.Waiting;
                    }

                    break;
                case PasserbyPhase.Waiting:
                    if (!isPlayerBusy && distanceToPlayer <= rules.DetectRadiusMeters)
                    {
                        Phase = PasserbyPhase.Approaching;
                    }

                    break;
                case PasserbyPhase.Approaching:
                    if (isPlayerBusy)
                    {
                        Phase = PasserbyPhase.Waiting;
                    }
                    else if (distanceToPlayer > rules.DetectRadiusMeters)
                    {
                        // 다가가는 사이 플레이어가 멀리 가 버리면 무시한 것으로 본다.
                        Rest(nowSeconds);
                    }
                    else if (distanceToPlayer <= rules.OfferDistanceMeters)
                    {
                        Phase = PasserbyPhase.Offering;
                    }

                    break;
                case PasserbyPhase.Offering:
                    if (distanceToPlayer > rules.DismissDistanceMeters)
                    {
                        Rest(nowSeconds);
                    }

                    break;
            }
        }

        /// <returns>제안 중이었으면 true이고 대화를 시작한다.</returns>
        public bool TryAccept()
        {
            if (Phase != PasserbyPhase.Offering)
            {
                return false;
            }

            Phase = PasserbyPhase.Talking;
            return true;
        }

        /// <returns>제안 중이고 거절할 수 있는 행인이면 true이고 쉬기 시작한다.</returns>
        public bool TryDecline(double nowSeconds)
        {
            if (Phase != PasserbyPhase.Offering || !rules.CanDecline)
            {
                return false;
            }

            Rest(nowSeconds);
            return true;
        }

        public void EndTalk(double nowSeconds)
        {
            if (Phase == PasserbyPhase.Talking)
            {
                Rest(nowSeconds);
            }
        }

        private void Rest(double nowSeconds)
        {
            Phase = PasserbyPhase.Resting;
            RestUntilSeconds = nowSeconds + rules.RestSeconds;
        }
    }
}
