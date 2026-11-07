using System;
using System.Globalization;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>저장된 복습 카드와 REVIEW 대화 시작 API. 일반 대화 시작 경로와 분리한다.</summary>
    public sealed class ReviewApi : IReviewApi
    {
        private readonly ApiClient client;

        public ReviewApi(ApiClient client) => this.client = client;

        public Task<ReviewPage> GetPageAsync(string sceneCode, string cursor)
        {
            string path = "/api/v1/review-cards?size=20";
            if (!string.IsNullOrEmpty(sceneCode))
                path += "&sceneCode=" + Uri.EscapeDataString(sceneCode);
            if (cursor != null)
                path += "&cursor=" + Uri.EscapeDataString(cursor);
            return client.GetAsync<ReviewPage>(path);
        }

        public Task<ReviewCard> SaveAsync(long sourceConversationId) =>
            client.PostAsync<ReviewCard>(
                "/api/v1/review-cards",
                new { conversationId = sourceConversationId }
            );

        public Task DeleteAsync(long cardId) => client.DeleteAsync(CardPath(cardId));

        public Task<ConversationStart> StartAsync(long cardId, string requestId) =>
            client.PostAsync<ConversationStart>(
                CardPath(cardId) + "/conversations",
                new { requestId }
            );

        private static string CardPath(long cardId) =>
            "/api/v1/review-cards/" + cardId.ToString(CultureInfo.InvariantCulture);

        public Task<ReviewPracticeResult> GetResultAsync(long cardId) =>
            client.GetAsync<ReviewPracticeResult>(CardPath(cardId) + "/result");
    }
}
