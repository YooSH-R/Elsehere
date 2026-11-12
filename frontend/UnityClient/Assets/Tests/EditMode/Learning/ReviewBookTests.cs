using System;
using System.Threading.Tasks;
using A305.Api;
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

        [Test]
        public async Task Save_WhenNothingToPractice_ExplainsWhyInsteadOfTheServerWording()
        {
            var book = new ReviewBook(
                new FakeApi
                {
                    Failure = new ApiException(
                        "REVIEW_NOT_READY",
                        409,
                        "이 대화는 연습할 표현이 부족해요."
                    ),
                }
            );

            Assert.That(await book.SaveAsync(55), Is.Null);
            Assert.That(
                book.Message,
                Is.EqualTo(
                    "이 대화에는 다시 연습할 문장이 없어요. 고친 문장이나 추천 표현이 2개 이상 있는 대화만 연습할 수 있어요."
                )
            );
        }

        [Test]
        public async Task Save_WhenTheServerCannotBeReached_SaysSo()
        {
            var book = new ReviewBook(
                new FakeApi
                {
                    Failure = new ApiException(ApiErrorCodes.NetworkError, 0, "연결 실패"),
                }
            );

            Assert.That(await book.SaveAsync(55), Is.Null);
            Assert.That(
                book.Message,
                Is.EqualTo(
                    "서버에 연결하지 못해 연습을 준비하지 못했어요. 잠시 뒤 다시 눌러 주세요."
                )
            );
        }

        private sealed class FakeApi : IReviewApi
        {
            public ReviewCard SavedCard;
            public ApiException Failure;

            public Task<ReviewCard> SaveAsync(long id) =>
                Failure != null
                    ? Task.FromException<ReviewCard>(Failure)
                    : Task.FromResult(SavedCard);

            public Task<ConversationStart> StartAsync(long id, string requestId) =>
                throw new NotSupportedException();
        }
    }
}
