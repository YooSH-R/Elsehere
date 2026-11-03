using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 아래 미니맵. 둥근 창 안에서 마을 지도 그림을 움직여 내 위치가 가운데 오게 하고,
    /// 그 위에 내 위치 화살표와 지금 가야 할 장소 테두리를 얹는다. 좌표는 <see cref="TownMapArt"/>를 따른다.
    /// </summary>
    public sealed class TownMiniMapView : MonoBehaviour
    {
        private const string TownSceneCode = "TOWN";

        // 목적지 테두리 그림은 장소 칸보다 사방 20단위 크다(테두리 12 + 그림 여백 8).
        private const float FrameOutset = 20;

        [SerializeField]
        [Tooltip(
            "창 안에서 움직이는 지도 그림. 크기는 그림 단위이고, 크기 비율(scale)로 줄여 창에 보인다"
        )]
        private RectTransform map;

        [SerializeField]
        [Tooltip("지도 그림 위의 내 위치 화살표. 그림은 위를 가리킨다")]
        private RectTransform player;

        [SerializeField]
        [Tooltip("지도 그림 위의 목적지 테두리")]
        private RectTransform destination;

        private string sceneCode;
        private Transform follow;

        /// <summary>마을에서는 플레이어 위치와 방향을 따라가고, 건물 안에서는 그 건물 출입문에 내 위치를 둔다.</summary>
        public void Bind(string currentSceneCode, Transform playerTransform)
        {
            sceneCode = currentSceneCode;
            follow = playerTransform;
            Redraw();
        }

        /// <summary>목적지가 마을 거리이거나 없으면 테두리를 숨긴다.</summary>
        public void ShowDestination(string destinationSceneCode)
        {
            Rect? tile = TownMapArt.Tile(destinationSceneCode);
            destination.gameObject.SetActive(tile.HasValue);
            if (!tile.HasValue)
                return;
            destination.anchoredPosition = OnMap(tile.Value.center);
            destination.sizeDelta = tile.Value.size + Vector2.one * FrameOutset * 2;
        }

        public void Redraw()
        {
            if (sceneCode == TownSceneCode && follow != null)
            {
                Show(
                    TownMapArt.ToArt(follow.position),
                    TownMapArt.ArrowRotation(follow.eulerAngles.y)
                );
                return;
            }
            Vector2? door = TownMapArt.Door(sceneCode);
            player.gameObject.SetActive(door.HasValue);
            if (door.HasValue)
                Show(
                    door.Value,
                    TownMapArt.Pointing(door.Value, TownMapArt.Tile(sceneCode).Value.center)
                );
        }

        private void Show(Vector2 point, float rotationDegrees)
        {
            player.gameObject.SetActive(true);
            player.anchoredPosition = OnMap(point);
            player.localEulerAngles = new Vector3(0f, 0f, rotationDegrees);
            float scale = map.localScale.x;
            Vector2 window = ((RectTransform)map.parent).rect.size / scale;
            Vector2 corner = TownMapArt.Window(point, window);
            map.anchoredPosition = new Vector2(-corner.x, corner.y) * scale;
        }

        // 지도 그림 왼쪽 위를 기준으로 놓인 표시의 자리. 그림 좌표는 아래로 갈수록 커진다.
        private static Vector2 OnMap(Vector2 point) => new Vector2(point.x, -point.y);

        private void LateUpdate() => Redraw();
    }
}
