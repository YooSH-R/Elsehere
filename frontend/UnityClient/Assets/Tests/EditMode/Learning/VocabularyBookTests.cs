using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class VocabularyBookTests
    {
        private VocabularyBook book;

        [SetUp]
        public void CreateBook()
        {
            book = new VocabularyBook();
        }

        [Test]
        public void Save_NewExpression_AddsEntrySavedOnce()
        {
            VocabularyEntry entry = book.Save("Could I have a soy latte, please?", "두유 라테 한 잔 주시겠어요?", "대화");

            Assert.That(book.Entries, Has.Count.EqualTo(1));
            Assert.That(entry.English, Is.EqualTo("Could I have a soy latte, please?"));
            Assert.That(entry.Korean, Is.EqualTo("두유 라테 한 잔 주시겠어요?"));
            Assert.That(entry.Source, Is.EqualTo("대화"));
            Assert.That(entry.SaveCount, Is.EqualTo(1));
            Assert.That(entry.IsLearned, Is.False);
        }

        [Test]
        public void Save_SameExpressionAgain_CountsSaveWithoutNewEntry()
        {
            // 같은 표현은 대소문자·앞뒤 공백이 달라도 한 항목으로 본다.
            VocabularyEntry first = book.Save("on the house", "서비스로 주는", "대화");

            VocabularyEntry second = book.Save("  On the House ", "서비스로 주는", "피드백");

            Assert.That(second, Is.SameAs(first));
            Assert.That(book.Entries, Has.Count.EqualTo(1));
            Assert.That(first.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void Save_BlankExpression_IsIgnored()
        {
            Assert.That(book.Save("  ", "뜻", "대화"), Is.Null);
            Assert.That(book.Save(null, "뜻", "대화"), Is.Null);
            Assert.That(book.Entries, Is.Empty);
        }

        [Test]
        public void Delete_RemovesOnlyThatEntry()
        {
            VocabularyEntry sofa = book.Save("sofa", "소파", "사물");
            VocabularyEntry bench = book.Save("bench", "벤치", "사물");

            book.Delete(sofa);

            Assert.That(book.Entries, Is.EqualTo(new[] { bench }));
        }

        [Test]
        public void ToggleLearned_SwitchesBetweenLearnedAndLearning()
        {
            VocabularyEntry entry = book.Save("sofa", "소파", "사물");

            book.ToggleLearned(entry);
            Assert.That(entry.IsLearned, Is.True);

            book.ToggleLearned(entry);
            Assert.That(entry.IsLearned, Is.False);
        }
    }
}
