using System.Threading.Tasks;

namespace A305.Learning
{
    /// <summary>
    /// 단어장 목록(VOCAB-01)·NPC 대사·대화 피드백 표현 저장(VOCAB-02 NPC_LINE·FEEDBACK)·메모·외웠어요(VOCAB-03)·삭제(VOCAB-04).
    /// 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.
    /// </summary>
    public interface IVocabularyApi
    {
        /// <summary>외운 단어를 포함해 한 페이지를 읽는다.</summary>
        /// <param name="cursor">이전 페이지의 <see cref="VocabPage.NextCursor"/>. 첫 페이지는 null.</param>
        Task<VocabPage> GetPageAsync(string cursor);

        /// <summary>NPC 대사의 단어 풀이(CONV-04)에서 한 단어를 저장한다. 이미 저장한 단어면 처음 항목을 돌려준다.</summary>
        /// <param name="turnId">단어 풀이를 받은 NPC 턴</param>
        Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text);

        /// <summary>대화 피드백의 단어장 후보나 '이 상황에서 자주 써요' 표현에서 단어·숙어·문장을 저장한다(VOCAB-02 FEEDBACK).</summary>
        /// <param name="conversationId">피드백을 받은 대화</param>
        Task<VocabItem> SaveFeedbackWordAsync(long conversationId, string text);

        Task<VocabItem> UpdateMemoAsync(long userVocabId, string memo);

        Task<VocabItem> UpdateMemorizedAsync(long userVocabId, bool memorized);

        Task DeleteAsync(long userVocabId);
    }
}
