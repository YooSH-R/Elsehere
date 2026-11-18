using System.Collections.Generic;
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

        public async Task<FavorUpdate> CallAsync(IReadOnlyList<FavorCallCandidate> candidates)
        {
            var body = new FavorCallBody { Candidates = new List<FavorCallCandidate>(candidates) };
            return await client.PostOrNullAsync<FavorUpdate>("/api/v1/favors/calls", body);
        }

        public Task<FavorUpdate> DeclineAsync(long favorId)
        {
            return client.PostAsync<FavorUpdate>($"/api/v1/favors/{favorId}/decline", null);
        }
    }
}
