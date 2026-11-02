using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>툴팁을 가리킨 곳 가까이에 두되 화면 밖으로 나가지 않게 자리를 정한다. 좌표는 캔버스 왼쪽 아래 기준이다.</summary>
    public static class TooltipPlacement
    {
        /// <param name="anchor">마우스 포인터 위치</param>
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
            return ClampInside(
                new Vector2(anchor.x - tooltipSize.x / 2f, anchor.y + gap),
                tooltipSize,
                canvasSize,
                margin
            );
        }

        /// <summary>
        /// 가리킨 영역의 위·아래·오른쪽·왼쪽 순서로 툴팁을 놓아 보고, 가리킨 영역과 피할 영역을 덮지 않는 첫 자리를 고른다.
        /// 캐릭터 옆 바닥처럼 포인터가 피할 영역 안에 있으면 그 둘레 네 자리가 모두 덮으므로, 가리킨 영역과 그 피할 영역을 함께 감싼
        /// 바깥의 위·아래·오른쪽·왼쪽도 놓아 본다. 모든 자리가 덮으면 덮는 넓이가 가장 작은 자리를 고른다.
        /// 위·아래에서는 가로로, 양옆에서는 세로로 포인터를 따라간다.
        /// </summary>
        /// <param name="pointer">마우스 포인터 위치</param>
        /// <param name="pointedArea">가리킨 사물이 화면에서 차지하는 영역. 크기가 0이면 포인터 한 점만 피한다.</param>
        /// <param name="avoidAreas">툴팁이 덮지 않을 다른 영역(다른 화면, 캐릭터)</param>
        /// <returns>툴팁의 왼쪽 아래 위치</returns>
        public static Vector2 PlaceAround(
            Vector2 pointer,
            Rect pointedArea,
            Vector2 tooltipSize,
            Vector2 canvasSize,
            float gap,
            float margin,
            IReadOnlyList<Rect> avoidAreas
        )
        {
            var candidates = new List<Vector2>(
                AroundArea(pointer, pointedArea, tooltipSize, canvasSize, gap, margin)
            );
            foreach (Rect area in avoidAreas)
            {
                if (area.Contains(pointer))
                {
                    candidates.AddRange(
                        AroundArea(
                            pointer,
                            Enclosing(pointedArea, area),
                            tooltipSize,
                            canvasSize,
                            gap,
                            margin
                        )
                    );
                }
            }

            // 한 점을 가리켜도 포인터를 덮지 않도록 가리킨 영역을 간격의 절반만큼 넓혀 본다.
            Rect pointed = new Rect(
                pointedArea.x - gap / 2f,
                pointedArea.y - gap / 2f,
                pointedArea.width + gap,
                pointedArea.height + gap
            );
            Vector2 best = candidates[0];
            float least = float.PositiveInfinity;
            foreach (Vector2 candidate in candidates)
            {
                float covered = CoveredArea(new Rect(candidate, tooltipSize), pointed, avoidAreas);
                if (covered <= 0f)
                {
                    return candidate;
                }

                if (covered < least)
                {
                    best = candidate;
                    least = covered;
                }
            }

            return best;
        }

        // 영역의 위·아래·오른쪽·왼쪽 자리. 위·아래에서는 가로로, 양옆에서는 세로로 포인터를 따라간다.
        private static Vector2[] AroundArea(
            Vector2 pointer,
            Rect area,
            Vector2 tooltipSize,
            Vector2 canvasSize,
            float gap,
            float margin
        )
        {
            float sideY = pointer.y - tooltipSize.y / 2f;
            return new[]
            {
                PlaceAbove(new Vector2(pointer.x, area.yMax), tooltipSize, canvasSize, gap, margin),
                ClampInside(
                    new Vector2(pointer.x - tooltipSize.x / 2f, area.yMin - gap - tooltipSize.y),
                    tooltipSize,
                    canvasSize,
                    margin
                ),
                ClampInside(new Vector2(area.xMax + gap, sideY), tooltipSize, canvasSize, margin),
                ClampInside(
                    new Vector2(area.xMin - gap - tooltipSize.x, sideY),
                    tooltipSize,
                    canvasSize,
                    margin
                ),
            };
        }

        private static Rect Enclosing(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax)
            );

        // 툴팁이 캔버스보다 크면 왼쪽·아래 margin에 맞춘다.
        private static Vector2 ClampInside(
            Vector2 bottomLeft,
            Vector2 tooltipSize,
            Vector2 canvasSize,
            float margin
        )
        {
            float maxX = canvasSize.x - margin - tooltipSize.x;
            float maxY = canvasSize.y - margin - tooltipSize.y;
            return new Vector2(
                Mathf.Max(margin, Mathf.Min(bottomLeft.x, maxX)),
                Mathf.Max(margin, Mathf.Min(bottomLeft.y, maxY))
            );
        }

        private static float CoveredArea(Rect tooltip, Rect pointed, IReadOnlyList<Rect> avoidAreas)
        {
            float covered = OverlapArea(tooltip, pointed);
            foreach (Rect area in avoidAreas)
            {
                covered += OverlapArea(tooltip, area);
            }

            return covered;
        }

        private static float OverlapArea(Rect a, Rect b)
        {
            float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return width > 0f && height > 0f ? width * height : 0f;
        }
    }
}
