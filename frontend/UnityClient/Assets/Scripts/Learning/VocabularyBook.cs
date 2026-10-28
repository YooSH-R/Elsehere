using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>
    /// 사용자의 단어장(VOCAB-01·03·04). 서버 목록을 읽어 보여 주고, 외웠어요·메모·삭제를 서버에 저장한 뒤 반영한다.
    /// 요청이 실패하면 값을 바꾸지 않고 <see cref="LastFailureMessage"/>로 알린다.
    /// </summary>
    public sealed class VocabularyBook
    {
        /// <summary>메모 최대 길이. 서버(VOCAB-03)와 같다.</summary>
        public const int MaxMemoLength = 200;

        private readonly List<VocabularyEntry> entries = new List<VocabularyEntry>();
        private readonly IVocabularyApi api;

        public VocabularyBook(IVocabularyApi api)
        {
            this.api = api;
        }

        public IReadOnlyList<VocabularyEntry> Entries => entries;

        /// <summary>마지막 요청이 실패한 이유. 화면에 보여 줄 문장이다. 성공하면 null.</summary>
        public string LastFailureMessage { get; private set; }

        /// <summary>서버 단어장을 처음부터 끝까지 읽어 바꾼다. 실패하면 이전 목록을 그대로 둔다.</summary>
        public async Task<bool> LoadAsync()
        {
            var loaded = new List<VocabularyEntry>();
            string cursor = null;
            try
            {
                do
                {
                    VocabPage page = await api.GetPageAsync(cursor);
                    foreach (VocabItem item in page.Items)
                    {
                        loaded.Add(new VocabularyEntry(item));
                    }

                    cursor = page.NextCursor;
                } while (cursor != null);
            }
            catch (ApiException)
            {
                return Fail("단어장을 불러오지 못했어요. 잠시 후 다시 열어 주세요.");
            }

            entries.Clear();
            entries.AddRange(loaded);
            LastFailureMessage = null;
            return true;
        }

        /// <summary>'외웠어요'를 켜거나 끈다. 서버에 저장한 뒤에 표시를 바꾼다.</summary>
        public async Task<bool> ToggleLearnedAsync(VocabularyEntry entry)
        {
            try
            {
                VocabItem saved = await api.UpdateMemorizedAsync(
                    entry.UserVocabId,
                    !entry.IsLearned
                );
                entry.IsLearned = saved.Memorized;
            }
            catch (ApiException)
            {
                return Fail("'외웠어요'를 저장하지 못했어요. 다시 시도해 주세요.");
            }

            LastFailureMessage = null;
            return true;
        }

        /// <summary>메모를 저장한다. <see cref="MaxMemoLength"/>자를 넘으면 서버에 보내지 않는다.</summary>
        public async Task<bool> SaveMemoAsync(VocabularyEntry entry, string memo)
        {
            if (memo != null && memo.Length > MaxMemoLength)
            {
                return Fail($"메모는 {MaxMemoLength}자까지 쓸 수 있어요.");
            }

            try
            {
                VocabItem saved = await api.UpdateMemoAsync(entry.UserVocabId, memo ?? "");
                entry.Memo = saved.Memo;
            }
            catch (ApiException)
            {
                return Fail("메모를 저장하지 못했어요. 다시 시도해 주세요.");
            }

            LastFailureMessage = null;
            return true;
        }

        /// <summary>단어장에서 지운다. 서버에서 지운 뒤에 목록에서 뺀다.</summary>
        public async Task<bool> DeleteAsync(VocabularyEntry entry)
        {
            try
            {
                await api.DeleteAsync(entry.UserVocabId);
            }
            catch (ApiException)
            {
                return Fail("단어를 지우지 못했어요. 다시 시도해 주세요.");
            }

            entries.Remove(entry);
            LastFailureMessage = null;
            return true;
        }

        private bool Fail(string message)
        {
            LastFailureMessage = message;
            return false;
        }
    }
}
