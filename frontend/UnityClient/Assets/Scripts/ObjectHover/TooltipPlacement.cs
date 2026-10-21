using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>툴팁을 사물 위에 두되 화면 밖으로 나가지 않게 자리를 정한다. 좌표는 캔버스 왼쪽 아래 기준이다.</summary>
    public static class TooltipPlacement
    {
        /// <param name="anchor">툴팁을 붙일 지점(사물 위쪽)</param>
        /// <param name="gap">지점과 툴팁 아래쪽 사이 간격</param>
        /// <param name="margin">캔버스 가장자리와 툴팁 사이에 남길 간격</param>
        /// <returns>툴팁의 왼쪽 아래 위치. 툴팁 아래 가운데가 지점 위에 오도록 두고 margin 안으로 당긴다.</returns>
        public static Vector2 PlaceAbove(
            Vector2 anchor,
            Vector2 tooltipSize,
            Vector2 canvasSize,
            float gap,
            float margin
        )
        {
            float x = anchor.x - tooltipSize.x / 2f;
            float y = anchor.y + gap;

            float maxX = canvasSize.x - margin - tooltipSize.x;
            float maxY = canvasSize.y - margin - tooltipSize.y;
            // 툴팁이 캔버스보다 크면 왼쪽·아래 margin에 맞춘다.
            x = Mathf.Max(margin, Mathf.Min(x, maxX));
            y = Mathf.Max(margin, Mathf.Min(y, maxY));

            return new Vector2(x, y);
        }
    }
}
