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
        public void Hint_WithRoleLabel_SaysTheRoleWithTheRightParticle()
        {
            // 영어 이름만으로는 드럭스토어 직원인지 알 수 없어, 역할이 있으면 역할로 알린다.
            Assert.That(
                PasserbyOfferText.Hint("Alex", "드럭스토어 직원"),
                Is.EqualTo("드럭스토어 직원이 말을 걸었어요")
            );
            Assert.That(
                PasserbyOfferText.Hint("Mia", "바리스타"),
                Is.EqualTo("바리스타가 말을 걸었어요")
            );
        }

        [Test]
        public void Hint_WithoutNpcName_DoesNotAssumeNpcKind()
        {
            Assert.That(PasserbyOfferText.Hint(null), Is.EqualTo("누군가 말을 걸었어요"));
            Assert.That(PasserbyOfferText.Hint(""), Is.EqualTo("누군가 말을 걸었어요"));
        }

        [Test]
        public void TalkLabel_WithNpcName_NamesWhoToTalkTo()
        {
            // 이름 뒤 조사가 받침에 따라 바뀌지 않도록 '에게'를 쓴다.
            Assert.That(PasserbyOfferText.TalkLabel("Mia"), Is.EqualTo("Mia에게 말 걸기"));
        }

        [Test]
        public void TalkLabel_WithoutNpcName_JustSaysTalk()
        {
            Assert.That(PasserbyOfferText.TalkLabel(null), Is.EqualTo("말 걸기"));
            Assert.That(PasserbyOfferText.TalkLabel(""), Is.EqualTo("말 걸기"));
        }
    }
}
