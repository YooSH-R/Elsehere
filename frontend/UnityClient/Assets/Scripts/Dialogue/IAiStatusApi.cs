using System.Threading.Tasks;

namespace A305.Dialogue
{
    /// <summary>
    /// AI 이용 상태 조회. 실패하면 <see cref="A305.Api.ApiException"/>을 던진다. 테스트에서는 가짜 구현으로 바꾼다.
    /// </summary>
    public interface IAiStatusApi
    {
        /// <summary>AI-01 새 대화를 시작할 수 있는지와 게스트의 남은 대화 수.</summary>
        Task<AiStatusData> GetStatusAsync();
    }
}
