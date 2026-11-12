using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    // 영어 표현 카드의 고친 문장에서 학습자가 한 말과 달라진 곳만 표시한다(S15P31A305-862).
    public class ExpressionChangesTests
    {
        [Test]
        public void ChangedParts_AreTheWordsTheLearnerDidNotSay_IncludingCapitalLetters()
        {
            Assert.That(
                ExpressionChanges.ChangedParts("i want to speak korea", "I want to speak Korean."),
                Is.EqualTo(new[] { "I", "Korean" })
            );
        }

        [Test]
        public void NeighbouringChangedWords_AreOnePart_AndPunctuationIsNeverMarked()
        {
            Assert.That(
                ExpressionChanges.ChangedParts(
                    "yesterday i go to the bakery and buy two bread, but it was close so i come back home",
                    "Yesterday I went to the bakery and bought two loaves of bread, but it was closed, so I came back home."
                ),
                Is.EqualTo(new[] { "Yesterday I went", "bought", "loaves of", "closed", "I came" })
            );
        }

        // 한국어로 말한 부분은 영어로 바뀐 곳이고, 학습자가 영어로 말한 낱말(want)은 그대로다.
        [Test]
        public void KoreanParts_AreMarkedAsChanged_ButTheLearnersEnglishWordsAreNot()
        {
            Assert.That(
                ExpressionChanges.ChangedParts(
                    "한국어로 대화하자. i want korea",
                    "Let's talk in Korean. I want to speak Korean."
                ),
                Is.EqualTo(new[] { "Let's talk in Korean", "I", "to speak Korean" })
            );
        }

        // 한국어로만 말했으면 영어 문장 전체가 새로 생긴 것이라 밑줄을 그어도 알려 주는 것이 없다.
        [Test]
        public void KoreanOnlyLine_MarksNothing()
        {
            Assert.That(
                ExpressionChanges.ChangedParts("카페 어디야?", "Where is the cafe?"),
                Is.Empty
            );
        }

        [Test]
        public void OnlyPunctuationDifferences_MarkNothing()
        {
            Assert.That(
                ExpressionChanges.ChangedParts("I want coffee", "I want coffee."),
                Is.Empty
            );
        }

        [Test]
        public void Mark_WrapsChangedPartsInTheAccentUnderline_AndKeepsTheRestAsPlainText()
        {
            Assert.That(
                ExpressionChanges.Mark("i want coffee", "I want coffee."),
                Is.EqualTo(
                    "<color=#D24F2F><u><noparse>I</noparse></u></color><noparse> want coffee.</noparse>"
                )
            );
        }

        // 문장 안의 '<'를 꾸밈 태그로 읽지 않게 모든 글자를 noparse 안에 둔다.
        [Test]
        public void Mark_NeverLetsTheSentenceBecomeRichTextTags()
        {
            Assert.That(
                ExpressionChanges.Mark("I like <b>", "I like <b>."),
                Is.EqualTo("<noparse>I like <b>.</noparse>")
            );
        }

        [Test]
        public void Mark_WithoutWhatTheLearnerSaid_MarksNothing()
        {
            Assert.That(
                ExpressionChanges.Mark(null, "I want coffee."),
                Is.EqualTo("<noparse>I want coffee.</noparse>")
            );
        }
    }
}
