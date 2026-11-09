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

        // 이름이 대사와 같은 흰색이면 누가 말했는지 읽기 어렵다. NPC는 이름표와 같은 밝은 코랄, 나는 밝은 하늘색으로 칠한다.
        [Test]
        public void Format_ColorsTheSpeakersApartFromTheLines()
        {
            string text = DialogueLinesText.Format(new[] { Opening, Order }, "Mia", false, null);

            Assert.That(text, Does.StartWith("<color=#FFB199><b>Mia</b></color>  Hi there!"));
            Assert.That(text, Does.Contain("\n<color=#9ED3FF><b>나</b></color>  A latte, please."));
        }

        // 이름 길이가 달라도 문장은 같은 자리에서 시작한다. 이름 뒤 문장과 그 아래 뜻을 같은 들여쓰기로 맞춘다.
        [Test]
        public void Format_StartsEverySentenceAtTheSameIndent()
        {
            string text = DialogueLinesText.Format(
                new[] { Order, Question },
                "Mia",
                true,
                null,
                sentenceIndent: 64
            );

            Assert.That(
                text,
                Does.Contain("<b>나</b></color><indent=64>A latte, please.</indent>")
            );
            Assert.That(
                text,
                Does.Contain("<b>Mia</b></color><indent=64>Regular milk or soy milk?</indent>")
            );
            Assert.That(
                text,
                Does.Contain("\n<indent=64><size=80%>"),
                "뜻도 문장과 같은 자리에서 시작합니다."
            );
        }

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
                    "<color=#FFB199><b>Mia</b></color>  Hi there! What can I get for you?\n"
                        + "<color=#9ED3FF><b>나</b></color>  A latte, please.\n"
                        + "<color=#FFB199><b>Mia</b></color>  Regular milk or soy milk?"
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
                    "<color=#FFB199><b>Mia</b></color>  Regular milk or soy milk?\n"
                        + "<size=80%><color=#E6D3BC>일반 우유로 드릴까요, 두유로 드릴까요?</color></size>\n"
                        + "<size=80%><color=#E6D3BC>단어 풀이 · soy milk 두유</color></size>"
                )
            );
        }

        private static DialogueLine WithGlossary(string english, params GlossaryWord[] words) =>
            DialogueLine.FromNpc(english, "뜻", words);

        // 풀이가 대사의 내용어 대부분으로 늘어나, 대사 안에서 눌러 볼 수 있는 말까지 아래 줄에 다시 쓰면 대사 칸이 길어진다.
        // 아래 단어 풀이 줄에는 대사에서 찾지 못해 밑줄을 칠 수 없는 풀이만 남긴다.
        [Test]
        public void Format_WithMeaning_ListsOnlyGlossaryNotUnderlinedInTheLine()
        {
            DialogueLine line = WithGlossary(
                "Would you like an iced latte to go?",
                new GlossaryWord(105, "latte", "라떼"),
                new GlossaryWord(105, "to go", "포장"),
                new GlossaryWord(105, "takeout", "포장 주문")
            );
            var glossary = new List<GlossaryData>
            {
                new GlossaryData { Text = "latte", MeaningKo = "라떼" },
                new GlossaryData { Text = "to go", MeaningKo = "포장" },
                new GlossaryData { Text = "takeout", MeaningKo = "포장 주문" },
            };

            string text = DialogueLinesText.Format(new[] { line }, "Ben", true, glossary);

            Assert.That(
                text,
                Does.EndWith(
                    "<size=80%><color=#E6D3BC>단어 풀이 · takeout 포장 주문</color></size>"
                )
            );
        }

        [Test]
        public void Format_WithMeaning_OmitsGlossaryRowWhenEveryWordIsUnderlined()
        {
            DialogueLine line = WithGlossary(
                "Would you like an iced latte to go?",
                new GlossaryWord(105, "latte", "라떼"),
                new GlossaryWord(105, "to go", "포장")
            );
            var glossary = new List<GlossaryData>
            {
                new GlossaryData { Text = "latte", MeaningKo = "라떼" },
                new GlossaryData { Text = "to go", MeaningKo = "포장" },
            };

            string text = DialogueLinesText.Format(new[] { line }, "Ben", true, glossary);

            Assert.That(text, Does.Not.Contain("단어 풀이"));
            Assert.That(text, Does.EndWith("<size=80%><color=#E6D3BC>뜻</color></size>"));
        }

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
                    "<color=#FFB199><b>Ben</b></color>  Would you like an iced <link=\"0/0\"><u>latte</u></link> "
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
                    "<color=#FFB199><b>Ben</b></color>  <link=\"0/0\"><u>Latte</u></link> sounds good. "
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

            Assert.That(text, Is.EqualTo("<color=#FFB199><b>Ben</b></color>  An iced latte?"));
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
                Does.StartWith(
                    "<color=#FFB199><b>Ben</b></color>  An iced <link=\"0/0\"><u>latte</u></link>?\n"
                )
            );
        }

        // 고른 단어를 대사 안에서 칠해, 단어 카드가 어느 단어의 뜻인지 바로 보이게 한다.
        [Test]
        public void Format_MarksTheSelectedWordOnly()
        {
            var toGo = new GlossaryWord(105, "to go", "포장");
            DialogueLine line = WithGlossary(
                "An iced latte to go?",
                new GlossaryWord(105, "latte", "라떼"),
                toGo
            );

            string text = DialogueLinesText.Format(new[] { line }, "Ben", true, null, toGo);

            Assert.That(
                text,
                Does.StartWith(
                    "<color=#FFB199><b>Ben</b></color>  An iced <link=\"0/0\"><u>latte</u></link> "
                        + "<link=\"0/1\"><mark=#F982564D><u>to go</u></mark></link>?"
                )
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

        // 자막을 끄면 NPC 대사는 들어서 이해해야 한다. 글자는 투명하게 자리만 차지하고, 대사 칸이 그 자리를 단어마다 뿌옇게 덮는다.
        [Test]
        public void Format_HidingNpcLines_MakesNpcLinesInvisibleAndKeepsMyLines()
        {
            string text = DialogueLinesText.Format(
                new[] { Order, Question },
                "Mia",
                false,
                null,
                hidesNpcLines: true
            );

            Assert.That(
                text,
                Does.Contain(
                    $"<b>Mia</b></color>  <link=\"{DialogueLinesText.HiddenLineLink}\"><alpha=#00>Regular milk or soy milk?<alpha=#FF></link>"
                ),
                "누가 말했는지는 보이고 대사만 가립니다."
            );
            Assert.That(text, Does.Contain("<b>나</b></color>  A latte, please."));
        }

        // [뜻 보기]는 자막보다 큰 도움이다. 뜻을 펼쳤으면 영어 대사도 보여 뜻과 맞춰 보고 풀이 단어를 누를 수 있게 한다.
        [Test]
        public void Format_HidingNpcLinesWithMeaning_ShowsTheEnglish()
        {
            string text = DialogueLinesText.Format(
                new[] { Question },
                "Mia",
                true,
                null,
                hidesNpcLines: true
            );

            Assert.That(text, Does.Contain("Regular milk or soy milk?"));
            Assert.That(text, Does.Not.Contain(DialogueLinesText.HiddenLineLink));
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
