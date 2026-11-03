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

        /// <summary>장소의 Unity 씬 이름. 씬이 없는 장소면 null.</summary>
        public static string SceneOf(string placeCode) =>
            placeCode != null && ScenesByCode.TryGetValue(placeCode, out string scene)
                ? scene
                : null;

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
