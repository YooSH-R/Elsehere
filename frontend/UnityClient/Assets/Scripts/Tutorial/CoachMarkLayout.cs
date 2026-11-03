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
        /// 가리면 안 되는 곳(keepClear)이 있으면 그곳과 겹치는 자리는 건너뛰고, 옆자리는 그곳 아래·위로, 위·아래는 그곳 왼쪽·오른쪽으로 비켜서도 놓아 본다.
        /// 모든 자리가 겹치면 가리면 안 되는 곳이 없을 때와 같은 자리에 둔다.
        /// </summary>
        /// <param name="gap">대상과 말풍선 사이 거리(꼬리가 들어갈 자리)</param>
        /// <param name="margin">캔버스 가장자리에서 띄울 거리</param>
        /// <param name="keepClear">말풍선이 가리면 안 되는 곳(대화 위에 남긴 [건너뛰기] 카드). 넓이가 없으면 보지 않는다.</param>
        public static BubblePose Bubble(
            Rect hole,
            Vector2 size,
            Vector2 canvasSize,
            float gap,
            float margin,
            Rect keepClear = default
        )
        {
            BubblePose preferred = PreferredBubble(hole, size, canvasSize, gap, margin);
            if (
                keepClear.width <= 0f
                || keepClear.height <= 0f
                || !preferred.Rect.Overlaps(keepClear)
            )
            {
                return preferred;
            }

            float sideY = Clamp(
                hole.center.y - size.y / 2f,
                margin,
                canvasSize.y - margin - size.y
            );
            // 옆자리는 대상 높이 가운데, 가리면 안 되는 곳 바로 아래, 바로 위 순서로 놓아 본다.
            float[] sideYs = { sideY, keepClear.yMin - gap - size.y, keepClear.yMax + gap };
            var sides = new List<BubblePose>();
            foreach (float y in sideYs)
            {
                var rect = new Rect(hole.xMax + gap, y, size.x, size.y);
                if (FitsInside(rect, canvasSize, margin))
                {
                    sides.Add(
                        new BubblePose(rect, BubbleSide.Right, SideTail(rect, rect.xMin, hole))
                    );
                }
            }

            foreach (float y in sideYs)
            {
                var rect = new Rect(hole.xMin - gap - size.x, y, size.x, size.y);
                if (FitsInside(rect, canvasSize, margin))
                {
                    sides.Add(
                        new BubblePose(rect, BubbleSide.Left, SideTail(rect, rect.xMax, hole))
                    );
                }
            }

            // 옆자리가 조금 모자라면 화면 끝에 붙여 밝힌 곳에 말풍선 폭의 절반까지 겹쳐 둔다(대화 노트의 결과 칸 오른쪽).
            var pinnedRight = new Rect(canvasSize.x - margin - size.x, sideY, size.x, size.y);
            if (
                FitsInside(pinnedRight, canvasSize, margin)
                && hole.xMax - pinnedRight.xMin <= size.x / 2f
            )
            {
                sides.Add(
                    new BubblePose(
                        pinnedRight,
                        BubbleSide.Right,
                        SideTail(pinnedRight, pinnedRight.xMin, hole)
                    )
                );
            }

            var pinnedLeft = new Rect(margin, sideY, size.x, size.y);
            if (
                FitsInside(pinnedLeft, canvasSize, margin)
                && pinnedLeft.xMax - hole.xMin <= size.x / 2f
            )
            {
                sides.Add(
                    new BubblePose(
                        pinnedLeft,
                        BubbleSide.Left,
                        SideTail(pinnedLeft, pinnedLeft.xMax, hole)
                    )
                );
            }

            float[] stackXs =
            {
                hole.center.x - size.x / 2f,
                keepClear.xMin - gap - size.x,
                keepClear.xMax + gap,
            };
            float aboveY = hole.yMax + gap;
            float belowY = hole.yMin - gap - size.y;
            foreach (float x in stackXs)
            {
                var rect = new Rect(x, aboveY, size.x, size.y);
                if (FitsInside(rect, canvasSize, margin))
                {
                    sides.Add(
                        new BubblePose(rect, BubbleSide.Above, StackTail(rect, rect.yMin, hole))
                    );
                }
            }

            foreach (float x in stackXs)
            {
                var rect = new Rect(x, belowY, size.x, size.y);
                if (FitsInside(rect, canvasSize, margin))
                {
                    sides.Add(
                        new BubblePose(rect, BubbleSide.Below, StackTail(rect, rect.yMax, hole))
                    );
                }
            }

            foreach (BubblePose side in sides)
            {
                if (!side.Rect.Overlaps(keepClear))
                {
                    return side;
                }
            }

            return preferred;
        }

        // 가리면 안 되는 곳을 보지 않을 때의 자리
        private static BubblePose PreferredBubble(
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

        private static bool FitsInside(Rect rect, Vector2 canvasSize, float margin)
        {
            return rect.xMin >= margin
                && rect.yMin >= margin
                && rect.xMax <= canvasSize.x - margin
                && rect.yMax <= canvasSize.y - margin;
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

        /// <summary>발밑 나침반이 가리키는 방향(바닥과 나란한 단위 벡터), 원 가장자리 표시 자리, 바닥에 눕힌 그림의 회전.</summary>
        public readonly struct CompassPose
        {
            public CompassPose(Vector3 direction, Vector3 pointerPosition, Quaternion rotation)
            {
                Direction = direction;
                PointerPosition = pointerPosition;
                Rotation = rotation;
            }

            public Vector3 Direction { get; }
            public Vector3 PointerPosition { get; }

            /// <summary>오른쪽을 가리키는 그림을 바닥에 눕혀 대상 쪽으로 돌리는 회전</summary>
            public Quaternion Rotation { get; }
        }

        /// <summary>
        /// 걸어갈 대상을 플레이어 발밑 바닥에 나침반처럼 가리킨다. 높낮이는 보지 않고 바닥을 따라 대상 쪽을 가리키며,
        /// 대상이 발밑 바로 위·아래에 있어 방향을 알 수 없으면 앞쪽을 가리킨다.
        /// </summary>
        /// <param name="pointerDistanceMeters">발밑에서 가장자리 표시까지 거리</param>
        public static CompassPose Compass(Vector3 feet, Vector3 target, float pointerDistanceMeters)
        {
            Vector3 toward = target - feet;
            toward.y = 0f;
            Vector3 direction = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            // 그림은 오른쪽(+X)을 가리키고 앞(+Z)을 향한 면이다. 면을 아래로 눕히고 오른쪽을 대상 쪽으로 돌린다.
            Quaternion rotation = Quaternion.LookRotation(
                Vector3.down,
                Quaternion.Euler(0f, -90f, 0f) * direction
            );
            return new CompassPose(direction, feet + direction * pointerDistanceMeters, rotation);
        }

        /// <summary>
        /// 1인칭처럼 발밑 나침반이 화면에 없을 때 화면 아래에 띄울 화살표 방향(도, 오른쪽 0·위 90).
        /// 카메라가 보는 쪽을 위로 두어, 앞이면 위·뒤면 아래·옆이면 그쪽을 가리킨다.
        /// </summary>
        public static float CompassScreenArrowDegrees(Vector3 cameraForward, Vector3 direction)
        {
            Vector3 forward = cameraForward;
            forward.y = 0f;
            // 바로 아래를 내려다보면 수평 방향을 알 수 없으므로 앞쪽으로 본다.
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            // 위에서 볼 때 시계 방향(오른쪽)이 양수다.
            float turn = Vector3.SignedAngle(forward, direction, Vector3.up);
            return 90f - turn;
        }

        /// <summary>
        /// 다가오는 대상(Ben) 머리 위 화살표를 띄울지. 그 자리가 화면 안(위쪽 안내 카드 아래)에 있을 때만 띄우고,
        /// 화면 밖·카드 자리·카메라 뒤면 띄우지 않는다. 몸통이나 얼굴로 옮기면 대상을 가리므로 옮기지 않는다.
        /// </summary>
        /// <param name="point">대상 머리 위 화살표 자리의 화면 위치(캔버스 왼쪽 아래 기준)</param>
        /// <param name="topMargin">위쪽 안내 카드 자리. 여기에 걸린 자리는 화면 밖으로 본다.</param>
        public static bool ShowsMarker(
            Vector2 point,
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
            return !isBehindCamera && inner.Contains(point);
        }
    }
}
