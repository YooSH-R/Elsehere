using System.Collections.Generic;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>
    /// 문장 교정 탭은 발화마다 카드 한 장이다. 고친 영어 문장, 한국어로 한 말의 영어 문장, 고칠 것이 없는 문장을
    /// 서로 다른 이름표로 보이고, 담을 영어 문장이 있을 때만 [담기]를 둔다.
    /// </summary>
    public class CorrectionCardTests
    {
        [Test]
        public void CorrectedEnglish_StrikesWhatISaidAndOffersTheBetterSentence()
        {
            CorrectionCard card = Card(
                "I think lost my small bag in here",
                "I think I lost my small bag in here.",
                "lost 앞에도 주어 I를 넣어요."
            );

            Assert.That(card.Kind, Is.EqualTo(CorrectionKind.Corrected));
            Assert.That(card.SaidLabel, Is.EqualTo("내가 한 말"));
            Assert.That(card.Said, Is.EqualTo("I think lost my small bag in here"));
            Assert.That(card.IsSaidStruck, Is.True);
            Assert.That(card.BetterLabel, Is.EqualTo("이렇게 말해요"));
            Assert.That(card.Better, Is.EqualTo("I think I lost my small bag in here."));
            Assert.That(card.Reason, Is.EqualTo("lost 앞에도 주어 I를 넣어요."));
            Assert.That(card.CanSave, Is.True);
        }

        // 한국어로 한 말은 틀린 문장이 아니므로 줄을 긋지 않고, 영어 문장은 "영어로는"으로 보인다.
        [TestCase("가방 안에 여권 지갑이 있어요")]
        [TestCase("I'd like 라테, please.")]
        public void KoreanUtterance_KeepsTheKoreanTextAndShowsItInEnglish(string said)
        {
            CorrectionCard card = Card(
                said,
                "My passport wallet is in the bag.",
                "한국어로 말한 문장을 영어로 바꿨어요."
            );

            Assert.That(card.Kind, Is.EqualTo(CorrectionKind.Korean));
            Assert.That(card.SaidLabel, Is.EqualTo("내가 한 말 · 한국어"));
            Assert.That(card.Said, Is.EqualTo(said));
            Assert.That(card.IsSaidStruck, Is.False);
            Assert.That(card.BetterLabel, Is.EqualTo("영어로는"));
            Assert.That(card.Better, Is.EqualTo("My passport wallet is in the bag."));
            Assert.That(card.CanSave, Is.True);
        }

        // 뜻이 불분명해 영어 문장을 받지 못한 한국어 발화를 "잘했어요"로 칭찬하지 않는다.
        [Test]
        public void KoreanUtteranceWithoutEnglish_IsNotPraisedAndHasNothingToSave()
        {
            CorrectionCard card = Card("그거 주세요", null, "어떤 것을 말하는지 알 수 없었어요.");

            Assert.That(card.Kind, Is.EqualTo(CorrectionKind.Korean));
            Assert.That(card.SaidLabel, Is.EqualTo("내가 한 말 · 한국어"));
            Assert.That(card.BetterLabel, Is.Null);
            Assert.That(card.Better, Is.Null);
            Assert.That(card.Reason, Is.EqualTo("어떤 것을 말하는지 알 수 없었어요."));
            Assert.That(card.CanSave, Is.False);
        }

        // 고칠 것이 없으면(제안이 없거나 대소문자·끝 문장부호만 다르면) 그 문장을 "잘했어요"로 보이고 담을 문장을 두지 않는다.
        [TestCase(null)]
        [TestCase("can I get an iced latte, please")]
        public void SentenceWithoutCorrection_IsShownAsWellSaid(string better)
        {
            CorrectionCard card = Card(
                "Can I get an iced latte, please?",
                better,
                "Can I get ~ please?로 정중하게 주문했어요."
            );

            Assert.That(card.Kind, Is.EqualTo(CorrectionKind.WellSaid));
            Assert.That(card.SaidLabel, Is.EqualTo("잘했어요"));
            Assert.That(card.Said, Is.EqualTo("Can I get an iced latte, please?"));
            Assert.That(card.IsSaidStruck, Is.False);
            Assert.That(card.BetterLabel, Is.Null);
            Assert.That(card.Better, Is.Null);
            Assert.That(card.Reason, Is.EqualTo("Can I get ~ please?로 정중하게 주문했어요."));
            Assert.That(card.CanSave, Is.False);
        }

        [Test]
        public void Cards_FollowTheOrderOfTheUtterances()
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                2,
                new ServerFeedback(
                    new FeedbackData { Status = "DONE", Summary = "주문했어요." },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = "I want go",
                            BetterText = "I want to go.",
                            Reason = "to를 넣어요.",
                        },
                        new DetailedFeedbackItem
                        {
                            UserText = "Thanks!",
                            Reason = "고마움을 전했어요.",
                        },
                    },
                    null
                )
            );

            IReadOnlyList<CorrectionCard> cards = CorrectionCard.From(feedback);

            Assert.That(cards, Has.Count.EqualTo(2));
            Assert.That(cards[0].Kind, Is.EqualTo(CorrectionKind.Corrected));
            Assert.That(cards[1].Kind, Is.EqualTo(CorrectionKind.WellSaid));
        }

        private static CorrectionCard Card(string said, string better, string reason)
        {
            var feedback = ConversationFeedback.FromServer(
                "Mia",
                1,
                new ServerFeedback(
                    new FeedbackData { Status = "DONE", Summary = "대화했어요." },
                    new List<DetailedFeedbackItem>
                    {
                        new DetailedFeedbackItem
                        {
                            UserText = said,
                            BetterText = better,
                            Reason = reason,
                        },
                    },
                    null
                )
            );
            return CorrectionCard.From(feedback)[0];
        }
    }
}
