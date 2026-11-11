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
            // 마을 매장 실내 씬. 서버는 매장을 마을의 일부로 보고 매장 사물 단어도 마을 장소로 내보낸다(World.PlaceScenes와 같은 목록).
            { "PostOfficeInterior", "TOWN" },
            { "MapleDinerInterior", "TOWN" },
            { "TownBankInterior", "TOWN" },
            { "SudsLaundromatInterior", "TOWN" },
            { "BloomFlowersInterior", "TOWN" },
            { "MainStreetBarberInterior", "TOWN" },
            { "MapleBakeryInterior", "TOWN" },
            { "LittleArcadeInterior", "TOWN" },
            { "LittleArcadeUpperInterior", "TOWN" },
            { "HomeLivingInterior", "TOWN" },
            { "HomeLivingUpperInterior", "TOWN" },
        };

        public static bool TryGetSceneCode(string unitySceneName, out string sceneCode)
        {
            return SceneCodes.TryGetValue(unitySceneName ?? string.Empty, out sceneCode);
        }
    }
}
