using System.Collections.Generic;

namespace A305.ObjectHover
{
    /// <summary>사물 카드를 받을 서버 장소 코드(WORLD-02의 sceneCode)를 Unity 씬 이름으로 찾는다.</summary>
    public static class ObjectWordScenes
    {
        // Unity 씬 이름(서버 scenes.unity_key)과 서버 장소 코드. 서버 시드(world.sql)와 재접속 장소 저장(LastSceneRecorder)의 장소와 같아야 한다.
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
            { "PoliceStationScene", "POLICE_STATION" },
        };

        public static bool TryGetSceneCode(string unitySceneName, out string sceneCode)
        {
            return SceneCodes.TryGetValue(unitySceneName ?? string.Empty, out sceneCode);
        }
    }
}
