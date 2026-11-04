using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>NPC가 준비해 건네기로 한 것 조회 API의 경로(CONV-13).</summary>
    public sealed class FollowUpApi : IFollowUpApi
    {
        private readonly ApiClient client;

        public FollowUpApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<FollowUpList> GetFollowUpsAsync()
        {
            return client.GetAsync<FollowUpList>("/api/v1/follow-ups");
        }
    }
}
