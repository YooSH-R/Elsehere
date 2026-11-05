using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC가 준비해 건네기로 한 것 조회. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IFollowUpApi
    {
        /// <summary>CONV-13 아직 건네지 않은 것 전부. 준비가 먼저 끝나는 것부터 온다.</summary>
        Task<FollowUpList> GetFollowUpsAsync();

        /// <summary>CONV-14 학습자가 기다림 카드를 닫았다.</summary>
        Task DismissAsync(long followUpId);
    }
}
