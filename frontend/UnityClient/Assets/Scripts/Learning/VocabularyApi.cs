using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>단어장 API의 경로와 본문. 경로는 API 명세(docs/api/mvp-api.md)를 따른다.</summary>
    public sealed class VocabularyApi : IVocabularyApi
    {
        // 단어장 화면은 외운 단어도 '외웠어요' 표시와 함께 보여 주므로 외운 단어를 포함해 읽는다.
        private const string ListPath = "/api/v1/vocab?includeMemorized=true&size=50";

        private readonly ApiClient client;

        public VocabularyApi(ApiClient client)
        {
            this.client = client;
        }

        public Task<VocabPage> GetPageAsync(string cursor)
        {
            if (cursor == null)
            {
                return client.GetAsync<VocabPage>(ListPath);
            }

            return client.GetAsync<VocabPage>(ListPath + "&cursor=" + Uri.EscapeDataString(cursor));
        }

        public Task<VocabItem> UpdateMemoAsync(long userVocabId, string memo)
        {
            return client.PatchAsync<VocabItem>(
                EntryPath(userVocabId),
                new VocabUpdateRequest { Memo = memo }
            );
        }

        public Task<VocabItem> UpdateMemorizedAsync(long userVocabId, bool memorized)
        {
            return client.PatchAsync<VocabItem>(
                EntryPath(userVocabId),
                new VocabUpdateRequest { Memorized = memorized }
            );
        }

        public Task DeleteAsync(long userVocabId)
        {
            return client.DeleteAsync(EntryPath(userVocabId));
        }

        private static string EntryPath(long userVocabId)
        {
            return "/api/v1/vocab/" + userVocabId;
        }

        // VOCAB-03은 보낸 필드만 바꾸므로 바꾸지 않는 값은 null로 두어 본문에서 뺀다.
        private sealed class VocabUpdateRequest
        {
            public string Memo { get; set; }

            public bool? Memorized { get; set; }
        }
    }
}
