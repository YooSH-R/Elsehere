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
                        + "<size=80%><color=#E6D3BC>일반 우유로 드릴까요, 두유로 드릴까요?</color></size>\n"
                        + "<size=80%><color=#E6D3BC>단어 풀이 · soy milk 두유</color></size>"
                )
            );
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
