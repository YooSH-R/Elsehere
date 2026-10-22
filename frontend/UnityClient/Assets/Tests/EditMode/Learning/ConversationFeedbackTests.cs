using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ConversationFeedbackTests
    {
        private static readonly ReferenceExpression SoyLatte = new ReferenceExpression(
            "Could I have a soy latte, please?",
            "두유 카페라테 한 잔 주시겠어요?",
            "Could I have…?는 원하는 것을 정중하게 요청할 때 쓴다."
        );
        private static readonly ReferenceExpression Desserts = new ReferenceExpression(
            "Do you have any desserts?",
            "디저트 있나요?",
            "Do you have any…?로 있는지 자연스럽게 물어볼 수 있다."
        );

        private static readonly List<ReferenceRule> Rules = new List<ReferenceRule>
        {
            new ReferenceRule(new[] { "soy", "latte" }, SoyLatte),
            new ReferenceRule(new[] { "dessert" }, Desserts),
        };

        [Test]
        public void Reviews_UtteranceMatchingRule_GetsReferenceExpression()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "I want soy latte" }, Rules);

            Assert.That(feedback.Reviews, Has.Count.EqualTo(1));
            Assert.That(feedback.Reviews[0].Utterance, Is.EqualTo("I want soy latte"));
            Assert.That(feedback.Reviews[0].Reference, Is.SameAs(SoyLatte));
        }

        [Test]
        public void Reviews_UtteranceWithoutRule_HasNoReference()
        {
            // 참고 표현이 없으면 오류로 보지 않고 그대로 써도 된다고 안내한다.
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!" }, Rules);

            Assert.That(feedback.Reviews[0].Reference, Is.Null);
        }

        [Test]
        public void NextExpression_IsLastReferenceFound()
        {
            var feedback = new ConversationFeedback(
                "Emma",
                new[] { "soy latte please", "Any dessert?", "Thanks!" },
                Rules
            );

            Assert.That(feedback.NextExpression, Is.SameAs(Desserts));
        }

        [Test]
        public void NextExpression_WithoutAnyReference_IsNull()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!" }, Rules);

            Assert.That(feedback.NextExpression, Is.Null);
        }

        [Test]
        public void Summary_MentionsNpcAndUtteranceCount()
        {
            var feedback = new ConversationFeedback("Emma", new[] { "Hello!", "Thanks!" }, Rules);

            Assert.That(feedback.UtteranceCount, Is.EqualTo(2));
            Assert.That(feedback.Summary, Does.Contain("Emma"));
            Assert.That(feedback.Summary, Does.Contain("2"));
        }
    }
}
