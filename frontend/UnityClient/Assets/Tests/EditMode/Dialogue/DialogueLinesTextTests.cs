using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialogueLinesTextTests
    {
        private static readonly DialogueLine Opening = DialogueLine.FromNpc(
            "Hi there! What can I get for you?",
            "안녕하세요! 무엇을 드릴까요?"
        );
        private static readonly DialogueLine Order = DialogueLine.FromUser("A latte, please.");
        private static readonly DialogueLine Question = DialogueLine.FromNpc(
            "Regular milk or soy milk?",
            "일반 우유로 드릴까요, 두유로 드릴까요?"
        );

        // 최신 대사만 보이면 방금 무슨 말이 오갔는지 대화 중에 다시 볼 수 없다.
        [Test]
        public void Format_ShowsEveryLineOfThisConversationWithItsSpeaker()
        {
            string text = DialogueLinesText.Format(
                new[] { Opening, Order, Question },
                "Mia",
                false,
                null
            );

            Assert.That(
                text,
                Is.EqualTo(
                    "<b>Mia</b>  Hi there! What can I get for you?\n"
                        + "<b>나</b>  A latte, please.\n"
                        + "<b>Mia</b>  Regular milk or soy milk?"
                )
            );
        }

        // [뜻 보기]와 단어 풀이는 가장 최근 NPC 대사의 것이므로 그 대사 바로 아래에만 붙인다.
        [Test]
        public void Format_WithMeaning_PutsMeaningAndGlossaryUnderTheLatestNpcLineOnly()
        {
            var glossary = new List<GlossaryData>
            {
                new GlossaryData { Text = "soy milk", MeaningKo = "두유" },
            };

            string text = DialogueLinesText.Format(
                new[] { Opening, Order, Question },
                "Mia",
                true,
                glossary
            );

            Assert.That(text, Does.Not.Contain(Opening.Korean));
            Assert.That(
                text,
                Does.EndWith(
                    "<b>Mia</b>  Regular milk or soy milk?\n"
                        + "<color=#6E7888>일반 우유로 드릴까요, 두유로 드릴까요?</color>\n"
                        + "<color=#6E7888>단어 풀이 · soy milk 두유</color>"
                )
            );
        }

        private static DialogueLine WithGlossary(string english, params GlossaryWord[] words) =>
            DialogueLine.FromNpc(english, "뜻", words);

        // 풀이 단어를 대사 안에서 바로 눌러 뜻을 보고 저장할 수 있게 밑줄 친 링크로 둔다.
        [Test]
        public void Format_WithMeaning_UnderlinesGlossaryWordsInTheirLine()
        {
            DialogueLine line = WithGlossary(
                "Would you like an iced latte to go?",
                new GlossaryWord(105, "latte", "라떼"),
                new GlossaryWord(105, "to go", "포장")
            );

            string text = DialogueLinesText.Format(new[] { line }, "Ben", true, null);

            Assert.That(
                text,
                Does.StartWith(
                    "<b>Ben</b>  Would you like an iced <link=\"0/0\"><u>latte</u></link> "
                        + "<link=\"0/1\"><u>to go</u></link>?"
                )
            );
        }

        // 서버 풀이는 문장 첫머리처럼 대소문자가 다를 수 있고, 다른 단어 속 글자(good의 go)는 그 단어가 아니다.
        [Test]
        public void Format_MatchesWholeWordsRegardlessOfCase()
        {
            DialogueLine line = WithGlossary(
                "Latte sounds good. Let's go!",
                new GlossaryWord(105, "latte", "라떼"),
                new GlossaryWord(105, "go", "가다")
            );

            string text = DialogueLinesText.Format(new[] { line }, "Ben", true, null);

            Assert.That(
                text,
                Does.StartWith(
                    "<b>Ben</b>  <link=\"0/0\"><u>Latte</u></link> sounds good. "
                        + "Let's <link=\"0/1\"><u>go</u></link>!"
                )
            );
        }

        [Test]
        public void Format_WithoutMeaning_DoesNotUnderline()
        {
            DialogueLine line = WithGlossary(
                "An iced latte?",
                new GlossaryWord(105, "latte", "라떼")
            );

            string text = DialogueLinesText.Format(new[] { line }, "Ben", false, null);

            Assert.That(text, Is.EqualTo("<b>Ben</b>  An iced latte?"));
        }

        // 뜻을 받은 지난 대사의 단어도 이어서 눌러 볼 수 있게 밑줄을 둔다.
        [Test]
        public void Format_KeepsUnderlinesOnEarlierNpcLines()
        {
            DialogueLine first = WithGlossary(
                "An iced latte?",
                new GlossaryWord(105, "latte", "라떼")
            );

            string text = DialogueLinesText.Format(
                new[] { first, Order, Question },
                "Ben",
                true,
                null
            );

            Assert.That(
                text,
                Does.StartWith("<b>Ben</b>  An iced <link=\"0/0\"><u>latte</u></link>?\n")
            );
        }

        [Test]
        public void FindWord_ByLinkId_ReturnsThatGlossaryWord()
        {
            var toGo = new GlossaryWord(107, "to go", "포장");
            DialogueLine line = WithGlossary(
                "For here or to go?",
                new GlossaryWord(107, "for here", "매장에서"),
                toGo
            );

            GlossaryWord found = DialogueLinesText.FindWord(new[] { Order, line }, "1/1");

            Assert.That(found, Is.SameAs(toGo));
        }

        [TestCase("")]
        [TestCase("0/0")]
        [TestCase("1/5")]
        [TestCase("9/0")]
        [TestCase("menu")]
        public void FindWord_UnknownLink_IsNull(string linkId)
        {
            DialogueLine line = WithGlossary(
                "An iced latte?",
                new GlossaryWord(105, "latte", "라떼")
            );

            Assert.That(DialogueLinesText.FindWord(new[] { Order, line }, linkId), Is.Null);
        }

        [Test]
        public void Format_WithoutMeaning_ShowsOnlyEnglish()
        {
            string text = DialogueLinesText.Format(new[] { Opening }, "Mia", false, null);

            Assert.That(text, Does.Not.Contain(Opening.Korean));
        }

        [Test]
        public void Format_BeforeTheFirstLine_IsEmpty()
        {
            Assert.That(
                DialogueLinesText.Format(new DialogueLine[0], "Mia", false, null),
                Is.Empty
            );
        }
    }
}
