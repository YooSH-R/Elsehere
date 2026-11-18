using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 말을 걸러 온 NPC는 플레이어가 다가갈 수 있는 거리(행인 6m, 직원은 부르는 거리) 밖으로 가거나 제안을 무시하고 가면 그 자리에서 기다린다.
    /// 30초 안에 플레이어가 돌아오면 다시 말을 걸고, 돌아오지 않으면 말 걸기를 해지한다.
    /// </summary>
    public class FavorCallPatienceTests
    {
        private static readonly PasserbyRules Walker = new PasserbyRules(
            detectRadiusMeters: 6f,
            offerDistanceMeters: 2f,
            dismissDistanceMeters: 6f,
            restSeconds: 60f,
            canDecline: true,
            followUpCallDistanceMeters: 6f
        );

        [Test]
        public void WhilePlayerIsNearItKeepsWaiting()
        {
            var patience = new FavorCallPatience();

            Assert.That(patience.GivesUp(0d, isPlayerNear: true), Is.False);
            Assert.That(patience.GivesUp(100d, isPlayerNear: true), Is.False);
        }

        [Test]
        public void ItGivesUpThirtySecondsAfterThePlayerLeaves()
        {
            var patience = new FavorCallPatience();

            Assert.That(patience.GivesUp(10d, isPlayerNear: false), Is.False);
            Assert.That(patience.GivesUp(39d, isPlayerNear: false), Is.False);
            Assert.That(patience.GivesUp(40d, isPlayerNear: false), Is.True);
        }

        [Test]
        public void ComingBackStartsTheWaitOver()
        {
            var patience = new FavorCallPatience();
            patience.GivesUp(0d, isPlayerNear: false);
            patience.GivesUp(25d, isPlayerNear: true);

            Assert.That(patience.GivesUp(50d, isPlayerNear: false), Is.False);
            Assert.That(patience.GivesUp(80d, isPlayerNear: false), Is.True);
        }

        [Test]
        public void AnIgnoredFavorCallCanApproachAgainWithoutResting()
        {
            var walker = new PasserbyApproach(Walker);
            walker.Tick(0d, distanceToPlayer: 5f, isPlayerBusy: false);
            walker.Tick(1d, distanceToPlayer: 1.5f, isPlayerBusy: false);
            Assert.That(walker.Phase, Is.EqualTo(PasserbyPhase.Offering));
            // 플레이어가 제안을 무시하고 멀어지면 평소에는 쉬는 시간(60초) 동안 다시 오지 않는다.
            walker.Tick(2d, distanceToPlayer: 7f, isPlayerBusy: false);
            Assert.That(walker.Phase, Is.EqualTo(PasserbyPhase.Resting));

            walker.ResumeAfterIgnoredFavorCall(2d);
            walker.Tick(10d, distanceToPlayer: 5f, isPlayerBusy: false);

            Assert.That(walker.Phase, Is.EqualTo(PasserbyPhase.Approaching));
        }
    }
}
