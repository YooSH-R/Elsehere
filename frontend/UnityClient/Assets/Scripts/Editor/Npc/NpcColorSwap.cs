using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 색 텍스처에서 한 색 범위(색상·채도·명도)에 든 픽셀만 다른 색상·채도로 바꾼다. 명도는 그대로 두거나 배수만큼만 바꿔
    /// 주름·그림자를 지킨다. 같은 외형을 쓰는 NPC를 머리·옷·신발·눈동자 색으로 구분할 때 쓴다.
    /// 색상 범위의 시작이 끝보다 크면 빨강(0)을 넘는 범위다(예: 자주 .90 ~ 빨강 .05).
    /// 몸 높이 구역을 정하면 그 높이에 그려진 픽셀만 바꾼다. 머리카락과 바지처럼 같은 색이 몸 여러 곳에 있을 때 나눈다.
    /// </summary>
    public readonly struct NpcColorSwap
    {
        private readonly Vector2 hue;
        private readonly Vector2 saturation;
        private readonly Vector2 value;
        private readonly Vector2 height;
        private readonly float targetHue;
        private readonly float targetSaturation;
        private readonly float valueScale;
        private readonly bool keeps;

        /// <param name="hue">바꿀 색상 범위(0~1)</param>
        /// <param name="saturation">바꿀 채도 범위(0~1)</param>
        /// <param name="value">바꿀 명도 범위(0~1)</param>
        /// <param name="valueScale">명도에 곱하는 값. 1보다 크면 밝아지고 1을 넘지 않는다.</param>
        /// <param name="height">바꿀 몸 높이 구역(발바닥 0 ~ 머리 꼭대기 1). 비우거나 0~1이면 높이를 보지 않는다.</param>
        /// <param name="keeps">true면 이 범위의 픽셀을 바꾸지 않고 원래 색으로 지켜, 뒤의 다른 범위가 가져가지 않게 한다.</param>
        public NpcColorSwap(
            Vector2 hue,
            Vector2 saturation,
            Vector2 value,
            float targetHue,
            float targetSaturation,
            float valueScale = 1f,
            Vector2? height = null,
            bool keeps = false
        )
        {
            this.hue = hue;
            this.saturation = saturation;
            this.value = value;
            this.targetHue = targetHue;
            this.targetSaturation = targetSaturation;
            this.valueScale = valueScale;
            this.height = height ?? new Vector2(0f, 1f);
            this.keeps = keeps;
        }

        private bool LooksAtHeight => height.x > 0f || height.y < 1f;

        /// <summary>픽셀마다 처음 맞는 범위로 바꾼다. 어느 범위에도 들지 않으면 그대로 둔다. 투명도는 지킨다.</summary>
        /// <param name="heights">
        /// 픽셀마다 그 자리가 그려진 몸 높이(0~1). 몸에 쓰이지 않는 자리는 음수다. 없으면 높이 구역을 정한 범위는 바꾸지 않는다.
        /// </param>
        public static Color32[] Recolor(
            IReadOnlyList<Color32> pixels,
            IReadOnlyList<NpcColorSwap> swaps,
            IReadOnlyList<float> heights = null
        )
        {
            var result = new Color32[pixels.Count];
            for (int index = 0; index < pixels.Count; index++)
            {
                Color32 pixel = pixels[index];
                result[index] = pixel;
                Color.RGBToHSV(pixel, out float h, out float s, out float v);
                float at = heights != null ? heights[index] : -1f;
                foreach (NpcColorSwap swap in swaps)
                {
                    if (!swap.Matches(h, s, v, at))
                    {
                        continue;
                    }

                    // 색을 바꾸지 않는 부분은 뒤의 다른 범위가 그 픽셀을 가져가지 않게 그대로 둔다.
                    if (swap.keeps)
                    {
                        break;
                    }

                    result[index] = swap.Swapped(pixel, v);
                    break;
                }
            }
            return result;
        }

        // 틈을 찾는 방향: 가로·세로·두 대각선
        private static readonly (int X, int Y)[] GapDirections =
        {
            (1, 0),
            (0, 1),
            (1, 1),
            (1, -1),
        };

        /// <summary>
        /// 이 범위로 바꾼 픽셀 사이에 끼어 그대로 남은 가는 틈(머리카락 가닥 사이의 어두운 선)을 같은 색으로 메운다.
        /// 한 방향의 양쪽 radius 픽셀 안에 모두 바꾼 픽셀이 있어야 틈으로 보므로, 머리 가장자리 바깥(이마 피부)은 메우지 않는다.
        /// 머리카락과 피부처럼 색 범위가 겹쳐 범위를 넓힐 수 없는 외형에 쓴다.
        /// </summary>
        /// <param name="pixels">바꾸기 전 텍스처</param>
        /// <param name="result"><see cref="Recolor"/>로 바꾼 텍스처. 이 배열을 고친다.</param>
        /// <param name="width">텍스처 폭(픽셀). 픽셀은 한 줄씩 이어 둔다.</param>
        /// <param name="heights">픽셀마다 그려진 몸 높이. 이 범위의 높이 구역 밖은 메우지 않는다.</param>
        public static void FillGaps(
            IReadOnlyList<Color32> pixels,
            Color32[] result,
            int width,
            NpcColorSwap swap,
            IReadOnlyList<float> heights,
            int radius
        )
        {
            int rows = pixels.Count / width;
            var taken = new bool[pixels.Count];
            for (int index = 0; index < pixels.Count; index++)
            {
                Color.RGBToHSV(pixels[index], out float h, out float s, out float v);
                taken[index] =
                    swap.Matches(h, s, v, heights != null ? heights[index] : -1f)
                    && !Same(result[index], pixels[index]);
            }

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int index = row * width + column;
                    float at = heights != null ? heights[index] : -1f;
                    bool inArea =
                        !swap.LooksAtHeight || (at >= swap.height.x && at <= swap.height.y);
                    if (
                        taken[index]
                        || !inArea
                        || !Same(result[index], pixels[index])
                        || !IsBetweenTaken(taken, width, rows, column, row, radius)
                    )
                    {
                        continue;
                    }

                    Color.RGBToHSV(pixels[index], out _, out _, out float value);
                    result[index] = swap.Swapped(pixels[index], value);
                }
            }
        }

        private static bool IsBetweenTaken(
            bool[] taken,
            int width,
            int rows,
            int column,
            int row,
            int radius
        )
        {
            foreach ((int x, int y) in GapDirections)
            {
                if (
                    HasTakenWithin(taken, width, rows, column, row, x, y, radius)
                    && HasTakenWithin(taken, width, rows, column, row, -x, -y, radius)
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTakenWithin(
            bool[] taken,
            int width,
            int rows,
            int column,
            int row,
            int stepX,
            int stepY,
            int radius
        )
        {
            for (int step = 1; step <= radius; step++)
            {
                int x = column + stepX * step;
                int y = row + stepY * step;
                if (x < 0 || y < 0 || x >= width || y >= rows)
                {
                    return false;
                }

                if (taken[y * width + x])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Same(Color32 first, Color32 second)
        {
            return first.r == second.r
                && first.g == second.g
                && first.b == second.b
                && first.a == second.a;
        }

        private Color32 Swapped(Color32 pixel, float value)
        {
            Color32 swapped = Color.HSVToRGB(
                targetHue,
                targetSaturation,
                Mathf.Min(1f, value * valueScale)
            );
            swapped.a = pixel.a;
            return swapped;
        }

        private bool Matches(float h, float s, float v, float at)
        {
            bool inHue = hue.x <= hue.y ? h >= hue.x && h <= hue.y : h >= hue.x || h <= hue.y;
            bool inColor =
                inHue && s >= saturation.x && s <= saturation.y && v >= value.x && v <= value.y;
            if (!inColor || !LooksAtHeight)
            {
                return inColor;
            }

            return at >= 0f && at >= height.x && at <= height.y;
        }
    }

    /// <summary>
    /// 외형 텍스처에서 한 부분(머리·옷·신발·눈동자)이 차지하는 색 범위와 몸 높이 구역. <see cref="To"/>로 배색을 정해 바꾼다.
    /// </summary>
    public readonly struct NpcColorRange
    {
        private readonly Vector2 hue;
        private readonly Vector2 saturation;
        private readonly Vector2 value;
        private readonly Vector2 height;
        private readonly float brighten;

        /// <param name="height">이 부분이 있는 몸 높이(발바닥 0 ~ 머리 꼭대기 1). 비우면 몸 어디든</param>
        /// <param name="brighten">이 범위만 명도를 더 곱한다. 아주 어두운 눈동자가 바꾼 색으로 보이게 밝힐 때 쓴다.</param>
        public NpcColorRange(
            (float Min, float Max) hue,
            (float Min, float Max) saturation,
            (float Min, float Max) value,
            (float Min, float Max)? height = null,
            float brighten = 1f
        )
        {
            this.hue = new Vector2(hue.Min, hue.Max);
            this.saturation = new Vector2(saturation.Min, saturation.Max);
            this.value = new Vector2(value.Min, value.Max);
            this.height = height.HasValue
                ? new Vector2(height.Value.Min, height.Value.Max)
                : new Vector2(0f, 1f);
            this.brighten = brighten;
        }

        public NpcColorSwap To(NpcTint tint)
        {
            return new NpcColorSwap(
                hue,
                saturation,
                value,
                tint.Hue,
                tint.Saturation,
                tint.ValueScale * brighten,
                height
            );
        }

        /// <summary>이 범위의 픽셀을 원래 색으로 둔다. 범위가 겹치는 다른 부분의 배색이 이 부분까지 바꾸지 않게 앞에 둔다.</summary>
        public NpcColorSwap Keep()
        {
            return new NpcColorSwap(hue, saturation, value, 0f, 0f, 1f, height, keeps: true);
        }
    }

    /// <summary>
    /// 바꿀 색. 색상·채도는 이 값으로 바꾸고 명도는 원래 명도에 배수를 곱해 주름·그림자를 지킨다.
    /// 같은 외형의 NPC끼리 같은 색인지는 이름으로 가린다(<see cref="NpcCast"/>). 같은 이름에는 같은 값을 쓴다.
    /// </summary>
    public readonly struct NpcTint
    {
        public NpcTint(string name, float hue, float saturation, float valueScale = 1f)
        {
            Name = name;
            Hue = hue;
            Saturation = saturation;
            ValueScale = valueScale;
        }

        /// <summary>표와 보고에서 읽는 색 이름</summary>
        public string Name { get; }
        public float Hue { get; }
        public float Saturation { get; }
        public float ValueScale { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
