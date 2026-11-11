using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>대화 노트는 한 쪽에 대화 4개를 보이고, 받은 대화의 마지막 쪽에서는 서버에 더 있을 때만 다음으로 넘긴다.</summary>
    public class ConversationHistoryPagesTests
    {
        [TestCase(0, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(9, 3)]
        public void PageCount_FitsFourPerPage(int count, int pages)
        {
            Assert.That(ConversationHistoryPages.PageCount(count), Is.EqualTo(pages));
        }

        [Test]
        public void Label_ShowsPageOfPages()
        {
            Assert.That(ConversationHistoryPages.Label(0, 5), Is.EqualTo("1 / 2"));
        }

        [TestCase(0, 5, false, true)]
        [TestCase(1, 5, false, false)]
        [TestCase(1, 5, true, true)]
        [TestCase(0, 0, false, false)]
        public void CanGoNext_OnlyWithMoreConversations(
            int page,
            int count,
            bool hasMore,
            bool expected
        )
        {
            Assert.That(
                ConversationHistoryPages.CanGoNext(page, count, hasMore),
                Is.EqualTo(expected)
            );
        }

        [Test]
        public void NeedsMore_OnlyOnTheLastReceivedPage()
        {
            Assert.That(ConversationHistoryPages.NeedsMore(1, 5, hasMore: true), Is.True);
            Assert.That(ConversationHistoryPages.NeedsMore(0, 5, hasMore: true), Is.False);
            Assert.That(ConversationHistoryPages.NeedsMore(1, 5, hasMore: false), Is.False);
        }
    }
}
