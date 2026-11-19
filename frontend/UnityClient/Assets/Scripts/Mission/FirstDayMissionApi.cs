using System;
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

        public Task<FirstDayMissionState> StartShowcaseAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/showcase/start", null);

        public Task<FirstDayMissionState> StopShowcaseAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/showcase/stop", null);

        public Task<FirstDayMissionState> ConfirmShowcaseTaxiArrivalAsync(string destination) =>
            client.PostAsync<FirstDayMissionState>(
                "/api/v1/missions/showcase/taxi-arrival",
                new ArrivalRequest { Destination = destination }
            );

        public Task<FirstDayMissionState> RestAsync() =>
            client.PostAsync<FirstDayMissionState>("/api/v1/missions/first-day/rest", null);

        public async Task<FirstDayMissionState> ReceiveDinerMealAsync()
        {
            var state = await client.PostAsync<FirstDayMissionState>(
                "/api/v1/missions/town-story/diner-meal",
                null
            );
            state.IsTownStory = true;
            return state;
        }

        public Task<QuestBoardState> GetQuestBoardAsync() =>
            client.GetAsync<QuestBoardState>("/api/v1/missions/quests");

        /// <summary>하던 퀘스트를 그만둬 그 퀘스트의 처음으로 되돌리고, 바뀐 퀘스트 목록을 받는다.</summary>
        public Task<QuestBoardState> CancelQuestAsync(string questId) =>
            client.PostAsync<QuestBoardState>(
                $"/api/v1/missions/quests/{Uri.EscapeDataString(questId)}/cancel",
                null
            );

        public Task<PracticeBoardState> GetPracticeBoardAsync() =>
            client.GetAsync<PracticeBoardState>("/api/v1/practice-places");

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

        private sealed class ArrivalRequest
        {
            public string Destination { get; set; }
        }
    }
}
