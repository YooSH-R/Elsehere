using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>
    /// 사용자의 단어장(VOCAB-01·02·03·04). 서버 목록을 읽어 보여 주고, 대화 중 저장한 단어와 외웠어요·메모·삭제를 서버에 저장한 뒤 반영한다.
    /// 요청이 실패하면 값을 바꾸지 않고 <see cref="LastFailureMessage"/>로 알린다.
    /// </summary>
    public sealed class VocabularyBook
    {
        /// <summary>메모 최대 길이. 서버(VOCAB-03)와 같다.</summary>
        public const int MaxMemoLength = 200;

        private readonly List<VocabularyEntry> entries = new List<VocabularyEntry>();
        private readonly IVocabularyApi api;

        // 서버 목록을 한 번이라도 받았는지와 지금 받는 중인지. 받기 전의 빈 목록은 '단어 없음'이 아니다.
        private bool isLoaded;
        private bool isLoading;

        public VocabularyBook(IVocabularyApi api)
        {
            this.api = api;
        }

        public IReadOnlyList<VocabularyEntry> Entries => entries;

        /// <summary>
        /// 화면이 보여 줄 상태. 보여 줄 단어가 있으면 다시 읽는 중이거나 다시 읽지 못했어도 그 단어를 보인다.
        /// 단어가 없으면 읽는 중, 처음 읽기 실패, 빈 목록을 구분한다.
        /// </summary>
        public VocabularyListState ListState
        {
            get
            {
                if (entries.Count > 0)
                {
                    return VocabularyListState.Entries;
                }

                if (isLoading)
                {
                    return VocabularyListState.Loading;
                }

                if (!isLoaded)
                {
                    return LastFailureMessage != null
                        ? VocabularyListState.Failed
                        : VocabularyListState.Loading;
                }

                return VocabularyListState.Empty;
            }
        }

        /// <summary>마지막 요청이 실패한 이유. 화면에 보여 줄 문장이다. 성공하면 null.</summary>
        public string LastFailureMessage { get; private set; }

        /// <summary>서버 단어장을 처음부터 끝까지 읽어 바꾼다. 실패하면 이전 목록을 그대로 둔다.</summary>
        public async Task<bool> LoadAsync()
        {
            var loaded = new List<VocabularyEntry>();
            string cursor = null;
            isLoading = true;
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
            finally
            {
                isLoading = false;
            }

            entries.Clear();
            entries.AddRange(loaded);
            isLoaded = true;
            LastFailureMessage = null;
            return true;
        }

        /// <summary>대화 중 NPC 대사의 풀이 단어를 저장한다(VOCAB-02 NPC_LINE).</summary>
        /// <param name="turnId">단어 풀이를 받은 NPC 턴</param>
        public async Task<VocabularySaveResult> SaveNpcLineWordAsync(long turnId, string text)
        {
            VocabItem saved;
            try
            {
                saved = await api.SaveNpcLineWordAsync(turnId, text);
            }
            catch (ApiException)
            {
                Fail("단어를 저장하지 못했어요. 다시 시도해 주세요.");
                return VocabularySaveResult.Failed;
            }

            LastFailureMessage = null;
            if (!saved.Created)
            {
                return VocabularySaveResult.AlreadySaved;
            }

            // 목록은 최근 저장순이라 맨 앞에 둔다. 받기 전이면 넣지 않고 단어장을 열 때 다시 읽는다.
            if (isLoaded && entries.TrueForAll(entry => entry.UserVocabId != saved.UserVocabId))
            {
                entries.Insert(0, new VocabularyEntry(saved));
            }

            return VocabularySaveResult.Saved;
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
