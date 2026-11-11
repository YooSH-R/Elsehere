using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    public interface IPracticeProgressApi
    {
        Task<PracticeProgress> GetProgressAsync(string scenarioId, long conversationId);
    }

    /// <summary>장소 연습 대화 시작(PRC-02)과 목표 진행 조회(PRC-03). 일반 대화 시작 경로와 분리한다.</summary>
    public sealed class PracticeApi : IPracticeProgressApi
    {
        private readonly ApiClient client;

        public PracticeApi(ApiClient client) => this.client = client;

        public Task<ConversationStart> StartAsync(string scenarioId, string requestId) =>
            client.PostAsync<ConversationStart>(
                ScenarioPath(scenarioId) + "/conversations",
                new { requestId }
            );

        public Task<PracticeProgress> GetProgressAsync(string scenarioId, long conversationId) =>
            client.GetAsync<PracticeProgress>(
                ScenarioPath(scenarioId)
                    + "/conversations/"
                    + conversationId.ToString(CultureInfo.InvariantCulture)
                    + "/progress"
            );

        private static string ScenarioPath(string scenarioId) =>
            "/api/v1/practice-scenarios/" + Uri.EscapeDataString(scenarioId);
    }

    public sealed class PracticeProgress
    {
        public string ScenarioId { get; set; }
        public List<PracticeGoalProgress> Goals { get; set; } = new List<PracticeGoalProgress>();

        /// <summary>목표를 모두 해내 이 상황을 끝낸 것으로 기록했는지</summary>
        public bool Done { get; set; }
    }

    public sealed class PracticeGoalProgress
    {
        public string GoalId { get; set; }
        public string Title { get; set; }
        public string Hint { get; set; }
        public bool Completed { get; set; }
    }
}
