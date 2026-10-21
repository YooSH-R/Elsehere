using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class PasserbyApproachTests
    {
        private const float DetectRadius = 10f;
        private const float OfferDistance = 2f;
        private const float DismissDistance = 6f;
        private const float RestSeconds = 60f;

        private static readonly PasserbyRules TownPasserby = new PasserbyRules(
            DetectRadius,
            OfferDistance,
            DismissDistance,
            RestSeconds,
            canDecline: true
        );

        private static readonly PasserbyRules TutorialPasserby = new PasserbyRules(
            DetectRadius,
            OfferDistance,
            DismissDistance,
            RestSeconds,
            canDecline: false
        );

        [Test]
        public void NewPasserby_WaitsInPlace()
        {
            var passerby = new PasserbyApproach(TownPasserby);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void Tick_PlayerWithinDetectRadius_StartsApproaching()
        {
            var passerby = new PasserbyApproach(TownPasserby);

            passerby.Tick(0d, distanceToPlayer: 9f, isPlayerBusy: false);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Approaching));
        }

        [Test]
        public void Tick_PlayerFarAway_KeepsWaiting()
        {
            var passerby = new PasserbyApproach(TownPasserby);

            passerby.Tick(0d, distanceToPlayer: 11f, isPlayerBusy: false);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void Tick_PlayerBusy_DoesNotApproach()
        {
            // 다른 NPC와 대화 중이거나 다른 행인이 제안 중이면 끼어들지 않는다.
            var passerby = new PasserbyApproach(TownPasserby);

            passerby.Tick(0d, distanceToPlayer: 5f, isPlayerBusy: true);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void Tick_PlayerBecomesBusyWhileApproaching_StopsApproaching()
        {
            var passerby = new PasserbyApproach(TownPasserby);
            passerby.Tick(0d, 5f, isPlayerBusy: false);

            passerby.Tick(0.1d, 5f, isPlayerBusy: true);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void Tick_ReachesOfferDistance_OffersConversation()
        {
            var passerby = new PasserbyApproach(TownPasserby);
            passerby.Tick(0d, 5f, isPlayerBusy: false);

            passerby.Tick(1d, 1.9f, isPlayerBusy: false);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        [Test]
        public void Tick_PlayerRunsAwayWhileApproaching_RestsAsIgnored()
        {
            var passerby = new PasserbyApproach(TownPasserby);
            passerby.Tick(0d, 9f, isPlayerBusy: false);

            passerby.Tick(2d, 11f, isPlayerBusy: false);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.RestUntilSeconds, Is.EqualTo(2d + RestSeconds));
        }

        [Test]
        public void Tick_PlayerWalksAwayWhileOffering_ClosesOfferAndRests()
        {
            // 제안을 무시하고 멀어지면 거절과 같게 처리한다. 카메라·이동은 잠그지 않는다.
            PasserbyApproach passerby = Offering(TownPasserby);

            passerby.Tick(10d, 6.5f, isPlayerBusy: false);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.RestUntilSeconds, Is.EqualTo(10d + RestSeconds));
        }

        [Test]
        public void TryDecline_TownPasserby_RestsThenApproachesAgainAfterRest()
        {
            PasserbyApproach passerby = Offering(TownPasserby);

            bool isDeclined = passerby.TryDecline(10d);
            passerby.Tick(10d + RestSeconds - 1d, 3f, isPlayerBusy: false);
            PasserbyPhase beforeRestEnds = passerby.Phase;
            passerby.Tick(10d + RestSeconds, 3f, isPlayerBusy: false);
            passerby.Tick(10d + RestSeconds + 0.1d, 3f, isPlayerBusy: false);

            Assert.That(isDeclined, Is.True);
            Assert.That(beforeRestEnds, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Approaching));
        }

        [Test]
        public void TryDecline_TutorialPasserby_IsRejectedAndKeepsOffering()
        {
            PasserbyApproach passerby = Offering(TutorialPasserby);

            bool isDeclined = passerby.TryDecline(10d);

            Assert.That(isDeclined, Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Offering));
        }

        [Test]
        public void TryAccept_WhileOffering_StartsTalking()
        {
            PasserbyApproach passerby = Offering(TownPasserby);

            bool isAccepted = passerby.TryAccept();

            Assert.That(isAccepted, Is.True);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Talking));
        }

        [Test]
        public void TryAccept_WhenNotOffering_IsRejected()
        {
            var passerby = new PasserbyApproach(TownPasserby);

            Assert.That(passerby.TryAccept(), Is.False);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Waiting));
        }

        [Test]
        public void Tick_WhileTalking_StaysTalkingEvenIfPlayerIsFar()
        {
            PasserbyApproach passerby = Offering(TownPasserby);
            passerby.TryAccept();

            passerby.Tick(20d, 20f, isPlayerBusy: true);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Talking));
        }

        [Test]
        public void EndTalk_RestsBeforeApproachingAgain()
        {
            PasserbyApproach passerby = Offering(TownPasserby);
            passerby.TryAccept();

            passerby.EndTalk(30d);

            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.RestUntilSeconds, Is.EqualTo(30d + RestSeconds));
        }

        [Test]
        public void NewPasserby_WithEarlierRestTime_RestsUntilThen()
        {
            // 거절한 뒤 다른 공간에 다녀와도 쉬는 시간은 이어진다.
            var passerby = new PasserbyApproach(TownPasserby, restUntilSeconds: 100d);

            passerby.Tick(50d, 3f, isPlayerBusy: false);
            PasserbyPhase whileResting = passerby.Phase;
            passerby.Tick(100d, 3f, isPlayerBusy: false);
            passerby.Tick(100.1d, 3f, isPlayerBusy: false);

            Assert.That(whileResting, Is.EqualTo(PasserbyPhase.Resting));
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Approaching));
        }

        private static PasserbyApproach Offering(PasserbyRules rules)
        {
            var passerby = new PasserbyApproach(rules);
            passerby.Tick(0d, 5f, isPlayerBusy: false);
            passerby.Tick(1d, 1.5f, isPlayerBusy: false);
            Assert.That(passerby.Phase, Is.EqualTo(PasserbyPhase.Offering), "준비: 제안 상태");
            return passerby;
        }
    }
}
