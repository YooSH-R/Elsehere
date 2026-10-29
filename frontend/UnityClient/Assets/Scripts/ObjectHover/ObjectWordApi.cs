using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.ObjectHover
{
    /// <summary>사물 카드 API의 경로와 본문. 경로는 API 명세(docs/api/mvp-api.md)를 따른다.</summary>
    public sealed class ObjectWordApi : IObjectWordApi
    {
        private readonly ApiClient client;

        public ObjectWordApi(ApiClient client)
        {
            this.client = client;
        }

        /// <summary>WORLD-04 발음 음성 경로. 음성 파일이라 JSON API 대신 오디오 요청으로 받는다.</summary>
        public static string PronunciationPath(long vocabId)
        {
            return "/api/v1/vocab/" + vocabId + "/pronunciation";
        }

        public Task<SceneWords> GetSceneWordsAsync(string sceneCode)
        {
            return client.GetAsync<SceneWords>("/api/v1/scenes/" + Uri.EscapeDataString(sceneCode));
        }

        public Task SaveAsync(string wordKey, string requestId)
        {
            return client.PostAsync(
                "/api/v1/vocab",
                new SaveRequest
                {
                    RequestId = requestId,
                    SourceType = "OBJECT",
                    WordKey = wordKey,
                }
            );
        }

        public Task SetHoverHiddenAsync(string wordKey, bool hoverHidden)
        {
            return client.PutAsync<WordPreference>(
                "/api/v1/me/word-prefs/" + Uri.EscapeDataString(wordKey),
                new WordPreference { HoverHidden = hoverHidden }
            );
        }

        private sealed class SaveRequest
        {
            public string RequestId { get; set; }

            public string SourceType { get; set; }

            public string WordKey { get; set; }
        }

        // 요청은 hoverHidden만 보내고, 응답은 wordKey를 더해 돌려준다.
        private sealed class WordPreference
        {
            public string WordKey { get; set; }

            public bool HoverHidden { get; set; }
        }
    }
}
