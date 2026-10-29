using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.World
{
    /// <summary>
    /// 장소 씬에 도착하면 그 장소를 재접속 위치로 저장한다(WORLD-05). 저장에 실패해도 게임 진행을 막지 않도록 예외를 던지지 않고
    /// 결과로 알린다.
    /// </summary>
    public sealed class LastSceneRecorder
    {
        // Unity 씬 이름(서버 scenes.unity_key)과 서버 장소 코드. 서버 시드(world.sql)의 장소와 같아야 한다.
        private static readonly IReadOnlyDictionary<string, string> SceneCodes = new Dictionary<
            string,
            string
        >
        {
            { "HomeScene", "HOME" },
            { "TownScene", "TOWN" },
            { "CafeScene", "CAFE" },
            { "HotelScene", "HOTEL" },
            { "ShopScene", "SHOP" },
        };

        private readonly ILastSceneApi api;

        public LastSceneRecorder(ILastSceneApi api)
        {
            this.api = api;
        }

        /// <summary>마지막 저장이 실패한 이유. 저장에 실패하지 않았으면 null.</summary>
        public ApiException LastError { get; private set; }

        /// <param name="unitySceneName">도착한 Unity 씬 이름</param>
        public async Task<LastSceneRecordStatus> RecordAsync(string unitySceneName)
        {
            LastError = null;
            if (
                unitySceneName == null
                || !SceneCodes.TryGetValue(unitySceneName, out string sceneCode)
            )
            {
                return LastSceneRecordStatus.NotAPlace;
            }

            try
            {
                await api.SaveAsync(sceneCode);
                return LastSceneRecordStatus.Saved;
            }
            catch (ApiException exception)
            {
                LastError = exception;
                return LastSceneRecordStatus.Failed;
            }
        }
    }
}
