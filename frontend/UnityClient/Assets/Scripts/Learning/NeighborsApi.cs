using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>[이웃] 창 API 경로. 경로는 API 명세(docs/api/mvp-api.md)의 WORLD-08을 따른다.</summary>
    public sealed class NeighborsApi : INeighborsApi
    {
        private const string ListPath = "/api/v1/me/npcs";

        private readonly ApiClient client;

        public NeighborsApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<NeighborList> GetNeighborsAsync()
        {
            return client.GetAsync<NeighborList>(ListPath);
        }
    }
}
