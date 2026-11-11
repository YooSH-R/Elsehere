using System.Globalization;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>저장된 복습 카드와 REVIEW 대화 시작 API. 일반 대화 시작 경로와 분리한다.</summary>
    public sealed class ReviewApi : IReviewApi, IReviewProgressApi
    {
        private readonly ApiClient client;

        public ReviewApi(ApiClient client) => this.client = client;

        public Task<ReviewCard> SaveAsync(long sourceConversationId) =>
            client.PostAsync<ReviewCard>(
                "/api/v1/review-cards",
                new { conversationId = sourceConversationId }
            );

        public Task<ConversationStart> StartAsync(long cardId, string requestId) =>
            client.PostAsync<ConversationStart>(
                CardPath(cardId) + "/conversations",
                new { requestId, speechSpeed = NpcSpeechSpeed.ForRequest }
            );

        private static string CardPath(long cardId) =>
            "/api/v1/review-cards/" + cardId.ToString(CultureInfo.InvariantCulture);

        public Task<ReviewPracticeProgress> GetProgressAsync(long cardId, long conversationId) =>
            client.GetAsync<ReviewPracticeProgress>(
                CardPath(cardId)
                    + "/conversations/"
                    + conversationId.ToString(CultureInfo.InvariantCulture)
                    + "/progress"
            );
    }
}
