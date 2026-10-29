using A305.Theme;
using UnityEngine;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 테마별 창·버튼 바탕 그림. 버튼 바탕은 흰색 계열로 그려 버튼 쓰임 색을 곱해 칠하고,
    /// 스티커 팝의 굵은 테두리와 그림자는 따로 겹치는 그림으로 둔다. border는 9칸 나누기 크기(왼·아래·오른·위)다.
    /// </summary>
    internal static class UiThemeArt
    {
        internal enum Part
        {
            Panel,
            Card,
            Button,
            ButtonOutline,
            Pill,
            PillOutline,
            Badge,
            Input,
        }

        internal readonly struct Art
        {
            public Art(Texture2D texture, Vector4 border)
            {
                Texture = texture;
                Border = border;
            }

            public Texture2D Texture { get; }
            public Vector4 Border { get; }
        }

        private static readonly Color Navy = Hex("#2D3A5A");

        /// <summary>테마에 없는 부분(말랑 젤리·구름 유리의 테두리 겹침 그림)은 null이다.</summary>
        public static Art? Paint(UiThemeId theme, Part part) =>
            theme switch
            {
                UiThemeId.Sticker => Sticker(part),
                UiThemeId.Cloud => Cloud(part),
                _ => Jelly(part),
            };

        // 말랑 젤리: 크림색 창, 흰 테두리, 아래쪽 두께감(립)이 있는 사탕 버튼.
        private static Art? Jelly(Part part)
        {
            switch (part)
            {
                case Part.Panel:
                {
                    var c = new UiArtCanvas(148, 148);
                    c.SoftShadow(12, 6, 136, 132, 34, 10, new Color(0.29f, 0.23f, 0.2f, 0.18f));
                    c.RoundRect(10, 10, 138, 132, 36, Hex("#EBD9BE"));
                    c.RoundRect(10, 18, 138, 138, 36, Color.white);
                    c.RoundRect(15, 23, 133, 133, 31, Hex("#FFF8EC"));
                    return new Art(c.ToTexture(), new Vector4(48, 56, 48, 48));
                }
                case Part.Card:
                {
                    var c = new UiArtCanvas(96, 96);
                    c.RoundRect(4, 4, 92, 88, 22, Hex("#EBD9BE"));
                    c.RoundRect(4, 9, 92, 92, 22, Color.white);
                    return new Art(c.ToTexture(), new Vector4(28, 32, 28, 28));
                }
                case Part.Button:
                {
                    var c = new UiArtCanvas(128, 128);
                    c.SoftShadow(10, 6, 118, 112, 34, 8, new Color(0.25f, 0.2f, 0.18f, 0.22f));
                    c.RoundRect(8, 8, 120, 116, 36, Gray(0.76f));
                    c.RoundRect(8, 16, 120, 124, 36, Gray(0.95f));
                    c.RoundRect(20, 96, 108, 116, 10, new Color(1, 1, 1, 0.6f));
                    return new Art(c.ToTexture(), Vector4.zero);
                }
                case Part.Pill:
                {
                    var c = new UiArtCanvas(160, 104);
                    c.RoundRect(6, 6, 154, 92, 43, Gray(0.76f));
                    c.RoundRect(6, 13, 154, 99, 43, Gray(0.95f));
                    c.RoundRect(26, 80, 134, 92, 6, new Color(1, 1, 1, 0.55f));
                    return new Art(c.ToTexture(), new Vector4(50, 30, 50, 30));
                }
                case Part.Badge:
                {
                    var c = new UiArtCanvas(64, 64);
                    c.Circle(32, 32, 30, Color.white);
                    return new Art(c.ToTexture(), new Vector4(26, 26, 26, 26));
                }
                case Part.Input:
                {
                    var c = new UiArtCanvas(96, 96);
                    c.RoundRect(2, 2, 94, 94, 30, Hex("#EBD9BE"));
                    c.RoundRect(6, 6, 90, 90, 26, Color.white);
                    return new Art(c.ToTexture(), new Vector4(34, 34, 34, 34));
                }
                default:
                    return null;
            }
        }

        // 스티커 팝: 흰 바탕, 굵은 남색 테두리, 오른쪽 아래로 진하게 밀린 그림자.
        private static Art? Sticker(Part part)
        {
            switch (part)
            {
                case Part.Panel:
                {
                    var c = new UiArtCanvas(132, 132);
                    c.RoundRect(10, 2, 130, 122, 28, Navy);
                    c.RoundRect(2, 10, 122, 130, 28, Navy);
                    c.RoundRect(6, 14, 118, 126, 24, Color.white);
                    return new Art(c.ToTexture(), new Vector4(36, 44, 44, 36));
                }
                case Part.Card:
                {
                    var c = new UiArtCanvas(96, 96);
                    c.RoundRect(7, 2, 94, 89, 20, Navy);
                    c.RoundRect(2, 7, 89, 94, 20, Navy);
                    c.RoundRect(5, 10, 86, 91, 17, Color.white);
                    return new Art(c.ToTexture(), new Vector4(26, 32, 32, 26));
                }
                case Part.Button:
                {
                    var c = new UiArtCanvas(128, 128);
                    c.Circle(60, 68, 52, Color.white);
                    return new Art(c.ToTexture(), Vector4.zero);
                }
                case Part.ButtonOutline:
                {
                    var c = new UiArtCanvas(128, 128);
                    var face = new Vector2(60, 68);
                    var shadow = new Vector2(67, 60);
                    c.Custom(
                        new Rect(0, 0, 128, 128),
                        Navy,
                        p =>
                            (p - face).sqrMagnitude >= 52 * 52
                            && (
                                (p - face).sqrMagnitude <= 58 * 58
                                || (p - shadow).sqrMagnitude <= 58 * 58
                            )
                    );
                    return new Art(c.ToTexture(), Vector4.zero);
                }
                case Part.Pill:
                {
                    var c = new UiArtCanvas(168, 104);
                    c.RoundRect(6, 16, 158, 98, 41, Color.white);
                    return new Art(c.ToTexture(), new Vector4(50, 34, 56, 34));
                }
                case Part.PillOutline:
                {
                    var c = new UiArtCanvas(168, 104);
                    c.Custom(
                        new Rect(0, 0, 168, 104),
                        Navy,
                        p =>
                            !Inside(p, 6, 16, 158, 98, 41)
                            && (Inside(p, 2, 12, 162, 102, 45) || Inside(p, 9, 6, 166, 94, 45))
                    );
                    return new Art(c.ToTexture(), new Vector4(50, 34, 56, 34));
                }
                case Part.Badge:
                {
                    var c = new UiArtCanvas(64, 64);
                    c.Circle(32, 32, 30, Navy);
                    c.Circle(32, 32, 25, Color.white);
                    return new Art(c.ToTexture(), new Vector4(26, 26, 26, 26));
                }
                case Part.Input:
                {
                    var c = new UiArtCanvas(96, 96);
                    c.RoundRect(2, 2, 94, 94, 28, Navy);
                    c.RoundRect(6, 6, 90, 90, 24, Color.white);
                    return new Art(c.ToTexture(), new Vector4(32, 32, 32, 32));
                }
                default:
                    return null;
            }
        }

        // 구름 유리: 반투명 흰 창과 부드럽게 퍼지는 그림자.
        private static Art? Cloud(Part part)
        {
            switch (part)
            {
                case Part.Panel:
                {
                    var c = new UiArtCanvas(160, 160);
                    c.SoftShadow(18, 12, 142, 136, 28, 16, new Color(0.14f, 0.22f, 0.31f, 0.24f));
                    c.RoundRect(16, 20, 144, 144, 28, Color.white);
                    return new Art(c.ToTexture(), new Vector4(46, 52, 46, 46));
                }
                case Part.Card:
                {
                    var c = new UiArtCanvas(112, 112);
                    c.SoftShadow(14, 10, 98, 94, 22, 11, new Color(0.14f, 0.22f, 0.31f, 0.2f));
                    c.RoundRect(12, 16, 100, 100, 22, Color.white);
                    return new Art(c.ToTexture(), new Vector4(36, 40, 36, 36));
                }
                case Part.Button:
                {
                    var c = new UiArtCanvas(128, 128);
                    c.SoftShadow(14, 6, 114, 106, 50, 12, new Color(0.14f, 0.22f, 0.31f, 0.3f));
                    c.Circle(64, 66, 50, Color.white);
                    return new Art(c.ToTexture(), Vector4.zero);
                }
                case Part.Pill:
                {
                    var c = new UiArtCanvas(168, 112);
                    c.SoftShadow(14, 8, 154, 92, 42, 12, new Color(0.14f, 0.22f, 0.31f, 0.28f));
                    c.RoundRect(12, 16, 156, 100, 42, Color.white);
                    return new Art(c.ToTexture(), new Vector4(54, 40, 54, 40));
                }
                case Part.Badge:
                {
                    var c = new UiArtCanvas(64, 64);
                    c.Circle(32, 32, 30, Color.white);
                    return new Art(c.ToTexture(), new Vector4(26, 26, 26, 26));
                }
                case Part.Input:
                {
                    var c = new UiArtCanvas(96, 96);
                    c.RoundRect(2, 2, 94, 94, 30, Hex("#C9DDF0"));
                    c.RoundRect(4, 4, 92, 92, 28, Color.white);
                    return new Art(c.ToTexture(), new Vector4(32, 32, 32, 32));
                }
                default:
                    return null;
            }
        }

        private static bool Inside(Vector2 p, float x0, float y0, float x1, float y1, float r)
        {
            r = Mathf.Min(r, (x1 - x0) / 2f, (y1 - y0) / 2f);
            float cx = Mathf.Clamp(p.x, x0 + r, x1 - r);
            float cy = Mathf.Clamp(p.y, y0 + r, y1 - r);
            return (p - new Vector2(cx, cy)).sqrMagnitude <= r * r;
        }

        private static Color Gray(float value) => new Color(value, value, value, 1f);

        internal static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }
}
