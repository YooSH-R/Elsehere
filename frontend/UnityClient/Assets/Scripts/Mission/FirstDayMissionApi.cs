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

        public Task<QuestBoardState> GetQuestBoardAsync() =>
            client.GetAsync<QuestBoardState>("/api/v1/missions/quests");

        public Task<FirstDayMissionState> GetTownStoryAsync() =>
            client.GetAsync<FirstDayMissionState>("/api/v1/missions/town-story");

        public Task<FirstDayMissionState> StartTownStoryAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/town-story/start", null);

        public async Task<FirstDayMissionState> LoadCurrentAsync(string scene)
        {
            FirstDayMissionState firstDay = await VisitAsync(scene);
            if (!firstDay.ShouldOfferTownStory)
                return firstDay;

            FirstDayMissionState townStory = await GetTownStoryAsync();
            townStory.IsTownStory = true;
            return townStory;
        }

        public async Task<FirstDayMissionState> StartCurrentAsync(string scene, bool isTownStory)
        {
            if (isTownStory)
                await StartTownStoryAsync();
            else
                await StartAsync();
            return await LoadCurrentAsync(scene);
        }

        private sealed class VisitRequest
        {
            public string Scene { get; set; }
        }
    }
}
