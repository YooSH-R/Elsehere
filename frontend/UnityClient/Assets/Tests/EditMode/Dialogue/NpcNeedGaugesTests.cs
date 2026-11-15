using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    /// <summary>
    /// 대화 창 이름 줄에 보이는 NPC 욕구 게이지. 생활 값 다섯 개 가운데 부탁으로 이어지는 배고픔·피로·외로움만 보이고,
    /// 막대는 가득할수록 채워진 상태이며 낮을수록 경고색으로 바뀐다(docs/npc-life-design.md 6절).
    /// </summary>
    public class NpcNeedGaugesTests
    {
        [Test]
        public void From_ShowsHungerEnergyAndSocialInThatOrder()
        {
            var needs = new NpcNeeds(
                new Dictionary<string, float>
                {
                    ["WORK"] = 80,
                    ["SOCIAL"] = 55,
                    ["FUN"] = 20,
                    ["ENERGY"] = 61,
                    ["HUNGER"] = 28,
                }
            );

            IReadOnlyList<NpcNeedGauge> gauges = NpcNeedGauges.From(needs);

            Assert.That(
                gauges.Select(gauge => gauge.Label),
                Is.EqualTo(new[] { "배고픔", "피로", "외로움" }),
                "재미·업무는 생활 동작에만 쓰고 보이지 않는다"
            );
            Assert.That(gauges.Select(gauge => gauge.Fill), Is.EqualTo(new[] { .28f, .61f, .55f }));
        }

        [TestCase(100f, NpcNeedTone.Fine)]
        [TestCase(70f, NpcNeedTone.Fine)]
        [TestCase(69.9f, NpcNeedTone.Low)]
        [TestCase(40f, NpcNeedTone.Low)]
        [TestCase(39.9f, NpcNeedTone.Urgent)]
        [TestCase(0f, NpcNeedTone.Urgent)]
        public void From_TintsLowerValuesWarmer(float value, NpcNeedTone tone)
        {
            var needs = new NpcNeeds(new Dictionary<string, float> { ["HUNGER"] = value });

            Assert.That(NpcNeedGauges.From(needs).Single().Tone, Is.EqualTo(tone));
        }

        [Test]
        public void From_SkipsNeedsTheNpcDoesNotHave()
        {
            var needs = new NpcNeeds(new Dictionary<string, float> { ["SOCIAL"] = 30 });

            Assert.That(NpcNeedGauges.From(needs).Select(gauge => gauge.Label), Is.EqualTo(new[] { "외로움" }));
        }

        [Test]
        public void From_NoNeedsGivesNoGauges()
        {
            Assert.That(NpcNeedGauges.From(null), Is.Empty, "생활하지 않는 NPC(튜토리얼 Ben, 택시 기사 등)");
        }

        [Test]
        public void NpcNeeds_KeepsItsOwnCopyOfTheValues()
        {
            var values = new Dictionary<string, float> { ["HUNGER"] = 50 };
            var needs = new NpcNeeds(values);

            values["HUNGER"] = 10;

            Assert.That(needs.Values["HUNGER"], Is.EqualTo(50f), "대화를 시작한 순간의 값");
        }
    }
}
