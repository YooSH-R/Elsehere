using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class DialogueHelpTextTests
    {
        [Test]
        public void Words_AreReadableAsSeparateListItems()
        {
            Assert.That(
                DialogueHelpText.Words(new[] { "reservation", "check in" }),
                Is.EqualTo("• reservation\n• check in")
            );
        }

        [Test]
        public void MissingWords_ShowsAnHonestPlaceholder()
        {
            Assert.That(DialogueHelpText.Words(null), Does.Contain("아직"));
        }

        [Test]
        public void WordsOnTheWay_SaysTheyAreComing()
        {
            Assert.That(
                DialogueHelpText.Words(null, isSuggesting: true),
                Is.EqualTo("추천 단어를 고르고 있어요...")
            );
        }

        [Test]
        public void Sentence_PrefersTheRequestedHint()
        {
            Assert.That(
                DialogueHelpText.Sentence("Can I leave my bags?", "Hello."),
                Is.EqualTo("Can I leave my bags?")
            );
        }

        [Test]
        public void MissingHint_UsesAnAvailableExample()
        {
            Assert.That(
                DialogueHelpText.Sentence(null, "I'd like a coffee."),
                Is.EqualTo("I'd like a coffee.")
            );
        }
    }
}
