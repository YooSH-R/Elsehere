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
    }
}
