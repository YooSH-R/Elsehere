using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 마을 바닥 좌표를 지도 위 비율 좌표(왼쪽 아래 0, 오른쪽 위 1)로 바꾼다.
    /// 남북으로 뻗은 큰길이 가로로 놓이도록 위에서 내려다본 마을을 시계 방향으로 90도 돌려 그린다. 북쪽이 오른쪽이다.
    /// </summary>
    public sealed class TownMap
    {
        private readonly Rect worldArea;

        /// <param name="worldArea">x·width는 서쪽 끝 X와 동서 폭, y·height는 남쪽 끝 Z와 남북 길이.</param>
        public TownMap(Rect worldArea)
        {
            this.worldArea = worldArea;
        }

        /// <summary>지도의 가로 길이를 세로 길이로 나눈 값.</summary>
        public float WidthPerHeight => worldArea.height / worldArea.width;

        /// <summary>지도 밖의 위치는 가장 가까운 지도 가장자리에 둔다.</summary>
        public Vector2 ToMap(Vector3 worldPosition) =>
            new Vector2(
                Mathf.InverseLerp(worldArea.yMin, worldArea.yMax, worldPosition.z),
                Mathf.InverseLerp(worldArea.xMax, worldArea.xMin, worldPosition.x)
            );

        /// <summary>오른쪽(북쪽)을 가리키는 표시를 바라보는 방향에 맞출 UI 회전값. UI는 반시계 방향이 양수다.</summary>
        public float ToMapRotation(float yawDegrees) => -yawDegrees;

        /// <summary>오른쪽을 가리키는 표시가 지도 위 from에서 to를 향하도록 돌릴 UI 회전값.</summary>
        public static float Pointing(Vector2 from, Vector2 to) =>
            Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
    }
}
