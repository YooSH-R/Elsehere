using A305.Npc;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 아래 미니맵. 둥근 창 안에서 마을 지도 그림을 움직여 내 위치가 가운데 오게 하고,
    /// 그 위에 내 위치 화살표와 지금 가야 할 장소 테두리, [이웃]에서 찾는 NPC 핀(<see cref="NpcTracking"/>)을 얹는다.
    /// 좌표는 <see cref="TownMapArt"/>를 따른다.
    /// 큰 지도도 같은 화면을 쓴다. 창이 지도 그림 전체 크기라 그림은 움직이지 않고 표시만 따라간다.
    /// 끌면 지도가 커서를 따라 움직여 주변을 둘러볼 수 있고(<see cref="MiniMapPan"/>), 끌고 놓았을 때는 큰 지도를 열지 않는다.
    /// </summary>
    public sealed class TownMiniMapView
        : MonoBehaviour,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
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

        [SerializeField]
        [Tooltip("[이웃]에서 찾는 NPC 핀. 자식 Letter(이름 첫 글자)와 Name/Label(이름)을 쓴다")]
        private RectTransform neighbor;

        private readonly MiniMapPan pan = new MiniMapPan();
        private string sceneCode;
        private Transform follow;
        private string shownNeighborName;

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
            ShowNeighbor();
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
            Vector2 corner = TownMapArt.Window(pan.Centre(point, Time.unscaledTime), Window);
            map.anchoredPosition = new Vector2(-corner.x, corner.y) * map.localScale.x;
        }

        // 찾는 NPC를 지도에 찍는다. 마을에 켜진 몸은 그대로 따라가고, 몸이 없으면 일과로 일터 칸에 둔다(NpcLocator).
        // 숙소에서 쉬거나 걸어 다녀 자리를 모르면 핀을 숨긴다.
        private void ShowNeighbor()
        {
            if (neighbor == null)
                return;
            NpcTracking tracking = NpcTracking.Shared;
            Vector2? point =
                tracking.Code != null ? TownMapArt.NeighborPoint(Locate(tracking)) : null;
            if (neighbor.gameObject.activeSelf != point.HasValue)
                neighbor.gameObject.SetActive(point.HasValue);
            if (!point.HasValue)
                return;
            neighbor.anchoredPosition = OnMap(point.Value);
            if (shownNeighborName == tracking.Name)
                return;
            shownNeighborName = tracking.Name;
            string name = string.IsNullOrEmpty(tracking.Name) ? "?" : tracking.Name;
            neighbor.Find("Letter").GetComponent<TMP_Text>().text = name.Substring(0, 1)
                .ToUpperInvariant();
            neighbor.Find("Name/Label").GetComponent<TMP_Text>().text = name;
        }

        private NpcLocation Locate(NpcTracking tracking)
        {
            NpcIdentity body = NpcTracking.FindBody(tracking.Code);
            return NpcLocator.Locate(
                tracking.Life,
                GameClock.Shared.Minutes,
                NpcDuty.RequiresPresence(tracking.Code),
                sceneCode == TownSceneCode,
                body != null ? body.transform.position : (Vector3?)null
            );
        }

        // 창 크기와 지금 창에 보이는 가운데(그림 단위)
        private Vector2 Window => ((RectTransform)map.parent).rect.size / map.localScale.x;

        private Vector2 Shown =>
            new Vector2(-map.anchoredPosition.x, map.anchoredPosition.y) / map.localScale.x
            + Window / 2f;

        // 지도 그림 왼쪽 위를 기준으로 놓인 표시의 자리. 그림 좌표는 아래로 갈수록 커진다.
        private static Vector2 OnMap(Vector2 point) => new Vector2(point.x, -point.y);

        // 판 전체가 큰 지도를 여는 버튼이라, 끌기 시작하면 놓을 때 누르기로 치지 않는다.
        public void OnBeginDrag(PointerEventData eventData) => eventData.eligibleForClick = false;

        public void OnDrag(PointerEventData eventData)
        {
            float screenPerArt =
                GetComponentInParent<Canvas>().rootCanvas.scaleFactor * map.localScale.x;
            // 화면은 위로, 그림은 아래로 갈수록 y가 커진다.
            Vector2 cursorMove = new Vector2(eventData.delta.x, -eventData.delta.y) / screenPerArt;
            pan.Drag(Shown, cursorMove, Window);
        }

        public void OnEndDrag(PointerEventData eventData) => pan.Release(Time.unscaledTime);

        private void LateUpdate() => Redraw();
    }
}
