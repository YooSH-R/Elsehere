using System.Collections.Generic;

namespace A305.World
{
    /// <summary>
    /// 서버 장소 코드와 그 장소의 Unity 씬 이름(서버 scenes.unity_key). 서버 시드(world.sql)의 장소와 같아야 한다.
    /// 새 장소 씬을 만들면 여기에 함께 등록한다.
    /// </summary>
    public static class PlaceScenes
    {
        private static readonly IReadOnlyDictionary<string, string> ScenesByCode = new Dictionary<
            string,
            string
        >
        {
            { "HOME", "HomeScene" },
            { "TOWN", "TownScene" },
            { "CAFE", "CafeScene" },
            { "HOTEL", "HotelScene" },
            { "SHOP", "ShopScene" },
        };

        // 영업시간 안내("카페는 지금 영업시간이 아니에요")에 쓰는 짧은 이름
        private static readonly IReadOnlyDictionary<string, string> NamesByCode = new Dictionary<
            string,
            string
        >
        {
            { "HOME", "집" },
            { "TOWN", "마을" },
            { "CAFE", "카페" },
            { "HOTEL", "호텔" },
            { "SHOP", "드럭스토어" },
        };

        /// <summary>장소의 Unity 씬 이름. 씬이 없는 장소면 null.</summary>
        public static string SceneOf(string placeCode) =>
            placeCode != null && ScenesByCode.TryGetValue(placeCode, out string scene)
                ? scene
                : null;

        /// <summary>안내 문구에 쓰는 장소의 한국어 이름. 씬이 없는 장소면 null.</summary>
        public static string NameOf(string placeCode) =>
            placeCode != null && NamesByCode.TryGetValue(placeCode, out string name) ? name : null;

        /// <summary>Unity 씬이 나타내는 장소 코드. 장소 씬이 아니면 null.</summary>
        public static string CodeOf(string unitySceneName)
        {
            foreach (KeyValuePair<string, string> place in ScenesByCode)
            {
                if (place.Value == unitySceneName)
                    return place.Key;
            }

            return null;
        }
    }
}
