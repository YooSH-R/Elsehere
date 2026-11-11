using System.Collections.Generic;

namespace A305.World
{
    /// <summary>
    /// 장소 코드와 그 장소의 Unity 씬 이름. 서버 장소(서버 scenes.unity_key)와 마을 매장 실내 씬을 함께 둔다.
    /// 서버는 마을 매장을 마을(TOWN)의 일부로 보므로, 서버와 주고받는 장소 코드는 <see cref="ServerCodeOf"/>로 찾는다.
    /// 새 장소 씬을 만들면 여기에 함께 등록하고, 서버 장소면 서버 시드(world.sql)에도 더한다.
    /// </summary>
    public static class PlaceScenes
    {
        private const string Town = "TOWN";

        // 서버 장소. 서버 시드(world.sql)의 장소와 같아야 한다.
        private static readonly IReadOnlyDictionary<string, string> ServerScenesByCode =
            new Dictionary<string, string>
            {
                { "HOME", "HomeScene" },
                { Town, "TownScene" },
                { "CAFE", "CafeScene" },
                { "HOTEL", "HotelScene" },
                { "SHOP", "ShopScene" },
                { "POLICE_STATION", "PoliceStationScene" },
            };

        // 마을 매장 실내 씬(Scenes/ShopInteriors). 코드는 영업시간 파일(Data/World/PlaceHours.json)·택시 목적지와 같다.
        private static readonly IReadOnlyDictionary<string, string> TownShopScenesByCode =
            new Dictionary<string, string>
            {
                { "POST_OFFICE", "PostOfficeInterior" },
                { "DINER", "MapleDinerInterior" },
                { "BANK", "TownBankInterior" },
                { "LAUNDROMAT", "SudsLaundromatInterior" },
                { "FLOWER_SHOP", "BloomFlowersInterior" },
                { "BARBER", "MainStreetBarberInterior" },
                { "BAKERY", "MapleBakeryInterior" },
                { "ARCADE", "LittleArcadeInterior" },
                { "HOME_STORE", "HomeLivingInterior" },
            };

        // 같은 건물 2층 가게(서점·찻집). 장소 코드가 따로 없어 그 건물(1층 매장)의 영업시간을 따른다.
        private static readonly IReadOnlyDictionary<string, string> UpperFloorShopCodesByScene =
            new Dictionary<string, string>
            {
                { "LittleArcadeUpperInterior", "ARCADE" },
                { "HomeLivingUpperInterior", "HOME_STORE" },
            };

        // 영업시간 안내("카페는 지금 영업시간이 아니에요")에 쓰는 짧은 이름
        private static readonly IReadOnlyDictionary<string, string> NamesByCode = new Dictionary<
            string,
            string
        >
        {
            { "HOME", "집" },
            { Town, "마을" },
            { "CAFE", "카페" },
            { "HOTEL", "호텔" },
            { "SHOP", "드럭스토어" },
            { "POLICE_STATION", "경찰서" },
            { "POST_OFFICE", "우체국" },
            { "DINER", "메이플 식당" },
            { "BANK", "타운 은행" },
            { "LAUNDROMAT", "빨래방" },
            { "FLOWER_SHOP", "꽃집" },
            { "BARBER", "이발소" },
            { "BAKERY", "베이커리" },
            { "ARCADE", "오락실" },
            { "HOME_STORE", "생활용품점" },
        };

        /// <summary>장소의 Unity 씬 이름. 한 건물의 1층·2층 가게가 코드를 함께 쓰면 1층 씬이다. 씬이 없는 장소면 null.</summary>
        public static string SceneOf(string placeCode)
        {
            if (placeCode == null)
                return null;
            if (ServerScenesByCode.TryGetValue(placeCode, out string scene))
                return scene;
            return TownShopScenesByCode.TryGetValue(placeCode, out scene) ? scene : null;
        }

        /// <summary>안내 문구에 쓰는 장소의 한국어 이름. 씬이 없는 장소면 null.</summary>
        public static string NameOf(string placeCode) =>
            placeCode != null && NamesByCode.TryGetValue(placeCode, out string name) ? name : null;

        /// <summary>Unity 씬이 나타내는 장소 코드(영업시간·길 안내에 쓴다). 장소 씬이 아니면 null.</summary>
        public static string CodeOf(string unitySceneName)
        {
            if (unitySceneName == null)
                return null;
            if (UpperFloorShopCodesByScene.TryGetValue(unitySceneName, out string upperFloor))
                return upperFloor;
            return CodeIn(ServerScenesByCode, unitySceneName)
                ?? CodeIn(TownShopScenesByCode, unitySceneName);
        }

        /// <summary>
        /// 서버와 주고받는 장소 코드(재접속 위치·사물 카드). 서버는 마을 매장을 마을의 일부로 보므로 매장 실내 씬은 TOWN이다.
        /// 장소 씬이 아니면 null.
        /// </summary>
        public static string ServerCodeOf(string unitySceneName)
        {
            if (IsTownShop(unitySceneName))
                return Town;
            return unitySceneName == null ? null : CodeIn(ServerScenesByCode, unitySceneName);
        }

        /// <summary>마을 매장 실내 씬(2층 가게 포함)인지</summary>
        private static bool IsTownShop(string unitySceneName) =>
            unitySceneName != null
            && (
                UpperFloorShopCodesByScene.ContainsKey(unitySceneName)
                || CodeIn(TownShopScenesByCode, unitySceneName) != null
            );

        private static string CodeIn(
            IReadOnlyDictionary<string, string> scenesByCode,
            string unitySceneName
        )
        {
            foreach (KeyValuePair<string, string> place in scenesByCode)
            {
                if (place.Value == unitySceneName)
                    return place.Key;
            }

            return null;
        }
    }
}
