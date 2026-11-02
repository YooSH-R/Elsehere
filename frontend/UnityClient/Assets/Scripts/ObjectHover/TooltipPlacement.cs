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
        /// 바깥의 위·아래·오른쪽·왼쪽도 놓아 본다. 모든 자리가 덮으면 각 자리를 덮은 영역 밖으로 왼쪽·오른쪽·아래·위로 옮겨 보고
        /// 빈자리 중 포인터에 가장 가까운 곳을 고른다. 빈자리가 없을 때만 덮는 넓이가 가장 작은 자리를 고른다.
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

            // 화면 오른쪽 끝에서 메뉴(≡)를 펼친 때처럼 둘레가 모두 UI로 막혀도 화면의 다른 빈자리로 비킨다.
            var obstacles = new List<Rect>(avoidAreas) { pointed };
            Vector2? clear = null;
            float closest = float.PositiveInfinity;
            foreach (Vector2 candidate in candidates)
            {
                foreach (Direction direction in Directions)
                {
                    if (
                        !TryMoveClear(
                            candidate,
                            direction,
                            tooltipSize,
                            canvasSize,
                            gap,
                            margin,
                            obstacles,
                            out Vector2 moved
                        )
                    )
                    {
                        continue;
                    }

                    float distance = (moved + tooltipSize / 2f - pointer).sqrMagnitude;
                    if (distance < closest)
                    {
                        clear = moved;
                        closest = distance;
                    }
                }
            }

            return clear ?? best;
        }

        private enum Direction
        {
            Left,
            Right,
            Down,
            Up,
        }

        private static readonly Direction[] Directions =
        {
            Direction.Left,
            Direction.Right,
            Direction.Down,
            Direction.Up,
        };

        // 자리를 한 방향으로, 겹친 영역 너머(간격 포함)까지 거듭 옮겨 아무것도 덮지 않는 자리를 찾는다.
        // 옮긴 자리가 또 다른 영역에 걸리면 그 너머로 다시 옮기고, 화면 가장자리 밖으로 나가면 빈자리가 없다고 본다.
        private static bool TryMoveClear(
            Vector2 start,
            Direction direction,
            Vector2 tooltipSize,
            Vector2 canvasSize,
            float gap,
            float margin,
            IReadOnlyList<Rect> obstacles,
            out Vector2 position
        )
        {
            float maxX = canvasSize.x - margin - tooltipSize.x;
            float maxY = canvasSize.y - margin - tooltipSize.y;
            position = start;
            while (
                position.x >= margin
                && position.x <= maxX
                && position.y >= margin
                && position.y <= maxY
            )
            {
                Rect tooltip = new Rect(position, tooltipSize);
                Vector2 next = position;
                bool blocked = false;
                foreach (Rect obstacle in obstacles)
                {
                    if (OverlapArea(tooltip, obstacle) > 0f)
                    {
                        next = MovePast(next, obstacle, tooltipSize, gap, direction);
                        blocked = true;
                    }
                }

                if (!blocked)
                {
                    return true;
                }

                position = next;
            }

            return false;
        }

        // 영역 너머로 옮긴 자리. 여러 영역에 걸리면 가장 멀리 옮긴 값이 남는다.
        private static Vector2 MovePast(
            Vector2 position,
            Rect obstacle,
            Vector2 tooltipSize,
            float gap,
            Direction direction
        )
        {
            return direction switch
            {
                Direction.Left => new Vector2(
                    Mathf.Min(position.x, obstacle.xMin - gap - tooltipSize.x),
                    position.y
                ),
                Direction.Right => new Vector2(
                    Mathf.Max(position.x, obstacle.xMax + gap),
                    position.y
                ),
                Direction.Down => new Vector2(
                    position.x,
                    Mathf.Min(position.y, obstacle.yMin - gap - tooltipSize.y)
                ),
                _ => new Vector2(position.x, Mathf.Max(position.y, obstacle.yMax + gap)),
            };
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
