using System;
using A305.Theme;
using UnityEngine;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 글자 대신 쓰는 이모티콘 느낌의 아이콘 그림. 디자인 시안의 48칸 좌표(위가 0)를 그대로 옮겨 128px로 그린다.
    /// 같은 그림을 모든 테마가 쓰고, 스티커 팝 테마만 둘레에 남색 테두리를 더한다.
    /// </summary>
    internal static class UiIconArt
    {
        internal const int Size = 128;
        private const float Scale = Size / 48f;

        private static readonly Color White = Color.white;
        private static readonly Color Ink = Hex("#4A4A5A");
        private static readonly Color Blue = Hex("#3F7FD6");
        private static readonly Color DeepBlue = Hex("#2E62AE");
        private static readonly Color Red = Hex("#E0553D");
        private static readonly Color Yellow = Hex("#FFC93C");
        private static readonly Color Cream = Hex("#FFF8EC");

        public static Texture2D Paint(UiIcon icon)
        {
            var c = new UiArtCanvas(Size, Size);
            switch (icon)
            {
                case UiIcon.Map:
                    c.Polygon(
                        Hex("#8CC084"),
                        V(6, 12),
                        V(17, 8),
                        V(31, 12),
                        V(42, 8),
                        V(42, 38),
                        V(31, 42),
                        V(17, 38),
                        V(6, 42)
                    );
                    c.Polygon(Hex("#5F9E66"), V(17, 8), V(31, 12), V(31, 42), V(17, 38));
                    c.Polygon(Red, V(16.6f, 22.4f), V(31.4f, 22.4f), V(24, 34.5f));
                    Circle(c, 24, 20.2f, 7.9f, Red);
                    Circle(c, 24, 20.2f, 3.1f, White);
                    break;
                case UiIcon.Book:
                    Box(c, 12, 8, 40, 42, 4, Cream);
                    Box(c, 8, 6, 36, 42, 5, Blue);
                    Box(c, 8, 6, 15, 42, 3.5f, DeepBlue);
                    c.Polygon(Yellow, V(27, 6), V(33, 6), V(33, 19), V(30, 16.6f), V(27, 19));
                    Box(c, 19, 27, 31, 31, 2, White);
                    break;
                case UiIcon.Star:
                    c.Polygon(Yellow, Star(24, 23, 19.5f, 8.6f));
                    Circle(c, 19.3f, 23.5f, 2.2f, Hex("#4A3B33"));
                    Circle(c, 28.7f, 23.5f, 2.2f, Hex("#4A3B33"));
                    Line(
                        c,
                        2.2f,
                        Hex("#4A3B33"),
                        V(20.3f, 28.2f),
                        V(22.2f, 29.7f),
                        V(24, 30.1f),
                        V(25.8f, 29.7f),
                        V(27.7f, 28.2f)
                    );
                    Circle(c, 16.5f, 27.5f, 1.8f, Hex("#FF9B7A"));
                    Circle(c, 31.5f, 27.5f, 1.8f, Hex("#FF9B7A"));
                    break;
                case UiIcon.Talk:
                    Box(c, 4, 9, 44, 34, 5, White);
                    c.Polygon(White, V(14.5f, 33), V(23, 33), V(14.5f, 41));
                    foreach (float x in new[] { 16f, 24f, 32f })
                        Circle(c, x, 21.5f, 2.8f, Blue);
                    break;
                case UiIcon.Mic:
                    Box(c, 17, 5, 31, 30, 7, White);
                    c.Arc(S(24), Y(21), S(13), 180, 360, S(4.4f), White);
                    Line(c, 4.4f, White, V(24, 35), V(24, 42));
                    Line(c, 4.4f, White, V(17, 43), V(31, 43));
                    break;
                case UiIcon.Send:
                    c.Polygon(White, V(5, 23), V(43, 5), V(30, 43), V(23, 27));
                    Line(c, 2, Hex("#9CC6EE"), V(23, 27), V(42, 6));
                    break;
                case UiIcon.Meaning:
                    Line(c, 6, Blue, V(31, 31), V(40.5f, 40.5f));
                    Circle(c, 21, 21, 15, Blue);
                    Circle(c, 21, 21, 11.2f, White);
                    // 한글 '가'
                    Line(c, 2.6f, Blue, V(14.5f, 15.5f), V(20.5f, 15.5f), V(18.6f, 25.5f));
                    Line(c, 2.6f, Blue, V(24.5f, 13.5f), V(24.5f, 28));
                    Line(c, 2.6f, Blue, V(24.5f, 20.5f), V(28, 20.5f));
                    break;
                case UiIcon.Hint:
                    Circle(c, 24, 19, 12.5f, Hex("#FFD54A"));
                    Box(c, 18, 31, 30, 38, 2, Hex("#B8A892"));
                    Box(c, 20, 39, 28, 42.5f, 1.5f, Hex("#B8A892"));
                    Line(c, 2, White, V(20, 15), V(22.5f, 12.5f));
                    Line(c, 3, White, V(24, 1.5f), V(24, 4.5f));
                    Line(c, 3, White, V(9, 7.5f), V(11.2f, 9.7f));
                    Line(c, 3, White, V(39, 7.5f), V(36.8f, 9.7f));
                    break;
                case UiIcon.Example:
                    Box(c, 5, 9, 43, 36, 7, White);
                    c.Polygon(White, V(12, 35), V(20, 35), V(10, 44));
                    foreach (float x in new[] { 17f, 28f })
                    {
                        Circle(c, x, 20.5f, 3.6f, Hex("#5FAE6E"));
                        Line(c, 2.8f, Hex("#5FAE6E"), V(x + 2.2f, 22), V(x - 1, 28));
                    }
                    break;
                case UiIcon.Replay:
                    c.Polygon(
                        White,
                        V(7, 18),
                        V(14, 18),
                        V(24, 10),
                        V(24, 38),
                        V(14, 30),
                        V(7, 30)
                    );
                    c.Arc(S(30), Y(24), S(9.5f), 110, -110, S(3.6f), White);
                    c.Polygon(
                        White,
                        Polar(30, 24, 9.5f, 110, 4.6f),
                        Polar(30, 24, 9.5f, 110, -4.6f),
                        Tip(30, 24, 9.5f, 110, 5.5f)
                    );
                    break;
                case UiIcon.Save:
                    c.Polygon(Red, V(10, 5), V(38, 5), V(38, 43), V(24, 34), V(10, 43));
                    c.Polygon(Hex("#FFE08A"), Star(24, 19.5f, 8, 3.6f));
                    break;
                case UiIcon.Exit:
                    Box(c, 8, 6, 30, 42, 3, Hex("#C98B5B"));
                    Box(c, 11.5f, 9.5f, 26.5f, 38.5f, 2, Hex("#E2AE7E"));
                    Circle(c, 23.5f, 25, 2, Hex("#FFE08A"));
                    Line(c, 3.6f, Red, V(28, 24), V(43, 24));
                    Line(c, 3.6f, Red, V(37.5f, 18.5f), V(43, 24), V(37.5f, 29.5f));
                    break;
                case UiIcon.Close:
                    Line(c, 5, Ink, V(14, 14), V(34, 34));
                    Line(c, 5, Ink, V(34, 14), V(14, 34));
                    break;
                case UiIcon.Check:
                    Line(c, 6, White, V(11, 25), V(20, 34), V(37, 14));
                    break;
                case UiIcon.Play:
                    c.Polygon(White, V(15, 10), V(38, 24), V(15, 38));
                    break;
                case UiIcon.Skip:
                    c.Polygon(Ink, V(8, 11), V(23, 24), V(8, 37));
                    c.Polygon(Ink, V(23, 11), V(38, 24), V(23, 37));
                    Box(c, 38, 11, 42, 37, 1.5f, Ink);
                    break;
                case UiIcon.Palette:
                    Circle(c, 24, 24, 19.5f, Hex("#F6D8A8"));
                    Circle(c, 29, 32.5f, 4.4f, Hex("#E3BE86"));
                    Circle(c, 15, 21, 3.8f, Hex("#FF8A5B"));
                    Circle(c, 21, 12.5f, 3.8f, Hex("#FFCB52"));
                    Circle(c, 31, 12.5f, 3.8f, Hex("#69B4F0"));
                    Circle(c, 36, 22, 3.8f, Hex("#7CC48A"));
                    break;
                case UiIcon.Previous:
                    Line(c, 5, Ink, V(36, 24), V(13, 24));
                    Line(c, 5, Ink, V(22, 14.5f), V(12.5f, 24), V(22, 33.5f));
                    break;
                case UiIcon.Next:
                    Line(c, 5, Ink, V(12, 24), V(35, 24));
                    Line(c, 5, Ink, V(26, 14.5f), V(35.5f, 24), V(26, 33.5f));
                    break;
                case UiIcon.Delete:
                    c.Polygon(
                        Hex("#A9AEBA"),
                        V(12.5f, 15),
                        V(35.5f, 15),
                        V(32.5f, 43),
                        V(15.5f, 43)
                    );
                    Box(c, 9, 10, 39, 15, 2, Hex("#7E8494"));
                    Box(c, 19, 6, 29, 10.5f, 2, Hex("#7E8494"));
                    Line(c, 2.4f, Hex("#7E8494"), V(20, 20), V(21, 38));
                    Line(c, 2.4f, Hex("#7E8494"), V(28, 20), V(27, 38));
                    break;
                case UiIcon.Detail:
                    Line(c, 6, Blue, V(31, 31), V(40.5f, 40.5f));
                    Circle(c, 21, 21, 14.5f, Blue);
                    Circle(c, 21, 21, 10.6f, White);
                    Line(c, 2.4f, Hex("#A9C6E2"), V(15.5f, 18.5f), V(26.5f, 18.5f));
                    Line(c, 2.4f, Hex("#A9C6E2"), V(15.5f, 23.5f), V(23, 23.5f));
                    break;
                case UiIcon.Retry:
                    c.Arc(S(24), Y(25), S(13), 70, -230, S(5), Blue);
                    c.Polygon(
                        Blue,
                        Polar(24, 25, 13, 70, 6.5f),
                        Polar(24, 25, 13, 70, -6.5f),
                        Tip(24, 25, 13, 70, -7)
                    );
                    break;
                case UiIcon.Rest:
                    Box(c, 9, 19, 33, 41, 6, White);
                    c.Ring(S(33.5f), Y(29), S(6.5f), S(3.2f), White);
                    Box(c, 6, 41, 36, 44, 1.5f, White);
                    Line(c, 2.4f, White, V(16, 15), V(17.5f, 11.5f), V(15.5f, 7.5f));
                    Line(c, 2.4f, White, V(24, 15), V(25.5f, 11.5f), V(23.5f, 7.5f));
                    break;
                case UiIcon.Home:
                    Box(c, 30, 8, 35, 18, 1, White);
                    c.Polygon(White, V(5, 23), V(24, 7), V(43, 23));
                    Box(c, 10, 20, 38, 42, 3, White);
                    Box(c, 20, 28, 28, 42, 2, Hex("#C97B4A"));
                    Circle(c, 26, 35.5f, 1, Yellow);
                    break;
                case UiIcon.Hotel:
                    Box(c, 6, 12, 12, 41, 2.5f, White);
                    Box(c, 6, 27, 42, 36, 2.5f, White);
                    Box(c, 14, 20, 23, 27, 2.5f, White);
                    Box(c, 24, 21, 42, 31, 3, Yellow);
                    Box(c, 37, 35, 42, 41, 1, White);
                    break;
                case UiIcon.Store:
                    c.Arc(S(24), Y(17), S(7.5f), 0, 180, S(3.2f), White);
                    Box(c, 9, 16, 39, 42, 4, White);
                    Box(c, 9, 23, 39, 27, .5f, Red);
                    Circle(c, 24, 34, 4, Yellow);
                    break;
                case UiIcon.Tree:
                    Box(c, 21.5f, 28, 26.5f, 44, 2, Hex("#A0714B"));
                    Circle(c, 24, 19, 14, Hex("#6FBF73"));
                    Circle(c, 18.5f, 14.5f, 5, Hex("#8ED38F"));
                    break;
                case UiIcon.Menu:
                    Box(c, 11, 13, 37, 18, 2.5f, Ink);
                    Box(c, 11, 21.5f, 37, 26.5f, 2.5f, Ink);
                    Box(c, 11, 30, 37, 35, 2.5f, Ink);
                    break;
                // 하늘색 휴대폰 버튼 위의 흰 휴대폰
                case UiIcon.Phone:
                    Box(c, 14, 5, 34, 43, 5, White);
                    Box(c, 17, 10, 31, 35, 2, Hex("#2F6FB8"));
                    Circle(c, 24, 39, 1.6f, Hex("#2F6FB8"));
                    break;
                case UiIcon.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(icon), icon, "그림이 없는 아이콘");
            }
            return c.ToTexture();
        }

        /// <summary>디자인 시안의 48칸 좌표(위가 0)를 그림판 픽셀 좌표(아래가 0)로 바꾼다.</summary>
        private static Vector2 V(float x, float y) => new Vector2(x * Scale, (48f - y) * Scale);

        private static float S(float length) => length * Scale;

        private static float Y(float y) => (48f - y) * Scale;

        private static void Circle(UiArtCanvas c, float x, float y, float r, Color color) =>
            c.Circle(S(x), Y(y), S(r), color);

        private static void Box(
            UiArtCanvas c,
            float x0,
            float y0,
            float x1,
            float y1,
            float r,
            Color color
        ) => c.RoundRect(S(x0), Y(y1), S(x1), Y(y0), S(r), color);

        private static void Line(
            UiArtCanvas c,
            float thickness,
            Color color,
            params Vector2[] points
        ) => c.Line(S(thickness), color, points);

        private static Vector2[] Star(float x, float y, float outer, float inner)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                points[i] = V(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r);
            }
            return points;
        }

        /// <summary>호 끝의 화살촉. 원 위 한 점에서 반지름 방향으로 벌린 두 점과 진행 방향 끝점을 만든다.</summary>
        private static Vector2 Polar(float x, float y, float r, float angleDeg, float spread)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var center = new Vector2(S(x), Y(y));
            var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            return center + radial * S(r + spread);
        }

        private static Vector2 Tip(float x, float y, float r, float angleDeg, float ahead)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var center = new Vector2(S(x), Y(y));
            var onCircle = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * S(r);
            var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            return onCircle + tangent * S(ahead);
        }

        private static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }
}
