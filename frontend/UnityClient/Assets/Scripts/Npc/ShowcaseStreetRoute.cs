using UnityEngine;

namespace A305.Npc
{
    /// <summary>시연 동선 반대편 보도에서만 걷는 두 주민의 짧은 왕복 경로.</summary>
    public static class ShowcaseStreetRoute
    {
        public static bool AppliesTo(bool showcase, string scene) =>
            showcase && scene == "TownScene";

        public static bool KeepsHotelWorker(string npcCode) => npcCode == "HOTEL_STAFF";

        public static bool TryGet(string npcCode, out Vector3 start, out Vector3 end)
        {
            switch (npcCode)
            {
                case "TOWN_GRANDFATHER":
                    start = new Vector3(-4.8f, .1f, 23);
                    end = new Vector3(-4.8f, .1f, 27);
                    return true;
                case "TOWN_GRANDMOTHER":
                    start = new Vector3(-4.8f, .1f, 43);
                    end = new Vector3(-4.8f, .1f, 47);
                    return true;
                default:
                    start = end = Vector3.zero;
                    return false;
            }
        }
    }
}
