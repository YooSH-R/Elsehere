using UnityEngine;

namespace A305.Player
{
    /// <summary>창 가로 크기가 바뀔 때 칸이 붙는 쪽</summary>
    public enum HorizontalPin
    {
        Left,
        Right,

        /// <summary>왼쪽·오른쪽 거리를 지키며 늘어난다.</summary>
        Stretch,

        /// <summary>가운데에서 떨어진 거리를 지킨다.</summary>
        Center,
    }

    /// <summary>창 세로 크기가 바뀔 때 칸이 붙는 쪽</summary>
    public enum VerticalPin
    {
        Top,
        Bottom,

        /// <summary>위·아래 거리를 지키며 늘어난다.</summary>
        Stretch,

        /// <summary>가운데에서 떨어진 거리를 지킨다.</summary>
        Center,
    }

    /// <summary>
    /// 크기를 바꿀 수 있는 창(<see cref="WindowFrame"/>) 안의 칸이 어느 쪽에 붙을지 정한다.
    /// 프리팹은 고정 좌표(왼쪽 위 기준)로 만들어져 있어, 창을 열기 전에 지금 자리를 그대로 둔 채 앵커만 바꾼다.
    /// </summary>
    public static class WindowLayout
    {
        /// <summary><paramref name="window"/> 아래 <paramref name="path"/> 칸을 붙인다. 프리팹에 없으면 경고만 남긴다.</summary>
        public static void Pin(
            Transform window,
            string path,
            HorizontalPin horizontal,
            VerticalPin vertical
        )
        {
            if (window.Find(path) is RectTransform item)
                Pin(item, horizontal, vertical);
            else
                Debug.LogWarning(
                    $"창 칸 {window.name}/{path}을 찾지 못해 크기에 맞춰 붙이지 않습니다.",
                    window
                );
        }

        public static void Pin(RectTransform item, HorizontalPin horizontal, VerticalPin vertical)
        {
            Vector2 parentSize = ((RectTransform)item.parent).rect.size;
            Vector2 min = Vector2.Scale(item.anchorMin, parentSize) + item.offsetMin;
            Vector2 max = Vector2.Scale(item.anchorMax, parentSize) + item.offsetMax;
            (float minX, float maxX) = Anchors(horizontal);
            (float minY, float maxY) = Anchors(vertical);
            item.anchorMin = new Vector2(minX, minY);
            item.anchorMax = new Vector2(maxX, maxY);
            item.offsetMin = min - Vector2.Scale(item.anchorMin, parentSize);
            item.offsetMax = max - Vector2.Scale(item.anchorMax, parentSize);
        }

        private static (float Min, float Max) Anchors(HorizontalPin pin) =>
            pin switch
            {
                HorizontalPin.Left => (0f, 0f),
                HorizontalPin.Right => (1f, 1f),
                HorizontalPin.Stretch => (0f, 1f),
                _ => (.5f, .5f),
            };

        private static (float Min, float Max) Anchors(VerticalPin pin) =>
            pin switch
            {
                VerticalPin.Bottom => (0f, 0f),
                VerticalPin.Top => (1f, 1f),
                VerticalPin.Stretch => (0f, 1f),
                _ => (.5f, .5f),
            };
    }
}
