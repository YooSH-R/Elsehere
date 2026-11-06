using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>복습장 목록과 요청 상태. 화면은 성공한 요청의 결과만 반영한다.</summary>
    public sealed class ReviewBook
    {
        private readonly IReviewApi api;
        private readonly List<ReviewCard> items = new List<ReviewCard>();
        private string nextCursor;

        public ReviewBook(IReviewApi api) => this.api = api;

        public IReadOnlyList<ReviewCard> Items => items;
        public bool IsBusy { get; private set; }
        public string SceneCode { get; private set; }
        public string Message { get; private set; }
        public bool HasMore => nextCursor != null;

        public Task<bool> LoadMoreAsync()
        {
            if (IsBusy || !HasMore)
                return Task.FromResult(false);
            return RunAsync(async () =>
            {
                ReviewPage page = await api.GetPageAsync(SceneCode, nextCursor);
                RequirePage(page);
                foreach (ReviewCard card in page.Items ?? new List<ReviewCard>())
                    if (card != null && !items.Exists(item => item.Id == card.Id))
                        items.Add(card);
                nextCursor = page.NextCursor;
            });
        }

        public async Task<ReviewCard> SaveAsync(long conversationId)
        {
            ReviewCard saved = null;
            await RunAsync(async () =>
            {
                ReviewCard card = await api.SaveAsync(conversationId);
                if (card == null || card.Id <= 0)
                    throw UnexpectedResponse();
                saved = card;
                int index = items.FindIndex(item => item.Id == card.Id);
                if (index >= 0)
                    items[index] = card;
                else if (SceneCode == null || card.SceneCode == SceneCode)
                    items.Insert(0, card);
            });
            return saved;
        }

        public Task<bool> DeleteAsync(long cardId) =>
            RunAsync(async () =>
            {
                await api.DeleteAsync(cardId);
                items.RemoveAll(card => card.Id == cardId);
            });

        public async Task<bool> LoadAsync(string sceneCode = null)
        {
            if (IsBusy)
                return false;
            IsBusy = true;
            Message = null;
            try
            {
                ReviewPage page = await api.GetPageAsync(sceneCode, null);
                RequirePage(page);
                items.Clear();
                items.AddRange(page.Items ?? new List<ReviewCard>());
                nextCursor = page.NextCursor;
                SceneCode = sceneCode;
                return true;
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> RunAsync(Func<Task> request)
        {
            if (IsBusy)
                return false;
            IsBusy = true;
            Message = null;
            try
            {
                await request();
                return true;
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static void RequirePage(ReviewPage page)
        {
            if (page == null)
                throw UnexpectedResponse();
        }

        private static ApiException UnexpectedResponse() =>
            new ApiException(
                ApiErrorCodes.UnexpectedResponse,
                0,
                "복습장을 읽지 못했어요. 다시 시도해 주세요."
            );
    }
}
