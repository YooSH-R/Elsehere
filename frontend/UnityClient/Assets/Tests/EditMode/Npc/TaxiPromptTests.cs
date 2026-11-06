using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class TaxiPromptTests
    {
        [Test]
        public void Choose_PassingTaxi_ShowsHailWithGuestRemainingConversations()
        {
            // 택시를 세우면 바로 기사와 대화하므로 [택시 세우기]에도 남은 대화 수를 붙인다.
            TaxiPrompt prompt = TaxiPrompt.Choose(true, false, true, 2, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Hail));
            Assert.That(prompt.Label, Is.EqualTo("택시 세우기"));
            Assert.That(prompt.Remaining, Is.EqualTo("게스트 남은 대화 2번"));
        }

        [Test]
        public void Choose_StoppedTaxiNearbyForMember_ShowsTalkToDriverWithoutRemaining()
        {
            TaxiPrompt prompt = TaxiPrompt.Choose(false, true, true, null, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Talk));
            Assert.That(prompt.Label, Is.EqualTo("Joe에게 말 걸기"));
            Assert.That(prompt.Remaining, Is.Null);
        }

        // S15P31A305-578: 대화를 다 쓴 게스트에게는 막힌 이유 대신 가입으로 이어지는 버튼을 보인다.
        [Test]
        public void Choose_GuestUsedAllConversations_OffersSignupInsteadOfHail()
        {
            TaxiPrompt prompt = TaxiPrompt.Choose(true, false, false, 0, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Signup));
            Assert.That(prompt.Label, Is.EqualTo("가입하고 계속 대화하기"));
            Assert.That(prompt.Remaining, Is.EqualTo("게스트 대화를 모두 썼어요"));
        }

        [Test]
        public void Choose_GuestUsedAllConversationsNearStoppedTaxi_OffersSignupInsteadOfTalk()
        {
            TaxiPrompt prompt = TaxiPrompt.Choose(false, true, false, 0, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Signup));
            Assert.That(prompt.Label, Is.EqualTo("가입하고 계속 대화하기"));
        }

        [Test]
        public void Choose_CannotStartConversationNearStoppedTaxi_ShowsReasonInsteadOfTalk()
        {
            TaxiPrompt prompt = TaxiPrompt.Choose(false, true, false, null, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Blocked));
            Assert.That(prompt.Label, Is.EqualTo("지금은 대화할 수 없어요"));
        }

        [Test]
        public void Choose_NoTaxiToHailOrTalk_ShowsNothingEvenWhenBlocked()
        {
            Assert.That(
                TaxiPrompt.Choose(false, false, false, 0, "Joe").Kind,
                Is.EqualTo(TaxiPromptKind.None)
            );
        }
    }
}
