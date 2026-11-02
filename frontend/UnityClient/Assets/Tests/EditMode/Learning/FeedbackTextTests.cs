using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class FeedbackTextTests
    {
        // 대화 노트 부제는 이름 끝소리에 맞춰 와·과를 고른다. 영어 이름은 한국어로 읽을 때 받침이 남는지(n·m·l·ng)로 본다.
        [TestCase("Ben", "Ben과 나눈 대화")]
        [TestCase("Mia", "Mia와 나눈 대화")]
        [TestCase("Jake", "Jake와 나눈 대화")]
        [TestCase("Alex", "Alex와 나눈 대화")]
        [TestCase("Sam", "Sam과 나눈 대화")]
        [TestCase("Bill", "Bill과 나눈 대화")]
        [TestCase("Young", "Young과 나눈 대화")]
        [TestCase("지민", "지민과 나눈 대화")]
        [TestCase("하나", "하나와 나눈 대화")]
        public void Partner_PicksWaOrGwaByTheLastSoundOfTheName(string npcName, string expected)
        {
            Assert.That(FeedbackText.Partner(npcName), Is.EqualTo(expected));
        }

        [Test]
        public void Transcript_ListsLinesWithSpeakerNames()
        {
            string text = FeedbackText.Transcript(
                "Mia",
                new[]
                {
                    DialogueLine.FromNpc("Hi! What can I get for you?", null),
                    DialogueLine.FromUser("An iced latte, please."),
                }
            );

            Assert.That(text, Does.Contain("<b>Mia</b>  Hi! What can I get for you?"));
            Assert.That(text, Does.Contain("<b>나</b>  An iced latte, please."));
        }

        [Test]
        public void Transcript_WithoutLines_SaysNothingWasExchanged()
        {
            string text = FeedbackText.Transcript("Mia", new List<DialogueLine>());

            Assert.That(text, Is.EqualTo("주고받은 대화가 없어요."));
        }

        [Test]
        public void Detail_ExampleFeedback_ShowsExampleNotices()
        {
            // 대본 대화는 키워드 규칙으로 만든 예시라 오류를 판정하지 않는다는 안내를 유지한다.
            var feedback = new ConversationFeedback(
                "Emma",
                new[] { "Hello!" },
                new List<ReferenceRule>()
            );

            string text = FeedbackText.Detail(feedback);

            Assert.That(
                text,
                Does.Contain("참고 표현 없음 · 지금 문장 그대로도 자연스럽게 전달돼요.")
            );
            Assert.That(text, Does.Contain("문법 오류나 점수를 매기지 않아요."));
        }

        [Test]
        public void Detail_ServerReviewWithoutBetterText_ShowsAiReasonInsteadOfNaturalNotice()
        {
            // AI가 뜻을 확인해야 한다고 본 발화에 "자연스럽게 전달돼요"를 붙이면 AI 평가와 반대가 된다.
            ConversationFeedback feedback = ServerFeedbackWith(
                new DetailedFeedbackItem
                {
                    UserText = "That one, the thing.",
                    BetterText = null,
                    Reason =
                        "어떤 것을 가리키는지 분명하지 않아요. 원하는 메뉴 이름을 말해 보세요.",
                }
            );

            string text = FeedbackText.Detail(feedback);

            Assert.That(
                text,
                Does.Contain(
                    "어떤 것을 가리키는지 분명하지 않아요. 원하는 메뉴 이름을 말해 보세요."
                )
            );
            Assert.That(text, Does.Not.Contain("자연스럽게 전달돼요"));
        }

        [Test]
        public void Detail_ServerIdiom_ShowsOnItsOwnLine()
        {
            ConversationFeedback feedback = ServerFeedbackWith(
                new DetailedFeedbackItem
                {
                    UserText = "Medium size, please.",
                    BetterText = "A medium, please.",
                    Reason = "사이즈만 말할 때는 관사와 함께 짧게 말해요.",
                    Idiom = new IdiomData
                    {
                        Text = "for here or to go",
                        MeaningKo = "매장에서 드실 건가요, 가져가실 건가요?",
                    },
                },
                new DetailedFeedbackItem
                {
                    UserText = "I'm good, thanks.",
                    BetterText = null,
                    Reason = "괜찮다는 뜻이 자연스럽게 전달됐어요.",
                    Idiom = new IdiomData { Text = "I'm good", MeaningKo = "괜찮아요, 됐어요" },
                }
            );

            string text = FeedbackText.Detail(feedback);

            Assert.That(text, Does.Contain("참고 표현 · <b>A medium, please.</b>"));
            Assert.That(text, Does.Not.Contain("A medium, please.</b> ("));
            Assert.That(
                text,
                Does.Contain(
                    "회화 표현 · <b>for here or to go</b> (매장에서 드실 건가요, 가져가실 건가요?)"
                )
            );
            Assert.That(text, Does.Contain("회화 표현 · <b>I'm good</b> (괜찮아요, 됐어요)"));
        }

        [Test]
        public void Detail_ServerFeedback_DoesNotClaimToSkipGrammar()
        {
            // AI 피드백은 문법 교정도 한다. 예시 피드백의 "문법 오류를 매기지 않는다" 안내를 붙이면 내용과 어긋난다.
            ConversationFeedback feedback = ServerFeedbackWith(
                new DetailedFeedbackItem
                {
                    UserText = "I want go to the station.",
                    BetterText = "I want to go to the station.",
                    Reason = "want 뒤에 동작을 말할 때는 to와 동사원형을 사용해요.",
                }
            );

            string text = FeedbackText.Detail(feedback);

            Assert.That(text, Does.Not.Contain("문법 오류나 점수를 매기지 않아요."));
            Assert.That(text, Does.Contain("AI가 대화를 보고 만든 피드백이에요."));
        }

        private static ConversationFeedback ServerFeedbackWith(params DetailedFeedbackItem[] items)
        {
            return ConversationFeedback.FromServer(
                "Mia",
                items.Length,
                new ServerFeedback(
                    new FeedbackData { Status = "DONE", Summary = "카페에서 주문했어요" },
                    new List<DetailedFeedbackItem>(items),
                    null
                )
            );
        }
    }
}
