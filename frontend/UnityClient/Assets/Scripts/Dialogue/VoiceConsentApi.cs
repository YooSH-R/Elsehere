using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>선택 동의 API의 경로. 경로는 API 명세(docs/api/mvp-api.md USER-02·03)를 따른다.</summary>
    public sealed class VoiceConsentApi : IVoiceConsentApi
    {
        private const string ConsentsPath = "/api/v1/me/consents";
        private const string VoiceConsentType = "VOICE";

        private readonly ApiClient client;

        public VoiceConsentApi(ApiClient client)
        {
            this.client = client;
        }

        public Task AgreeAsync()
        {
            return client.PutAsync<ConsentChange>(
                ConsentsPath,
                new ConsentChange { ConsentType = VoiceConsentType, Agreed = true }
            );
        }

        // 요청 본문이자 응답에서 읽는 칸. 응답의 제목·버전 같은 나머지 칸은 쓰지 않는다.
        private sealed class ConsentChange
        {
            public string ConsentType { get; set; }
            public bool Agreed { get; set; }
        }
    }
}
