using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class GlossaryWordTests
    {
        [Test]
        public void FromMeaning_KeepsTheTurnOfEveryWord()
        {
            var glossary = new List<GlossaryData>
            {
                new GlossaryData { Text = "latte", MeaningKo = "라떼" },
                new GlossaryData { Text = "to go", MeaningKo = "포장" },
            };

            IReadOnlyList<GlossaryWord> words = GlossaryWord.FromMeaning(105, glossary);

            Assert.That(words.Select(word => word.TurnId), Is.EqualTo(new[] { 105L, 105L }));
            Assert.That(words.Select(word => word.Text), Is.EqualTo(new[] { "latte", "to go" }));
            Assert.That(words.Select(word => word.MeaningKo), Is.EqualTo(new[] { "라떼", "포장" }));
        }

        // 쉬운 대사는 서버가 빈 풀이를 보내고, 예전 응답은 풀이가 없을 수 있다.
        [Test]
        public void FromMeaning_WithoutGlossary_IsEmpty()
        {
            Assert.That(GlossaryWord.FromMeaning(105, null), Is.Empty);
        }
    }
}
