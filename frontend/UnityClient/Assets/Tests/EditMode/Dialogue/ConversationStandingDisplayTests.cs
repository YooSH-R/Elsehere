using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class ConversationStandingDisplayTests
    {
        private const string Heart = "<color=#FF6B81>♥</color>";

        [Test]
        public void WithoutServerStanding_ShowsNothing()
        {
            // 호감도를 보내지 않는 서버나 대본 대화는 칸을 숨긴다.
            var display = new ConversationStandingDisplay();

            Assert.That(display.Text(new ConversationStanding(), 0f), Is.Null);
            Assert.That(display.Text(null, 0f), Is.Null);
        }

        [Test]
        public void Widest_IsTheLineWithTheBiggestDropAndTheLongerRudeNotice()
        {
            // 이름 줄의 욕구 게이지는 이 폭 뒤에 자리를 잡아, 무례 경고 문구가 붙었다 사라져도 움직이지 않는다.
            Assert.That(
                ConversationStandingDisplay.Widest(3),
                Is.EqualTo(
                    $"{Heart} -100 <color=#E05252>-9</color>   ●○○   <size=80%>무례한 말 1/3 · 3번이면 대화가 끝나요</size>"
                )
            );
        }

        [Test]
        public void AtStart_ShowsAffinityAndEmptyStrikes()
        {
            var display = new ConversationStandingDisplay();

            string text = display.Text(Standing(affinity: 12, rudeCount: 0), 0f);

            Assert.That(text, Is.EqualTo($"{Heart} 12   ○○○"));
        }

        [Test]
        public void RightAfterRudeTurn_ShowsChangeFilledStrikeAndNotice()
        {
            var display = new ConversationStandingDisplay();
            ConversationStanding standing = Standing(affinity: 12, rudeCount: 0);
            display.Text(standing, 0f);

            standing.Apply(State(affinity: 6, rudeCount: 1));
            string text = display.Text(standing, 10f);

            Assert.That(
                text,
                Is.EqualTo(
                    $"{Heart} 6 <color=#E05252>-6</color>   ●○○   "
                        + "<size=80%>무례한 말 1/3 · 3번이면 대화가 끝나요</size>"
                )
            );
        }

        [Test]
        public void AfterFlashTime_KeepsStrikeButHidesChangeAndNotice()
        {
            var display = new ConversationStandingDisplay();
            ConversationStanding standing = Standing(affinity: 12, rudeCount: 0);
            display.Text(standing, 0f);
            standing.Apply(State(affinity: 6, rudeCount: 1));
            display.Text(standing, 10f);

            string text = display.Text(
                standing,
                10f + ConversationStandingDisplay.FlashSeconds + 0.1f
            );

            Assert.That(text, Is.EqualTo($"{Heart} 6   ●○○"));
        }

        [Test]
        public void AffinityRises_ShowsPlusChangeWithoutNotice()
        {
            var display = new ConversationStandingDisplay();
            ConversationStanding standing = Standing(affinity: 12, rudeCount: 1);
            display.Text(standing, 0f);

            standing.Apply(State(affinity: 15, rudeCount: 1));
            string text = display.Text(standing, 5f);

            Assert.That(text, Is.EqualTo($"{Heart} 15 <color=#5FAF6B>+3</color>   ●○○"));
        }

        [Test]
        public void ThirdRudeTurn_FillsAllStrikesAndSaysConversationEnded()
        {
            var display = new ConversationStandingDisplay();
            ConversationStanding standing = Standing(affinity: 0, rudeCount: 2);
            display.Text(standing, 0f);

            standing.Apply(State(affinity: -6, rudeCount: 3));
            string text = display.Text(standing, 5f);

            Assert.That(
                text,
                Is.EqualTo(
                    $"{Heart} -6 <color=#E05252>-6</color>   ●●●   "
                        + "<size=80%>무례한 말 3/3 · 대화가 끝났어요</size>"
                )
            );
        }

        private static ConversationStanding Standing(int affinity, int rudeCount)
        {
            var standing = new ConversationStanding();
            standing.Apply(State(affinity, rudeCount));
            return standing;
        }

        private static ConversationState State(int affinity, int rudeCount)
        {
            return new ConversationState
            {
                Status = "ACTIVE",
                Affinity = affinity,
                RudeCount = rudeCount,
                RudeLimit = 3,
            };
        }
    }
}
