using System.Threading.Tasks;
using A305.Api;

namespace A305.Mission
{
    public sealed class FirstDayMissionApi
    {
        private readonly ApiClient client;

        public FirstDayMissionApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<FirstDayMissionState> VisitAsync(string scene)
        {
            return client.PostAsync<FirstDayMissionState>(
                "/api/v1/missions/first-day/visit",
                new VisitRequest { Scene = scene }
            );
        }

        public Task<FirstDayMissionState> StartAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/first-day/start", null);

        public Task<FirstDayMissionState> RestAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/first-day/rest", null);

        private sealed class VisitRequest
        {
            public string Scene { get; set; }
        }
    }
}
