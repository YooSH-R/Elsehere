using System.Collections.Generic;
using UnityEngine;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 색 텍스처에서 한 색 범위(색상·채도·명도)에 든 픽셀만 다른 색상·채도로 바꾼다. 명도는 그대로 두거나 배수만큼만 바꿔
    /// 주름·그림자를 지킨다. 같은 외형을 쓰는 NPC를 머리·옷·눈동자 색으로 구분할 때 쓴다.
    /// 색상 범위의 시작이 끝보다 크면 빨강(0)을 넘는 범위다(예: 자주 .90 ~ 빨강 .05).
    /// </summary>
    public readonly struct NpcColorSwap
    {
        private readonly Vector2 hue;
        private readonly Vector2 saturation;
        private readonly Vector2 value;
        private readonly float targetHue;
        private readonly float targetSaturation;
        private readonly float valueScale;

        /// <param name="hue">바꿀 색상 범위(0~1)</param>
        /// <param name="saturation">바꿀 채도 범위(0~1)</param>
        /// <param name="value">바꿀 명도 범위(0~1)</param>
        /// <param name="valueScale">명도에 곱하는 값. 1보다 크면 밝아지고 1을 넘지 않는다.</param>
        public NpcColorSwap(
            Vector2 hue,
            Vector2 saturation,
            Vector2 value,
            float targetHue,
            float targetSaturation,
            float valueScale = 1f
        )
        {
            this.hue = hue;
            this.saturation = saturation;
            this.value = value;
            this.targetHue = targetHue;
            this.targetSaturation = targetSaturation;
            this.valueScale = valueScale;
        }

        /// <summary>픽셀마다 처음 맞는 범위로 바꾼다. 어느 범위에도 들지 않으면 그대로 둔다. 투명도는 지킨다.</summary>
        public static Color32[] Recolor(
            IReadOnlyList<Color32> pixels,
            IReadOnlyList<NpcColorSwap> swaps
        )
        {
            var result = new Color32[pixels.Count];
            for (int index = 0; index < pixels.Count; index++)
            {
                Color32 pixel = pixels[index];
                result[index] = pixel;
                Color.RGBToHSV(pixel, out float h, out float s, out float v);
                foreach (NpcColorSwap swap in swaps)
                {
                    if (!swap.Matches(h, s, v))
                    {
                        continue;
                    }

                    Color32 swapped = Color.HSVToRGB(
                        swap.targetHue,
                        swap.targetSaturation,
                        Mathf.Min(1f, v * swap.valueScale)
                    );
                    swapped.a = pixel.a;
                    result[index] = swapped;
                    break;
                }
            }
            return result;
        }

        private bool Matches(float h, float s, float v)
        {
            bool inHue = hue.x <= hue.y ? h >= hue.x && h <= hue.y : h >= hue.x || h <= hue.y;
            return inHue && s >= saturation.x && s <= saturation.y && v >= value.x && v <= value.y;
        }
    }
}
