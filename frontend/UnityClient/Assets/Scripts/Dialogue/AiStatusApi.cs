using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>AI 이용 상태 API의 경로. 경로는 API 명세(docs/api/mvp-api.md AI-01)를 따른다.</summary>
    public sealed class AiStatusApi : IAiStatusApi
    {
        private readonly ApiClient client;

        public AiStatusApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<AiStatusData> GetStatusAsync()
        {
            return client.GetAsync<AiStatusData>("/api/v1/ai/status");
        }
    }
}
