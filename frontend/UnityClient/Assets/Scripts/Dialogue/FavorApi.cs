using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>돌발 부탁 API의 경로(FAV-01·FAV-02).</summary>
    public sealed class FavorApi : IFavorApi
    {
        private readonly ApiClient client;

        public FavorApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<FavorUpdate> GetCurrentAsync()
        {
            return client.GetOrNullAsync<FavorUpdate>("/api/v1/favors/current");
        }

        public Task<FavorUpdate> AbandonAsync(long favorId)
        {
            return client.PostAsync<FavorUpdate>($"/api/v1/favors/{favorId}/abandon", null);
        }
    }
}
