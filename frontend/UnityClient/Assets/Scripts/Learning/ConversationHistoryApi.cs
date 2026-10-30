using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>지난 대화 API의 경로. 경로는 API 명세(docs/api/mvp-api.md)를 따른다.</summary>
    public sealed class ConversationHistoryApi : IConversationHistoryApi
    {
        private const string ListPath = "/api/v1/conversations?size=20";

        private readonly ApiClient client;

        public ConversationHistoryApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<ConversationPage> GetPageAsync(string cursor)
        {
            if (cursor == null)
            {
                return client.GetAsync<ConversationPage>(ListPath);
            }

            return client.GetAsync<ConversationPage>(
                ListPath + "&cursor=" + Uri.EscapeDataString(cursor)
            );
        }

        public Task<PastFeedback> GetFeedbackAsync(long conversationId)
        {
            return client.GetAsync<PastFeedback>(ConversationPath(conversationId) + "/feedback");
        }

        public Task<ConversationDetail> GetDetailAsync(long conversationId)
        {
            return client.GetAsync<ConversationDetail>(ConversationPath(conversationId));
        }

        private static string ConversationPath(long conversationId)
        {
            return "/api/v1/conversations/" + conversationId;
        }
    }
}
