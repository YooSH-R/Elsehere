using System.Collections.Generic;
using UnityEngine;

namespace A305.Tutorial
{
    /// <summary>
    /// 코치 마크 화면 계산. 좌표는 캔버스 왼쪽 아래가 원점이다. 밝힐 대상에 여백을 둔 구멍을 만들고,
    /// 구멍 둘레를 위·아래·왼쪽·오른쪽 네 판으로 덮어 구멍 밖은 어둡게 하고 누를 수 없게 한다.
    /// </summary>
    public static class CoachMarkLayout
    {
        /// <summary>대상에 여백을 두르고 캔버스 밖으로 나간 부분을 자른 구멍.</summary>
        public static Rect Hole(Rect target, Vector2 canvasSize, float padding)
        {
            return Rect.MinMaxRect(
                Mathf.Max(0f, target.xMin - padding),
                Mathf.Max(0f, target.yMin - padding),
                Mathf.Min(canvasSize.x, target.xMax + padding),
                Mathf.Min(canvasSize.y, target.yMax + padding)
            );
        }

        /// <summary>구멍을 뺀 캔버스를 겹치지 않게 덮는 아래·위·왼쪽·오른쪽 판.</summary>
        public static Rect[] Shades(Rect hole, Vector2 canvasSize)
        {
            return new[]
            {
                Rect.MinMaxRect(0f, 0f, canvasSize.x, hole.yMin),
                Rect.MinMaxRect(0f, hole.yMax, canvasSize.x, canvasSize.y),
                Rect.MinMaxRect(0f, hole.yMin, hole.xMin, hole.yMax),
                Rect.MinMaxRect(hole.xMax, hole.yMin, canvasSize.x, hole.yMax),
            };
        }

        /// <summary>여러 대상(읽을 대사와 입력칸 등)을 한 구멍으로 밝힐 때 모두 담는 사각형.</summary>
        public static Rect Union(IReadOnlyList<Rect> targets)
        {
            Rect union = targets[0];
            for (int i = 1; i < targets.Count; i++)
            {
                union = Rect.MinMaxRect(
                    Mathf.Min(union.xMin, targets[i].xMin),
                    Mathf.Min(union.yMin, targets[i].yMin),
                    Mathf.Max(union.xMax, targets[i].xMax),
                    Mathf.Max(union.yMax, targets[i].yMax)
                );
            }

            return union;
        }

        /// <summary>장면 속 대상을 가리키는 화살표의 자리와 방향(도, 오른쪽이 0).</summary>
        public readonly struct MarkerPose
        {
            public MarkerPose(Vector2 position, float arrowDegrees)
            {
                Position = position;
                ArrowDegrees = arrowDegrees;
            }

            public Vector2 Position { get; }
            public float ArrowDegrees { get; }
        }

        /// <summary>
        /// 화면 안의 대상은 그 위에서 아래를 가리킨다. 화면 밖이나 카메라 뒤의 대상은 위·옆 가장자리의 안내 카드·메뉴와 겹치지 않게
        /// 대상이 있는 쪽 아래 구석(여백 안쪽)에 두고, 그쪽 옆을 가리켜 어느 쪽으로 돌아봐야 하는지 알린다.
        /// </summary>
        /// <param name="point">대상의 화면 위치(캔버스 왼쪽 아래 기준)</param>
        public static MarkerPose Marker(
            Vector2 point,
            bool isBehindCamera,
            Vector2 canvasSize,
            float margin
        )
        {
            var inner = Rect.MinMaxRect(
                margin,
                margin,
                canvasSize.x - margin,
                canvasSize.y - margin
            );
            if (!isBehindCamera && inner.Contains(point))
            {
                return new MarkerPose(point, -90f);
            }

            // 카메라 뒤의 점은 좌우가 뒤집혀 나온다.
            float fromCenter = point.x - canvasSize.x / 2f;
            bool isOnTheRight = isBehindCamera ? fromCenter <= 0f : fromCenter >= 0f;
            return isOnTheRight
                ? new MarkerPose(new Vector2(inner.xMax, inner.yMin), 0f)
                : new MarkerPose(new Vector2(inner.xMin, inner.yMin), 180f);
        }
    }
}
