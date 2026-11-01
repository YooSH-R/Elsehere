using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class ConversationStandingTests
    {
        private ConversationStanding standing;

        [SetUp]
        public void CreateStanding()
        {
            standing = new ConversationStanding();
        }

        [Test]
        public void Apply_StartState_ShowsAffinityAndEmptyStrikes()
        {
            standing.Apply(State(rudeCount: 0, affinity: 12));

            Assert.That(standing.IsKnown, Is.True);
            Assert.That(standing.Affinity, Is.EqualTo(12));
            Assert.That(standing.RudeCount, Is.EqualTo(0));
            Assert.That(standing.RudeLimit, Is.EqualTo(3));
            Assert.That(standing.IsStrike(0), Is.False);
            // 처음 받은 값은 바뀐 것이 아니므로 변화량과 경고 문구를 띄우지 않는다.
            Assert.That(standing.AffinityChange, Is.EqualTo(0));
            Assert.That(standing.RudeNotice, Is.Null);
        }

        [Test]
        public void Apply_RudeTurn_ShowsDeductionStrikeAndNotice()
        {
            standing.Apply(State(rudeCount: 0, affinity: 12));

            standing.Apply(State(rudeCount: 1, affinity: 8));

            Assert.That(standing.Affinity, Is.EqualTo(8));
            Assert.That(standing.AffinityChange, Is.EqualTo(-4));
            Assert.That(standing.AffinityChangeText, Is.EqualTo("-4"));
            Assert.That(standing.IsStrike(0), Is.True);
            Assert.That(standing.IsStrike(1), Is.False);
            Assert.That(standing.RudeNotice, Is.EqualTo("무례한 말 1/3 · 3번이면 대화가 끝나요"));
        }

        [Test]
        public void Apply_PoliteTurnAfterRude_KeepsStrikeButClearsChangeAndNotice()
        {
            standing.Apply(State(rudeCount: 0, affinity: 12));
            standing.Apply(State(rudeCount: 1, affinity: 8));

            standing.Apply(State(rudeCount: 1, affinity: 8));

            Assert.That(standing.IsStrike(0), Is.True);
            Assert.That(standing.AffinityChange, Is.EqualTo(0));
            Assert.That(standing.RudeNotice, Is.Null);
        }

        [Test]
        public void Apply_RudeLimitReached_FillsAllStrikesAndSaysConversationEnded()
        {
            standing.Apply(State(rudeCount: 2, affinity: 4));

            standing.Apply(State(rudeCount: 3, affinity: 0));

            Assert.That(standing.IsStrike(2), Is.True);
            Assert.That(standing.RudeNotice, Is.EqualTo("무례한 말 3/3 · 대화가 끝났어요"));
        }

        [TestCase(-4, "-4")]
        [TestCase(3, "+3")]
        public void AffinityChangeText_ShowsSign(int change, string expected)
        {
            standing.Apply(State(rudeCount: 0, affinity: 10));

            standing.Apply(State(rudeCount: 0, affinity: 10 + change));

            Assert.That(standing.AffinityChangeText, Is.EqualTo(expected));
        }

        [Test]
        public void Apply_EachState_AdvancesVersion()
        {
            standing.Apply(State(rudeCount: 0, affinity: 12));
            int first = standing.Version;

            standing.Apply(State(rudeCount: 0, affinity: 12));

            Assert.That(standing.Version, Is.EqualTo(first + 1));
        }

        [Test]
        public void Apply_ServerWithoutAffinityOrLimit_StaysHidden()
        {
            // 호감도·무례 한도를 보내기 전의 서버와 대화하면 화면에 아무것도 띄우지 않는다.
            standing.Apply(new ConversationState { Status = "ACTIVE" });
            standing.Apply(null);

            Assert.That(standing.IsKnown, Is.False);
            Assert.That(standing.RudeNotice, Is.Null);
        }

        private static ConversationState State(int rudeCount, int affinity)
        {
            return new ConversationState
            {
                Status = "ACTIVE",
                RudeCount = rudeCount,
                RudeLimit = 3,
                Affinity = affinity,
            };
        }
    }
}
