using System.Threading.Tasks;

namespace A305.Learning
{
    /// <summary>
    /// 지난 대화 목록(CONV-09), 저장된 피드백(FB-01), 대화 기록(CONV-10). 저장된 결과만 읽어 AI 사용량이 소진돼도 쓸 수 있다.
    /// 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.
    /// </summary>
    public interface IConversationHistoryApi
    {
        /// <param name="cursor">이전 페이지의 <see cref="ConversationPage.NextCursor"/>. 첫 페이지는 null.</param>
        /// <param name="placeGroup">이 장소 묶음의 대화만 받는다. null이면 모든 묶음.</param>
        Task<ConversationPage> GetPageAsync(string cursor, PlaceGroup? placeGroup);

        Task<PastFeedback> GetFeedbackAsync(long conversationId);

        Task<ConversationDetail> GetDetailAsync(long conversationId);
    }
}
