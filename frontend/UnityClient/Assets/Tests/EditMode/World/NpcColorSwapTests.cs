using A305.EditorTools.Npc;
using NUnit.Framework;
using UnityEngine;

namespace A305.World.Tests
{
    /// <summary>
    /// 같은 외형을 쓰는 NPC도 머리·옷·눈동자 색으로 구분되도록 색 텍스처의 한 색 범위만 다른 색으로 바꾼다.
    /// 바꾼 색은 원래 명암을 지켜 주름·그림자가 남는다.
    /// </summary>
    public class NpcColorSwapTests
    {
        // 중년 여성 외형의 파란 스웨터와 살구색 피부
        private static readonly Color32 Sweater = Color.HSVToRGB(.62f, .47f, .53f);
        private static readonly Color32 Skin = Color.HSVToRGB(.08f, .51f, 1f);

        private static readonly NpcColorSwap BlueToBurgundy = new NpcColorSwap(
            new Vector2(.55f, .70f),
            new Vector2(.2f, 1f),
            new Vector2(.2f, 1f),
            .97f,
            .55f
        );

        [Test]
        public void Recolor_PixelInTheRange_TakesTheNewHueAndSaturationButKeepsItsShade()
        {
            Color32[] result = NpcColorSwap.Recolor(new[] { Sweater }, new[] { BlueToBurgundy });

            Color.RGBToHSV(result[0], out float hue, out float saturation, out float value);
            Assert.That(hue, Is.EqualTo(.97f).Within(.01f));
            Assert.That(saturation, Is.EqualTo(.55f).Within(.01f));
            Assert.That(value, Is.EqualTo(.53f).Within(.01f));
        }

        [Test]
        public void Recolor_PixelOutsideEveryRange_StaysTheSame()
        {
            Color32[] result = NpcColorSwap.Recolor(new[] { Skin }, new[] { BlueToBurgundy });

            Assert.That(result[0], Is.EqualTo(Skin));
        }

        // 자주색(.96)과 빨강(.02)처럼 색상환의 0을 넘는 범위는 양 끝을 모두 고른다.
        [Test]
        public void Recolor_HueRangeAcrossRed_MatchesBothEnds()
        {
            var plumToGreen = new NpcColorSwap(
                new Vector2(.90f, .05f),
                new Vector2(.2f, 1f),
                new Vector2(.2f, 1f),
                .33f,
                .45f
            );
            Color32 plum = Color.HSVToRGB(.96f, .36f, .5f);
            Color32 red = Color.HSVToRGB(.02f, .6f, .6f);
            Color32 blue = Color.HSVToRGB(.62f, .5f, .5f);

            Color32[] result = NpcColorSwap.Recolor(
                new[] { plum, red, blue },
                new[] { plumToGreen }
            );

            Color.RGBToHSV(result[0], out float plumHue, out _, out _);
            Color.RGBToHSV(result[1], out float redHue, out _, out _);
            Assert.That(plumHue, Is.EqualTo(.33f).Within(.01f));
            Assert.That(redHue, Is.EqualTo(.33f).Within(.01f));
            Assert.That(result[2], Is.EqualTo(blue));
        }

        // 머리를 밝게(회색 머리를 흰 머리로) 바꿀 때 명암은 키우되 1을 넘지 않는다.
        [Test]
        public void Recolor_ValueScale_BrightensWithoutGoingPastWhite()
        {
            var greyToWhite = new NpcColorSwap(
                new Vector2(0f, 1f),
                new Vector2(0f, .2f),
                new Vector2(.3f, .9f),
                .1f,
                .05f,
                1.6f
            );
            Color32 dark = Color.HSVToRGB(.07f, .1f, .4f);
            Color32 light = Color.HSVToRGB(.07f, .1f, .8f);

            Color32[] result = NpcColorSwap.Recolor(new[] { dark, light }, new[] { greyToWhite });

            Color.RGBToHSV(result[0], out _, out _, out float darkValue);
            Color.RGBToHSV(result[1], out _, out _, out float lightValue);
            Assert.That(darkValue, Is.EqualTo(.64f).Within(.01f));
            Assert.That(lightValue, Is.EqualTo(1f).Within(.01f));
        }

        [Test]
        public void Recolor_KeepsTransparency()
        {
            Color32 seeThrough = Sweater;
            seeThrough.a = 40;

            Color32[] result = NpcColorSwap.Recolor(new[] { seeThrough }, new[] { BlueToBurgundy });

            Assert.That(result[0].a, Is.EqualTo(40));
        }
    }
}
