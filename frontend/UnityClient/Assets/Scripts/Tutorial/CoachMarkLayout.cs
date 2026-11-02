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
        /// 화면 안의 대상은 그 위에서 아래를 가리킨다. 대상 위 자리가 화면 위로 벗어나거나 위쪽 안내 카드 자리에 걸려도
        /// 대상 몸통이 보이면 몸통을 가리킨다(문 가까이 섰을 때). 둘 다 화면 밖이거나 카메라 뒤면 위·옆 가장자리의 안내 카드·메뉴와
        /// 겹치지 않게 대상이 있는 쪽 아래 구석(여백 안쪽)에 두고, 그쪽 옆을 가리켜 어느 쪽으로 돌아봐야 하는지 알린다.
        /// </summary>
        /// <param name="point">대상 위 화살표 자리의 화면 위치(캔버스 왼쪽 아래 기준)</param>
        /// <param name="bodyPoint">대상 몸통(문 가운데 등)의 화면 위치</param>
        /// <param name="topMargin">위쪽 안내 카드 자리. 여기에 걸린 자리는 화면 밖으로 본다.</param>
        /// <summary>말풍선이 밝힌 대상의 어느 쪽에 있는지</summary>
        public enum BubbleSide
        {
            Right,
            Left,
            Above,
            Below,
        }

        /// <summary>말풍선 자리(캔버스 왼쪽 아래 기준)와 대상을 향하는 꼬리 끝이 붙는 말풍선 가장자리의 점.</summary>
        public readonly struct BubblePose
        {
            public BubblePose(Rect rect, BubbleSide side, Vector2 tail)
            {
                Rect = rect;
                Side = side;
                Tail = tail;
            }

            public Rect Rect { get; }
            public BubbleSide Side { get; }
            public Vector2 Tail { get; }
        }

        // 꼬리가 말풍선 모서리의 둥근 곳에 걸리지 않게 가장자리 끝에서 이만큼 안쪽에 둔다.
        private const float TailInset = 20f;

        /// <summary>
        /// 설명 말풍선을 밝힌 대상 옆에 둔다. 오른쪽 → 왼쪽 → 위 → 아래 중 캔버스 안에 들어가는 첫 자리를 고르고,
        /// 옆이면 대상 높이 가운데에, 위·아래면 대상 가로 가운데에 맞춘 뒤 캔버스 안으로 민다. 어디에도 들어가지 않으면 위에 둔다.
        /// </summary>
        /// <param name="gap">대상과 말풍선 사이 거리(꼬리가 들어갈 자리)</param>
        /// <param name="margin">캔버스 가장자리에서 띄울 거리</param>
        public static BubblePose Bubble(
            Rect hole,
            Vector2 size,
            Vector2 canvasSize,
            float gap,
            float margin
        )
        {
            float sideY = Clamp(
                hole.center.y - size.y / 2f,
                margin,
                canvasSize.y - margin - size.y
            );
            float stackX = Clamp(
                hole.center.x - size.x / 2f,
                margin,
                canvasSize.x - margin - size.x
            );
            if (hole.xMax + gap + size.x <= canvasSize.x - margin)
            {
                var rect = new Rect(hole.xMax + gap, sideY, size.x, size.y);
                return new BubblePose(rect, BubbleSide.Right, SideTail(rect, rect.xMin, hole));
            }

            if (hole.xMin - gap - size.x >= margin)
            {
                var rect = new Rect(hole.xMin - gap - size.x, sideY, size.x, size.y);
                return new BubblePose(rect, BubbleSide.Left, SideTail(rect, rect.xMax, hole));
            }

            bool fitsBelow = hole.yMin - gap - size.y >= margin;
            if (hole.yMax + gap + size.y <= canvasSize.y - margin || !fitsBelow)
            {
                float y = Mathf.Min(hole.yMax + gap, canvasSize.y - margin - size.y);
                var rect = new Rect(stackX, y, size.x, size.y);
                return new BubblePose(rect, BubbleSide.Above, StackTail(rect, rect.yMin, hole));
            }

            var below = new Rect(stackX, hole.yMin - gap - size.y, size.x, size.y);
            return new BubblePose(below, BubbleSide.Below, StackTail(below, below.yMax, hole));
        }

        private static Vector2 SideTail(Rect bubble, float x, Rect hole)
        {
            return new Vector2(
                x,
                Clamp(hole.center.y, bubble.yMin + TailInset, bubble.yMax - TailInset)
            );
        }

        private static Vector2 StackTail(Rect bubble, float y, Rect hole)
        {
            return new Vector2(
                Clamp(hole.center.x, bubble.xMin + TailInset, bubble.xMax - TailInset),
                y
            );
        }

        // 최솟값이 최댓값보다 크면(캔버스가 말풍선보다 작음) 최솟값 쪽에 둔다.
        private static float Clamp(float value, float min, float max)
        {
            return Mathf.Max(min, Mathf.Min(value, max));
        }

        public static MarkerPose Marker(
            Vector2 point,
            Vector2 bodyPoint,
            bool isBehindCamera,
            Vector2 canvasSize,
            float margin,
            float topMargin
        )
        {
            var inner = Rect.MinMaxRect(
                margin,
                margin,
                canvasSize.x - margin,
                canvasSize.y - topMargin
            );
            if (!isBehindCamera && inner.Contains(point))
            {
                return new MarkerPose(point, -90f);
            }

            if (!isBehindCamera && inner.Contains(bodyPoint))
            {
                return new MarkerPose(bodyPoint, -90f);
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
