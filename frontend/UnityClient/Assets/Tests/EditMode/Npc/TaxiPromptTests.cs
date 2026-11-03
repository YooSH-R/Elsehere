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

        [Test]
        public void Choose_GuestUsedAllConversations_ShowsReasonInsteadOfHail()
        {
            TaxiPrompt prompt = TaxiPrompt.Choose(true, false, false, 0, "Joe");

            Assert.That(prompt.Kind, Is.EqualTo(TaxiPromptKind.Blocked));
            Assert.That(prompt.Label, Is.EqualTo("게스트 대화를 모두 썼어요"));
            Assert.That(prompt.Remaining, Is.Null);
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
