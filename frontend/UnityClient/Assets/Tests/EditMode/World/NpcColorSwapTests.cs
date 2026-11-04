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

        // 머리카락과 바지처럼 같은 색이 몸 여러 곳에 있으면 몸 높이 구역으로 머리 쪽만 바꾼다.
        [Test]
        public void Recolor_RangeLimitedToTheHead_ChangesOnlyPixelsDrawnHighOnTheBody()
        {
            var hairToBlack = new NpcColorSwap(
                new Vector2(.05f, .12f),
                new Vector2(.3f, .8f),
                new Vector2(.2f, .9f),
                .07f,
                .3f,
                .3f,
                new Vector2(.6f, 1f)
            );
            Color32 brown = Color.HSVToRGB(.08f, .5f, .5f);

            Color32[] result = NpcColorSwap.Recolor(
                new[] { brown, brown, brown },
                new[] { hairToBlack },
                new[] { .8f, .2f, -1f }
            );

            Assert.That(result[0], Is.Not.EqualTo(brown), "머리 높이의 픽셀은 바꿉니다.");
            Assert.That(result[1], Is.EqualTo(brown), "다리 높이의 픽셀은 둡니다.");
            Assert.That(result[2], Is.EqualTo(brown), "몸에 쓰이지 않는 자리는 둡니다.");
        }

        // 신발처럼 원래 색으로 둘 부분을 앞에 두면, 같은 색을 고르는 뒤의 범위(바지)가 그 픽셀을 바꾸지 않는다.
        [Test]
        public void Recolor_KeptRangeBeforeAnother_LeavesItsPixelsAlone()
        {
            var keepShoes = new NpcColorSwap(
                new Vector2(0f, .1f),
                new Vector2(.3f, .8f),
                new Vector2(.1f, .9f),
                0f,
                0f,
                1f,
                new Vector2(0f, .1f),
                keeps: true
            );
            var brownToNavy = new NpcColorSwap(
                new Vector2(0f, .1f),
                new Vector2(.3f, .8f),
                new Vector2(.1f, .9f),
                .62f,
                .55f
            );
            Color32 brown = Color.HSVToRGB(.06f, .5f, .4f);

            Color32[] result = NpcColorSwap.Recolor(
                new[] { brown, brown },
                new[] { keepShoes, brownToNavy },
                new[] { .05f, .3f }
            );

            Assert.That(result[0], Is.EqualTo(brown), "신발은 원래 색입니다.");
            Color.RGBToHSV(result[1], out float trouserHue, out _, out _);
            Assert.That(trouserHue, Is.EqualTo(.62f).Within(.01f), "바지는 남색이 됩니다.");
        }

        // 머리카락 가닥 사이의 어두운 선처럼, 바꾼 픽셀 사이에 끼어 범위 밖이라 남은 가는 틈도 같은 색으로 메운다.
        [Test]
        public void FillGaps_PixelBetweenRecoloredPixels_TakesTheSameColor()
        {
            var sandyToWhite = new NpcColorSwap(
                new Vector2(.08f, .14f),
                new Vector2(.2f, .5f),
                new Vector2(.4f, .9f),
                .1f,
                .05f,
                1.25f
            );
            Color32 hair = Color.HSVToRGB(.1f, .35f, .7f);
            Color32 strandLine = Color.HSVToRGB(.085f, .55f, .6f);
            Color32[] pixels = { hair, strandLine, hair };
            Color32[] result = NpcColorSwap.Recolor(pixels, new[] { sandyToWhite });

            NpcColorSwap.FillGaps(pixels, result, 3, sandyToWhite, null, 2);

            Color.RGBToHSV(result[1], out _, out float saturation, out _);
            Assert.That(saturation, Is.EqualTo(.05f).Within(.01f));
        }

        // 머리 가장자리처럼 한쪽에만 바꾼 픽셀이 있는 곳(이마의 피부)은 메우지 않는다.
        [Test]
        public void FillGaps_PixelBesideRecoloredPixelsOnOneSideOnly_StaysTheSame()
        {
            var sandyToWhite = new NpcColorSwap(
                new Vector2(.08f, .14f),
                new Vector2(.2f, .5f),
                new Vector2(.4f, .9f),
                .1f,
                .05f,
                1.25f
            );
            Color32 hair = Color.HSVToRGB(.1f, .35f, .7f);
            Color32 skin = Color.HSVToRGB(.08f, .55f, .95f);
            Color32[] pixels = { hair, hair, skin, skin, skin };
            Color32[] result = NpcColorSwap.Recolor(pixels, new[] { sandyToWhite });

            NpcColorSwap.FillGaps(pixels, result, 5, sandyToWhite, null, 2);

            Assert.That(result[2], Is.EqualTo(skin));
            Assert.That(result[3], Is.EqualTo(skin));
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
