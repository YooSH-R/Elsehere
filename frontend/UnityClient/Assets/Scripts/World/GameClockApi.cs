using System.Threading.Tasks;
using A305.Api;

namespace A305.World
{
    /// <summary>WORLD-07 <c>POST /api/v1/me/game-clock/beats</c>.</summary>
    public sealed class GameClockApi : IGameClockApi
    {
        private readonly ApiClient client;

        public GameClockApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<GameClockTime> BeatAsync(
            bool interactionPaused = false,
            bool interactionWasPaused = false,
            long? activeSeconds = null
        )
        {
            return client.PostAsync<GameClockTime>(
                "/api/v1/me/game-clock/beats",
                new
                {
                    interactionPaused,
                    interactionWasPaused,
                    activeSeconds,
                }
            );
        }
    }
}
