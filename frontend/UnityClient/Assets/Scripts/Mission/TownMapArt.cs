using System.Collections.Generic;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 시안 스타일로 그린 마을 지도 그림(<c>Art/UI/Map/TownMap.png</c>)의 좌표. 그림은 실제 마을 배치를 1m = 20단위로 그렸고,
    /// 큰길이 가로로 놓이도록 북쪽이 오른쪽·서쪽이 위다(왼쪽 위가 0, 아래로 갈수록 y가 커진다).
    /// 마을 위치를 그림 좌표로 바꾸고, 미니맵 창 자리와 내 위치 화살표 방향, 미션 장소 칸과 출입문 자리를 정한다.
    /// </summary>
    public static class TownMapArt
    {
        private const float UnitsPerMeter = 20;

        // 그림 왼쪽 위 모서리의 마을 좌표: 큰길 남쪽 끝(z)과 교차로 서쪽 끝(x)
        private const float SouthZ = -32;
        private const float WestX = -20;

        /// <summary>지도 그림 크기(단위). 남북 100m, 동서 40m</summary>
        public static readonly Vector2 Size = new Vector2(2000, 800);

        // 북쪽 거리의 두 줄은 모두 TownScene에 있다. 단계 번호 대신 담당 직원으로 가게를 찾으면 재방문도 같은 칸을 쓴다.
        public static readonly IReadOnlyDictionary<string, (string Name, Rect Tile)> Shops =
            new Dictionary<string, (string, Rect)>
            {
                ["BAKERY_STAFF"] = ("빵집", new Rect(984, 106, 152, 150)),
                ["POST_OFFICE_STAFF"] = ("우체국", new Rect(1164, 106, 152, 150)),
                ["HOME_STORE_STAFF"] = ("생활용품점", new Rect(1344, 106, 152, 150)),
                ["BANK_STAFF"] = ("은행", new Rect(1524, 106, 152, 150)),
                ["FLOWER_SHOP_STAFF"] = ("꽃집", new Rect(1704, 106, 152, 150)),
                ["ARCADE_STAFF"] = ("오락실", new Rect(1164, 544, 152, 150)),
                ["DINER_STAFF"] = ("식당", new Rect(1344, 544, 152, 150)),
                ["LAUNDROMAT_STAFF"] = ("빨래방", new Rect(1524, 544, 152, 150)),
                ["BARBER_STAFF"] = ("이발소", new Rect(1704, 544, 152, 150)),
            };

        public static Vector2 ToArt(Vector3 world) =>
            new Vector2((world.z - SouthZ) * UnitsPerMeter, (world.x - WestX) * UnitsPerMeter);

        public static Vector3 ToWorld(Vector2 art) =>
            new Vector3(WestX + art.y / UnitsPerMeter, 0, SouthZ + art.x / UnitsPerMeter);

        /// <summary>내 위치를 가운데에 둔 창의 왼쪽 위. 창이 지도 밖으로 나가지 않게 가장자리에서 멈춘다.</summary>
        public static Vector2 Window(Vector2 centre, Vector2 window) =>
            new Vector2(
                Mathf.Clamp(centre.x - window.x / 2f, 0, Size.x - window.x),
                Mathf.Clamp(centre.y - window.y / 2f, 0, Size.y - window.y)
            );

        /// <summary>위를 가리키는 화살표 그림을 바라보는 방향(북쪽 0, 동쪽 90)으로 돌릴 UI 회전값. 지도는 북쪽이 오른쪽이다.</summary>
        public static float ArrowRotation(float yawDegrees) => -90f - yawDegrees;

        /// <summary>위를 가리키는 화살표 그림이 그림 좌표 from에서 to를 향하도록 돌릴 UI 회전값.</summary>
        public static float Pointing(Vector2 from, Vector2 to) =>
            Mathf.Atan2(from.y - to.y, to.x - from.x) * Mathf.Rad2Deg - 90f;

        /// <summary>장소(씬 코드나 가게를 맡은 NPC 키)의 한국어 이름. 지도 범례와 기다리는 카드가 쓴다. 모르는 장소면 null이다.</summary>
        public static string PlaceName(string place) =>
            place switch
            {
                "CAFE" => "카페",
                "HOTEL" => "호텔",
                "SHOP" => "드럭스토어",
                "HOME" => "집",
                not null when Shops.TryGetValue(place, out var shop) => shop.Name,
                _ => null,
            };

        /// <summary>미션 장소 칸(그림에 그린 건물 칸). 마을 거리처럼 칸이 없으면 null이다.</summary>
        public static Rect? Tile(string destination) =>
            destination switch
            {
                "HOME" => new Rect(230, 590, 140, 140),
                "CAFE" => new Rect(382, 106, 156, 150),
                "SHOP" => new Rect(550, 106, 146, 150),
                "HOTEL" => new Rect(510, 550, 180, 180),
                not null when Shops.TryGetValue(destination, out var shop) => shop.Tile,
                _ => null,
            };

        /// <summary>장소 출입문 자리. 마을의 출입문 위치를 그림 좌표로 옮긴 값이다.</summary>
        public static Vector2? Door(string sceneCode) =>
            sceneCode switch
            {
                "HOME" => ToArt(new Vector3(8.95f, 0, -19f)),
                "CAFE" => ToArt(new Vector3(-6.95f, 0, -11f)),
                "SHOP" => ToArt(new Vector3(-6.93f, 0, -1f)),
                "HOTEL" => ToArt(new Vector3(6.95f, 0, -5f)),
                _ => null,
            };
    }
}
