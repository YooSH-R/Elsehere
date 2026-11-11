using System;
using System.Threading.Tasks;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    /// <summary>대화 노트의 [다시 연습]은 그 대화의 복습 카드를 찾거나 만든 뒤 연습을 시작한다.</summary>
    public class ReviewBookTests
    {
        [Test]
        public async Task Save_RepeatedOriginalConversationKeepsOneServerCard()
        {
            var card = new ReviewCard { Id = 42, SourceConversationId = 55 };
            var book = new ReviewBook(new FakeApi { SavedCard = card });

            Assert.That(await book.SaveAsync(55), Is.SameAs(card));
            Assert.That(await book.SaveAsync(55), Is.SameAs(card));
        }

        private sealed class FakeApi : IReviewApi
        {
            public ReviewCard SavedCard;

            public Task<ReviewCard> SaveAsync(long id) => Task.FromResult(SavedCard);

            public Task<ConversationStart> StartAsync(long id, string requestId) =>
                throw new NotSupportedException();
        }
    }
}
