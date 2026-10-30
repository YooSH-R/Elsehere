using System.Threading.Tasks;

namespace A305.World
{
    /// <summary>WORLD-07 게임 시간 신호. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface IGameClockApi
    {
        Task<GameClockTime> BeatAsync();
    }
}
