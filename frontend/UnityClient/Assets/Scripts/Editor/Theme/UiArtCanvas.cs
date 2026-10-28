using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Theme
{
    /// <summary>
    /// 아이콘과 창·버튼 바탕을 도형으로 그리는 작은 그림판. 좌표는 완성 그림의 픽셀 단위이며 아래가 0이다.
    /// 한 픽셀을 4×4로 나눠 칠한 뒤 평균을 내어 가장자리를 부드럽게 한다.
    /// </summary>
    internal sealed class UiArtCanvas
    {
        private const int Samples = 4;
        private readonly int width;
        private readonly int height;
        private readonly Color[] fine;

        public UiArtCanvas(int width, int height)
        {
            this.width = width;
            this.height = height;
            fine = new Color[width * Samples * height * Samples];
        }

        public int Width => width;
        public int Height => height;

        public void Circle(float x, float y, float radius, Color color) =>
            Fill(
                new Rect(x - radius, y - radius, radius * 2, radius * 2),
                color,
                p => (p - new Vector2(x, y)).sqrMagnitude <= radius * radius
            );

        public void Ring(float x, float y, float outer, float inner, Color color) =>
            Fill(
                new Rect(x - outer, y - outer, outer * 2, outer * 2),
                color,
                p =>
                {
                    float d = (p - new Vector2(x, y)).sqrMagnitude;
                    return d <= outer * outer && d >= inner * inner;
                }
            );

        public void RoundRect(float x0, float y0, float x1, float y1, float radius, Color color) =>
            Fill(
                Rect.MinMaxRect(x0, y0, x1, y1),
                color,
                p => InsideRoundRect(p, x0, y0, x1, y1, radius)
            );

        public void Polygon(Color color, params Vector2[] corners)
        {
            float xMin = float.MaxValue,
                yMin = float.MaxValue,
                xMax = float.MinValue,
                yMax = float.MinValue;
            foreach (Vector2 c in corners)
            {
                xMin = Mathf.Min(xMin, c.x);
                yMin = Mathf.Min(yMin, c.y);
                xMax = Mathf.Max(xMax, c.x);
                yMax = Mathf.Max(yMax, c.y);
            }
            Fill(Rect.MinMaxRect(xMin, yMin, xMax, yMax), color, p => InsidePolygon(p, corners));
        }

        /// <summary>점들을 잇는 둥근 끝 선. 화살표·체크·호를 그릴 때 쓴다.</summary>
        public void Line(float thickness, Color color, params Vector2[] points)
        {
            float r = thickness / 2f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                Vector2 a = points[i],
                    b = points[i + 1];
                Rect bounds = Rect.MinMaxRect(
                    Mathf.Min(a.x, b.x) - r,
                    Mathf.Min(a.y, b.y) - r,
                    Mathf.Max(a.x, b.x) + r,
                    Mathf.Max(a.y, b.y) + r
                );
                Fill(bounds, color, p => DistanceToSegment(p, a, b) <= r);
            }
        }

        /// <summary>중심에서 시작 각도부터 끝 각도까지(도, 반시계) 도는 호를 선으로 그린다.</summary>
        public void Arc(
            float x,
            float y,
            float radius,
            float fromDeg,
            float toDeg,
            float thickness,
            Color color
        )
        {
            var points = new List<Vector2>();
            int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 8f));
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius));
            }
            Line(thickness, color, points.ToArray());
        }

        /// <summary>둥근 사각형 바깥으로 흐려지는 그림자. 구름 유리 테마의 부드러운 그림자에 쓴다.</summary>
        public void SoftShadow(
            float x0,
            float y0,
            float x1,
            float y1,
            float radius,
            float blur,
            Color color
        ) =>
            Fill(
                Rect.MinMaxRect(x0 - blur, y0 - blur, x1 + blur, y1 + blur),
                color,
                p => true,
                p =>
                {
                    float d = DistanceOutsideRoundRect(p, x0, y0, x1, y1, radius);
                    return d <= 0 ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / blur), 2f);
                }
            );

        /// <summary>안쪽 판정을 직접 넘겨 칠한다. 다른 도형을 비워 둔 테두리처럼 단순 도형으로 안 되는 모양에 쓴다.</summary>
        public void Custom(Rect bounds, Color color, Func<Vector2, bool> inside) =>
            Fill(bounds, color, inside);

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            int fineWidth = width * Samples;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                    sum += fine[(y * Samples + sy) * fineWidth + x * Samples + sx];
                pixels[y * width + x] = Unpremultiply(sum / (Samples * Samples));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>그림 둘레에 같은 두께의 테두리를 더한다. 스티커 팝 테마의 굵은 남색 테두리 아이콘에 쓴다.</summary>
        public static Texture2D WithOutline(Texture2D source, float thickness, Color color)
        {
            int w = source.width,
                h = source.height;
            Color[] src = source.GetPixels();
            var result = new Color[src.Length];
            int reach = Mathf.CeilToInt(thickness) + 1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float edge = 0f;
                for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx,
                        ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    float coverage = Mathf.Clamp01(
                        thickness + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)
                    );
                    edge = Mathf.Max(edge, src[ny * w + nx].a * coverage);
                }
                Color top = src[y * w + x];
                Color under = new Color(color.r, color.g, color.b, edge * color.a);
                float a = top.a + under.a * (1f - top.a);
                result[y * w + x] =
                    a <= 0f
                        ? Color.clear
                        : new Color(
                            (top.r * top.a + under.r * under.a * (1f - top.a)) / a,
                            (top.g * top.a + under.g * under.a * (1f - top.a)) / a,
                            (top.b * top.a + under.b * under.a * (1f - top.a)) / a,
                            a
                        );
            }
            var outlined = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outlined.SetPixels(result);
            outlined.Apply();
            return outlined;
        }

        private void Fill(
            Rect bounds,
            Color color,
            Func<Vector2, bool> inside,
            Func<Vector2, float> strength = null
        )
        {
            int fineWidth = width * Samples;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(bounds.xMin * Samples));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(bounds.yMin * Samples));
            int x1 = Mathf.Min(fineWidth - 1, Mathf.CeilToInt(bounds.xMax * Samples));
            int y1 = Mathf.Min(height * Samples - 1, Mathf.CeilToInt(bounds.yMax * Samples));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2((x + 0.5f) / Samples, (y + 0.5f) / Samples);
                if (!inside(p))
                    continue;
                float alpha = color.a * (strength != null ? strength(p) : 1f);
                if (alpha <= 0f)
                    continue;
                // 미리 곱한 알파로 겹쳐 칠한다.
                Color under = fine[y * fineWidth + x];
                fine[y * fineWidth + x] = new Color(
                    color.r * alpha + under.r * (1f - alpha),
                    color.g * alpha + under.g * (1f - alpha),
                    color.b * alpha + under.b * (1f - alpha),
                    alpha + under.a * (1f - alpha)
                );
            }
        }

        private static Color Unpremultiply(Color c) =>
            c.a <= 0.0001f ? Color.clear : new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a);

        private static bool InsideRoundRect(
            Vector2 p,
            float x0,
            float y0,
            float x1,
            float y1,
            float r
        ) => DistanceOutsideRoundRect(p, x0, y0, x1, y1, r) <= 0f;

        private static float DistanceOutsideRoundRect(
            Vector2 p,
            float x0,
            float y0,
            float x1,
            float y1,
            float r
        )
        {
            r = Mathf.Min(r, (x1 - x0) / 2f, (y1 - y0) / 2f);
            float cx = Mathf.Clamp(p.x, x0 + r, x1 - r);
            float cy = Mathf.Clamp(p.y, y0 + r, y1 - r);
            return (p - new Vector2(cx, cy)).magnitude - r;
        }

        private static bool InsidePolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if (
                    (polygon[i].y > p.y) != (polygon[j].y > p.y)
                    && p.x
                        < (polygon[j].x - polygon[i].x)
                            * (p.y - polygon[i].y)
                            / (polygon[j].y - polygon[i].y)
                            + polygon[i].x
                )
                    inside = !inside;
            return inside;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t =
                ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
