using System;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 수첩의 마을 지도. 길과 장소 칸은 Builder가 실제 마을 배치로 그려 두고,
    /// 이 컴포넌트는 내 위치 화살표와 지금 가야 할 장소 표시만 바꾼다.
    /// </summary>
    public sealed class TownMapView : MonoBehaviour
    {
        private const string TownSceneCode = "TOWN";

        [SerializeField]
        [Tooltip(
            "지도에 담는 마을 바닥 범위. x·width는 서쪽 끝 X와 동서 폭, y·height는 남쪽 끝 Z와 남북 길이"
        )]
        private Rect worldArea;

        [SerializeField]
        private RectTransform playerMarker;

        [SerializeField]
        private TownMapPlace[] places = Array.Empty<TownMapPlace>();

        private TownMap map;
        private string sceneCode;
        private Transform player;
        private TownMapPlace destination;

        /// <summary>마을에서는 플레이어 위치와 방향을 따라가고, 건물 안에서는 그 건물 칸에 내 위치를 둔다.</summary>
        public void Bind(string currentSceneCode, Transform playerTransform)
        {
            sceneCode = currentSceneCode;
            player = playerTransform;
            Redraw();
        }

        /// <summary>목적지가 마을 거리이거나 없으면 어느 건물도 표시하지 않는다.</summary>
        public void ShowDestination(string destinationSceneCode)
        {
            destination = Array.Find(places, place => place.SceneCode == destinationSceneCode);
            foreach (TownMapPlace place in places)
                place.SetDestination(place == destination);
        }

        public void Redraw()
        {
            map ??= new TownMap(worldArea);
            if (sceneCode == TownSceneCode && player != null)
            {
                MoveMarker(map.ToMap(player.position), map.ToMapRotation(player.eulerAngles.y));
                return;
            }
            // 건물 안에서는 그 건물 출입문에서 안쪽을 가리켜 들어와 있음을 보여 준다.
            TownMapPlace here = Array.Find(places, place => place.SceneCode == sceneCode);
            playerMarker.gameObject.SetActive(here != null);
            if (here != null)
                MoveMarker(here.Entrance, TownMap.Pointing(here.Entrance, here.Centre));
        }

        private void MoveMarker(Vector2 mapPoint, float rotationDegrees)
        {
            playerMarker.gameObject.SetActive(true);
            playerMarker.anchorMin = playerMarker.anchorMax = mapPoint;
            playerMarker.anchoredPosition = Vector2.zero;
            playerMarker.localEulerAngles = new Vector3(0f, 0f, rotationDegrees);
        }

        private void LateUpdate()
        {
            Redraw();
            // 목적지 테두리를 천천히 키웠다 줄여 다른 장소와 구분한다.
            destination?.Pulse(1f + .06f * Mathf.Sin(Time.unscaledTime * 4f));
        }
    }

    [Serializable]
    public sealed class TownMapPlace
    {
        [SerializeField]
        private string sceneCode;

        [SerializeField]
        [Tooltip("지도 위 건물 칸. 앵커가 건물이 차지한 지도 비율 범위다")]
        private RectTransform area;

        [SerializeField]
        [Tooltip("마을 쪽 출입문 위치. 앵커가 지도 비율 좌표다")]
        private RectTransform entrance;

        [SerializeField]
        private RectTransform destinationMark;

        public string SceneCode => sceneCode;

        public Vector2 Centre => (area.anchorMin + area.anchorMax) / 2f;

        public Vector2 Entrance => entrance.anchorMin;

        public void SetDestination(bool isDestination) =>
            destinationMark.gameObject.SetActive(isDestination);

        public void Pulse(float scale) =>
            destinationMark.localScale = new Vector3(scale, scale, 1f);
    }
}
