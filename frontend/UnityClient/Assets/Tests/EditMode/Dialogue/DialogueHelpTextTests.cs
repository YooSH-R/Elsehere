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
                Is.EqualTo("<b>reservation</b>\n\n<b>check in</b>")
            );
        }

        [Test]
        public void Meanings_AppearBelowThePhraseWithoutBulletsOrSeparators()
        {
            Assert.That(
                DialogueHelpText.Words(
                    new[] { "for here — 매장에서 먹을게요", "to go — 포장해 주세요" }
                ),
                Is.EqualTo(
                    "<b>for here</b>\n<size=80%>매장에서 먹을게요</size>\n\n<b>to go</b>\n<size=80%>포장해 주세요</size>"
                )
            );
        }

        [Test]
        public void HyphenatedWords_AreNotSplitAndBlankEntriesAreIgnored()
        {
            Assert.That(
                DialogueHelpText.Words(new[] { " ", "check-in — 체크인", null }),
                Is.EqualTo("<b>check-in</b>\n<size=80%>체크인</size>")
            );
        }

        [Test]
        public void ModelText_CannotInsertRichTextTags()
        {
            Assert.That(
                DialogueHelpText.Words(new[] { "<size=99> — <b>뜻</b>" }),
                Is.EqualTo("<b>‹size=99›</b>\n<size=80%>‹b›뜻‹/b›</size>")
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
