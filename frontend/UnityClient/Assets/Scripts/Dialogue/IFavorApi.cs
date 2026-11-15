using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// 진행 중 돌발 부탁 조회와 그만두기. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IFavorApi
    {
        /// <summary>FAV-01 진행 중 부탁. 없으면 null이다.</summary>
        Task<FavorUpdate> GetCurrentAsync();

        /// <summary>FAV-02 진행 중 부탁 그만두기. 끝난 부탁을 돌려준다.</summary>
        Task<FavorUpdate> AbandonAsync(long favorId);
    }
}
