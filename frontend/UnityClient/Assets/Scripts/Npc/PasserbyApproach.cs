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

        /// <summary>준비해 둔 것을 건네려고 제자리에서 불러 제안하거나 그 대화를 하는 중인지(<see cref="TryCall"/>)</summary>
        public bool IsCalling { get; private set; }

        // 부른 것을 거절·무시했거나 불러서 대화한 뒤 다시 부를 수 있는 시각
        private double callRestUntilSeconds;

        /// <summary>
        /// 준비해 둔 것이 다 되어 제자리에서 부른다. 기다리거나 쉬거나 다가가는 중이고 부르는 거리 안이면 더 걷지 않고 바로 대화를 제안한다.
        /// 다른 대화 뒤에 쉬는 시간은 기다리지 않지만, 부른 것을 거절·무시했거나 불러서 대화했으면 쉬는 시간이 지나야 다시 부른다.
        /// </summary>
        /// <param name="isPlayerBusy">다른 대화·제안 중이거나 새 대화를 시작할 수 없어 부르면 안 되는지</param>
        /// <returns>불렀으면 true이고 제안 중이 된다.</returns>
        public bool TryCall(double nowSeconds, float distanceToPlayer, bool isPlayerBusy)
        {
            // 걷는 속도가 0인 카운터 직원은 감지 거리 안, 제안 거리 밖에서 다가가는 상태로 서 있으므로 그때도 부른다.
            bool canCall =
                Phase == PasserbyPhase.Waiting
                || Phase == PasserbyPhase.Resting
                || Phase == PasserbyPhase.Approaching;
            if (
                !canCall
                || isPlayerBusy
                || distanceToPlayer > rules.FollowUpCallDistanceMeters
                || nowSeconds < callRestUntilSeconds
            )
            {
                return false;
            }

            Phase = PasserbyPhase.Offering;
            IsCalling = true;
            return true;
        }

        /// <param name="isPlayerBusy">플레이어가 다른 대화나 다른 행인의 제안 중이어서 끼어들면 안 되는지</param>
        /// <param name="isPlayerWaiting">튜토리얼처럼 플레이어가 이 행인을 기다리도록 붙잡혀 있는지. 멀리 있어도 다가가고 그만두지 않는다.</param>
        public void Tick(
            double nowSeconds,
            float distanceToPlayer,
            bool isPlayerBusy,
            bool isPlayerWaiting = false
        )
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
                    if (
                        !isPlayerBusy
                        && (isPlayerWaiting || distanceToPlayer <= rules.DetectRadiusMeters)
                    )
                    {
                        Phase = PasserbyPhase.Approaching;
                    }

                    break;
                case PasserbyPhase.Approaching:
                    if (isPlayerBusy)
                    {
                        Phase = PasserbyPhase.Waiting;
                    }
                    else if (!isPlayerWaiting && distanceToPlayer > rules.DetectRadiusMeters)
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
                    // 부르는 중에는 부르는 거리 안에 있는 동안 닫지 않는다. 직원은 무시 거리가 부르는 거리보다 짧다.
                    float dismissDistance = IsCalling
                        ? System.Math.Max(
                            rules.DismissDistanceMeters,
                            rules.FollowUpCallDistanceMeters
                        )
                        : rules.DismissDistanceMeters;
                    if (distanceToPlayer > dismissDistance)
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

        /// <summary>
        /// 플레이어가 먼저 말을 걸 수 있는지(기능 명세 F-22 [E 대화하기]). 기다리거나 쉬는 중이고 제안 거리 안에 있어야 한다.
        /// 다가오거나 제안하는 중에는 제안 화면의 [대화하기]를 쓴다.
        /// </summary>
        public bool CanPlayerTalk(float distanceToPlayer)
        {
            bool isIdle = Phase == PasserbyPhase.Waiting || Phase == PasserbyPhase.Resting;
            return isIdle && distanceToPlayer <= rules.OfferDistanceMeters;
        }

        /// <returns>말을 걸 수 있었으면 true이고 대화를 시작한다. 대화가 끝나면 다시 쉰다.</returns>
        public bool TryTalkByPlayer(float distanceToPlayer)
        {
            if (!CanPlayerTalk(distanceToPlayer))
            {
                return false;
            }

            Phase = PasserbyPhase.Talking;
            return true;
        }

        /// <summary>
        /// 플레이어가 NPC를 클릭·탭해 말을 걸 수 있는지(기능 명세 F-22). [말 걸기]보다 먼 클릭 거리 안이면 되고,
        /// 다가오거나 제안하는 중에도 걸 수 있다. 이미 대화 중이면 걸 수 없다.
        /// </summary>
        public bool CanTalkByClick(float distanceToPlayer, float reachMeters)
        {
            return Phase != PasserbyPhase.Talking && distanceToPlayer <= reachMeters;
        }

        /// <returns>말을 걸 수 있었으면 true이고 대화를 시작한다. 대화가 끝나면 다시 쉰다.</returns>
        public bool TryTalkByClick(float distanceToPlayer, float reachMeters)
        {
            if (!CanTalkByClick(distanceToPlayer, reachMeters))
            {
                return false;
            }

            Phase = PasserbyPhase.Talking;
            return true;
        }

        public void EndTalk(double nowSeconds)
        {
            if (Phase == PasserbyPhase.Talking)
            {
                Rest(nowSeconds);
            }
        }

        /// <summary>
        /// 말 걸기 부탁을 들고 온 NPC의 제안을 플레이어가 무시하고 멀어졌다. 쉬는 시간 없이 바로 기다림으로 돌아가, 플레이어가 다시 다가갈 수 있는
        /// 거리 안에 오면 또 다가가거나 부른다. 언제 그만둘지는 <see cref="FavorCallPatience"/>가 정한다.
        /// </summary>
        public void ResumeAfterIgnoredFavorCall(double nowSeconds)
        {
            Phase = PasserbyPhase.Waiting;
            RestUntilSeconds = nowSeconds;
            IsCalling = false;
            callRestUntilSeconds = nowSeconds;
        }

        private void Rest(double nowSeconds)
        {
            Phase = PasserbyPhase.Resting;
            RestUntilSeconds = nowSeconds + rules.RestSeconds;
            if (IsCalling)
            {
                // 건네지 못해 아직 남아 있어도 바로 다시 부르지 않는다.
                IsCalling = false;
                callRestUntilSeconds = RestUntilSeconds;
            }
        }
    }
}
