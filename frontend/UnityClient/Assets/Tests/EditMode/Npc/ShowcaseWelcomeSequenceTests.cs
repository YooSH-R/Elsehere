using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>
    /// 시연 햄버거 가게 입장 순서. 장면 전환 화면이 걷히고 조금 뒤에 존박의 랩을 시작하고,
    /// 입장부터 존박의 대화 제안에 답할 때까지 장소 생활소음을 끈다.
    /// </summary>
    public class ShowcaseWelcomeSequenceTests
    {
        // 장면 전환이 늦어질 수 있어, 화면이 보이고 1.5초가 지나야 랩을 시작한다.
        [Test]
        public void RapStartsOnlyAfterTheScreenIsShownForTheDelay()
        {
            var sequence = new ShowcaseWelcomeSequence();

            Assert.That(sequence.TickRap(5f, screenCovered: true), Is.False, "전환 화면 동안");
            Assert.That(sequence.TickRap(1f, screenCovered: false), Is.False, "화면이 보이고 1초");
            Assert.That(sequence.RapStarted, Is.False);
            Assert.That(
                sequence.TickRap(.6f, screenCovered: false),
                Is.True,
                "1.5초가 지나면 시작"
            );
            Assert.That(sequence.RapStarted, Is.True);
            Assert.That(sequence.TickRap(1f, screenCovered: false), Is.False, "한 번만 시작");
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
