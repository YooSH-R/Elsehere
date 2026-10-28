using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class PasserbyOfferTextTests
    {
        [Test]
        public void Hint_WithNpcName_NamesWhoIsTalking()
        {
            // 행인과 카페 직원이 같은 제안 화면을 쓰므로 "행인이"로 고정하지 않고 말을 건 NPC를 밝힌다.
            Assert.That(PasserbyOfferText.Hint("Emma"), Is.EqualTo("Emma이(가) 말을 걸었어요"));
        }

        [Test]
        public void Hint_WithoutNpcName_DoesNotAssumeNpcKind()
        {
            Assert.That(PasserbyOfferText.Hint(null), Is.EqualTo("누군가 말을 걸었어요"));
            Assert.That(PasserbyOfferText.Hint(""), Is.EqualTo("누군가 말을 걸었어요"));
        }
    }
}
