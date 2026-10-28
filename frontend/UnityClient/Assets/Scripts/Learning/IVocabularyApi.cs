using System.Threading.Tasks;

namespace A305.Learning
{
    /// <summary>단어장 목록(VOCAB-01)·메모·외웠어요(VOCAB-03)·삭제(VOCAB-04). 실패하면 <see cref="A305.Api.ApiException"/>을 던진다.</summary>
    public interface IVocabularyApi
    {
        /// <summary>외운 단어를 포함해 한 페이지를 읽는다.</summary>
        /// <param name="cursor">이전 페이지의 <see cref="VocabPage.NextCursor"/>. 첫 페이지는 null.</param>
        Task<VocabPage> GetPageAsync(string cursor);

        Task<VocabItem> UpdateMemoAsync(long userVocabId, string memo);

        Task<VocabItem> UpdateMemorizedAsync(long userVocabId, bool memorized);

        Task DeleteAsync(long userVocabId);
    }
}
