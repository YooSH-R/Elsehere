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
        private const string SavePath = "/api/v1/vocab";

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

        public Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text)
        {
            // 서버는 요청 ID를 쓰지 않고 이미 저장한 단어면 처음 항목을 돌려주므로(VOCAB-02) 요청마다 새로 만든다.
            return client.PostAsync<VocabItem>(
                SavePath,
                new NpcLineSaveRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    SourceType = "NPC_LINE",
                    TurnId = turnId,
                    Text = text,
                }
            );
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

        private sealed class NpcLineSaveRequest
        {
            public string RequestId { get; set; }

            public string SourceType { get; set; }

            public long TurnId { get; set; }

            public string Text { get; set; }
        }

        // VOCAB-03은 보낸 필드만 바꾸므로 바꾸지 않는 값은 null로 두어 본문에서 뺀다.
        private sealed class VocabUpdateRequest
        {
            public string Memo { get; set; }

            public bool? Memorized { get; set; }
        }
    }
}
