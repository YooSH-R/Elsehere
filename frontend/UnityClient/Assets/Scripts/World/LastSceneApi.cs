using System.Threading.Tasks;
using A305.Api;

namespace A305.World
{
    /// <summary>WORLD-05 <c>PUT /api/v1/me/last-scene</c>.</summary>
    public sealed class LastSceneApi : ILastSceneApi
    {
        private readonly ApiClient client;

        public LastSceneApi(ApiClient client)
        {
            this.client = client;
        }

        public Task SaveAsync(string sceneCode)
        {
            return client.PutAsync<LastSceneBody>(
                "/api/v1/me/last-scene",
                new LastSceneBody { SceneCode = sceneCode }
            );
        }

        // 요청과 응답 본문이 같은 형식이다: { "sceneCode": "CAFE" }
        private sealed class LastSceneBody
        {
            public string SceneCode { get; set; }
        }
    }
}
