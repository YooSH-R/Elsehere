using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>지난 대화 API의 경로. 경로는 API 명세(docs/api/mvp-api.md)를 따른다.</summary>
    public sealed class ConversationHistoryApi : IConversationHistoryApi
    {
        private const string ListPath = "/api/v1/conversations?size=20&feedbackOnly=true";

        private readonly ApiClient client;

        public ConversationHistoryApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<ConversationPage> GetPageAsync(string cursor, PlaceGroup? placeGroup)
        {
            string path = ListPath;
            if (placeGroup != null)
            {
                path += "&placeGroup=" + PlaceGroupText.ServerName(placeGroup.Value);
            }

            if (cursor != null)
            {
                path += "&cursor=" + Uri.EscapeDataString(cursor);
            }

            return client.GetAsync<ConversationPage>(path);
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
