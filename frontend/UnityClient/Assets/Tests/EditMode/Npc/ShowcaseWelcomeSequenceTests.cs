using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 시연 햄버거 가게 입장 순서. 입장하고 조금 뒤에 존박의 랩을 시작하고,
    /// 입장부터 존박의 대화 제안에 답할 때까지 장소 생활소음을 끈다.
    /// </summary>
    public class ShowcaseWelcomeSequenceTests
    {
        // 1.5초는 장면 전환 화면이 늦게 걷히는 것을 감안한 지연이다. 전환 화면을 따로 기다리지 않고 입장하고 1.5초 뒤에 시작한다.
        [Test]
        public void RapStartsTheDelayAfterEntry()
        {
            var sequence = new ShowcaseWelcomeSequence();

            Assert.That(sequence.TickRap(1f), Is.False, "입장하고 1초");
            Assert.That(sequence.RapStarted, Is.False);
            Assert.That(sequence.TickRap(.6f), Is.True, "1.5초가 지나면 시작");
            Assert.That(sequence.RapStarted, Is.True);
            Assert.That(sequence.TickRap(1f), Is.False, "한 번만 시작");
        }

        // 랩부터 제안에 답할 때까지 생활소음을 끄고, 거절하면 다시 낸다.
        [Test]
        public void PlaceStaysHushedUntilTheOfferIsDeclined()
        {
            var sequence = new ShowcaseWelcomeSequence();
            Assert.That(sequence.HushesPlace, Is.True, "입장과 랩");

            sequence.SeeOffer(PasserbyPhase.Waiting);
            sequence.SeeOffer(PasserbyPhase.Approaching);
            sequence.SeeOffer(PasserbyPhase.Offering);
            Assert.That(sequence.HushesPlace, Is.True, "다가와 제안하는 동안");

            sequence.SeeOffer(PasserbyPhase.Resting);
            Assert.That(sequence.HushesPlace, Is.False, "거절하면 다시 들린다");
        }

        // 제안을 받아 대화를 시작하면 생활소음은 평소 대화처럼 낮춰 다시 낸다.
        [Test]
        public void AcceptingTheOfferEndsTheHush()
        {
            var sequence = new ShowcaseWelcomeSequence();

            sequence.SeeOffer(PasserbyPhase.Talking);

            Assert.That(sequence.HushesPlace, Is.False);
        }
    }
}
