using NUnit.Framework;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordTests
    {
        [Test]
        public void ForHover_SavedWord_ShowsExample()
        {
            ObjectWord word = ObjectWord.ForHover(
                "TV",
                "텔레비전",
                "Let's watch TV together.",
                isSavedToVocabulary: true
            );

            Assert.That(word.Example, Is.EqualTo("Let's watch TV together."));
        }

        [Test]
        public void ForHover_UnsavedWord_HidesExampleButKeepsWordAndMeaning()
        {
            // 예문은 단어장에 저장한 뒤에만 호버에 보인다(ERD vocabularies.example).
            ObjectWord word = ObjectWord.ForHover(
                "sofa",
                "소파",
                "He is sitting on the sofa.",
                isSavedToVocabulary: false
            );

            Assert.That(word.Example, Is.Empty);
            Assert.That(word.Text, Is.EqualTo("sofa"));
            Assert.That(word.MeaningKo, Is.EqualTo("소파"));
        }

        [Test]
        public void ForHover_SavedWordWithoutExample_ShowsNoExample()
        {
            ObjectWord word = ObjectWord.ForHover("bed", "침대", null, isSavedToVocabulary: true);

            Assert.That(word.Example, Is.EqualTo(string.Empty));
        }
    }
}
