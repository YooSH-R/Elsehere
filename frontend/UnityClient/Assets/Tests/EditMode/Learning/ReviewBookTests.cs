using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;
using NUnit.Framework;

namespace A305.Learning.Tests
{
    public class ReviewBookTests
    {
        [Test]
        public async Task Load_PreventsConcurrentRequestsAndCommitsTheSuccessfulFilter()
        {
            var reply = new TaskCompletionSource<ReviewPage>();
            var api = new FakeApi { Page = reply.Task };
            var book = new ReviewBook(api);

            Task<bool> first = book.LoadAsync("CAFE");

            Assert.That(book.IsBusy, Is.True);
            Assert.That(await book.LoadAsync("HOTEL"), Is.False);
            Assert.That(api.ReadCount, Is.EqualTo(1));
            reply.SetResult(
                new ReviewPage
                {
                    Items = new List<ReviewCard> { new ReviewCard { Id = 42 } },
                    NextCursor = "42",
                }
            );
            Assert.That(await first, Is.True);
            Assert.That(book.SceneCode, Is.EqualTo("CAFE"));
            Assert.That(book.Items[0].Id, Is.EqualTo(42));
            Assert.That(book.HasMore, Is.True);
            Assert.That(book.IsBusy, Is.False);
        }

        [Test]
        public async Task LoadFailure_KeepsThePreviousCardsAndFilter()
        {
            var api = new FakeApi { Page = Task.FromResult(Page(42, "41")) };
            var book = new ReviewBook(api);
            await book.LoadAsync("CAFE");
            api.Page = Task.FromException<ReviewPage>(
                new ApiException("NETWORK_ERROR", 0, "연결을 확인해 주세요.")
            );

            Assert.That(await book.LoadAsync("HOTEL"), Is.False);
            Assert.That(book.Items[0].Id, Is.EqualTo(42));
            Assert.That(book.SceneCode, Is.EqualTo("CAFE"));
            Assert.That(book.Message, Is.EqualTo("연결을 확인해 주세요."));
        }

        [Test]
        public async Task LoadMore_AppendsNewCardsOnceAndUsesTheSavedCursor()
        {
            var api = new FakeApi { Page = Task.FromResult(Page(42, "42")) };
            var book = new ReviewBook(api);
            await book.LoadAsync("CAFE");
            var next = Page(42, null);
            next.Items.Add(new ReviewCard { Id = 41 });
            api.Page = Task.FromResult(next);

            Assert.That(await book.LoadMoreAsync(), Is.True);
            Assert.That(api.Cursor, Is.EqualTo("42"));
            Assert.That(book.Items.Count, Is.EqualTo(2));
            Assert.That(book.Items[1].Id, Is.EqualTo(41));
            Assert.That(book.HasMore, Is.False);
            Assert.That(await book.LoadMoreAsync(), Is.False);
            Assert.That(api.ReadCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Save_RepeatedOriginalConversationKeepsOneServerCard()
        {
            var card = new ReviewCard { Id = 42, SourceConversationId = 55 };
            var api = new FakeApi { SavedCard = card };
            var book = new ReviewBook(api);

            Assert.That(await book.SaveAsync(55), Is.SameAs(card));
            Assert.That(await book.SaveAsync(55), Is.SameAs(card));
            Assert.That(book.Items.Count, Is.EqualTo(1));
            Assert.That(book.Items[0].Id, Is.EqualTo(42));
        }

        [Test]
        public async Task DeleteFailure_KeepsTheCardAndExplainsWhy()
        {
            var api = new FakeApi
            {
                Page = Task.FromResult(Page(42, null)),
                DeleteError = new ApiException(
                    "REVIEW_IN_PROGRESS",
                    409,
                    "연습을 마친 뒤 삭제해 주세요."
                ),
            };
            var book = new ReviewBook(api);
            await book.LoadAsync();

            Assert.That(await book.DeleteAsync(42), Is.False);
            Assert.That(book.Items.Count, Is.EqualTo(1));
            Assert.That(book.Message, Is.EqualTo("연습을 마친 뒤 삭제해 주세요."));
        }

        private static ReviewPage Page(long id, string cursor) =>
            new ReviewPage
            {
                Items = new List<ReviewCard> { new ReviewCard { Id = id } },
                NextCursor = cursor,
            };

        private sealed class FakeApi : IReviewApi
        {
            public Task<ReviewPage> Page;
            public int ReadCount;
            public string Cursor;
            public ReviewCard SavedCard;
            public ApiException DeleteError;

            public Task<ReviewPage> GetPageAsync(string sceneCode, string cursor)
            {
                ReadCount++;
                Cursor = cursor;
                return Page;
            }

            public Task<ReviewCard> SaveAsync(long id) => Task.FromResult(SavedCard);

            public Task DeleteAsync(long id) =>
                DeleteError == null ? Task.CompletedTask : Task.FromException(DeleteError);

            public Task<ConversationStart> StartAsync(long id, string requestId) =>
                throw new NotSupportedException();
        }
    }
}
