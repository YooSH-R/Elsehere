using NUnit.Framework;

namespace A305.Npc.Tests
{
    public class PasserbyOfferTextTests
    {
        [TestCase("Mia", "카페 직원", "Mia(카페 직원)의 대화 제안")]
        [TestCase("Mia", "바리스타", "Mia(바리스타)의 대화 제안")]
        [TestCase("Alex", "드럭스토어 직원", "Alex(드럭스토어 직원)의 대화 제안")]
        public void Hint_WithNameAndRole_IdentifiesTheStaffMember(
            string name,
            string role,
            string expected
        )
        {
            Assert.That(PasserbyOfferText.Hint(name, role), Is.EqualTo(expected));
        }

        [Test]
        public void Hint_WithNpcName_NamesWhoIsTalking()
        {
            // 행인과 카페 직원이 같은 제안 화면을 쓰므로 "행인이"로 고정하지 않고 말을 건 NPC를 밝힌다.
            Assert.That(PasserbyOfferText.Hint("Emma"), Is.EqualTo("Emma이(가) 말을 걸었어요"));
        }

        [Test]
        public void Hint_WithRoleButNoName_SaysTheRoleWithTheRightParticle()
        {
            // 이름을 받지 못해도 직원 역할과 자연스러운 조사는 남긴다.
            Assert.That(
                PasserbyOfferText.Hint(null, "드럭스토어 직원"),
                Is.EqualTo("드럭스토어 직원이 말을 걸었어요")
            );
            Assert.That(
                PasserbyOfferText.Hint(null, "바리스타"),
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

        [Test]
        public void RemainingConversations_Guest_SaysHowManyAreLeft()
        {
            Assert.That(
                PasserbyOfferText.RemainingConversations(2, guest: true),
                Is.EqualTo("게스트 남은 대화 2번")
            );
        }

        [Test]
        public void RemainingConversations_Member_SaysHowManyAreLeftToday()
        {
            Assert.That(
                PasserbyOfferText.RemainingConversations(2, guest: false),
                Is.EqualTo("오늘 남은 대화 2번")
            );
        }

        [Test]
        public void RemainingConversations_WithoutLimit_ShowsNothing()
        {
            // 서버 상태를 아직 받지 못했으면 남은 수를 모른다.
            Assert.That(PasserbyOfferText.RemainingConversations(null, guest: false), Is.Null);
        }

        [Test]
        public void Blocked_GuestUsedAllConversations_SaysWhy()
        {
            Assert.That(
                PasserbyOfferText.Blocked(0, guest: true),
                Is.EqualTo("게스트 대화를 모두 썼어요")
            );
        }

        [Test]
        public void Blocked_MemberUsedTodaysConversations_SaysComeBackTomorrow()
        {
            Assert.That(
                PasserbyOfferText.Blocked(0, guest: false),
                Is.EqualTo("오늘의 대화를 모두 썼어요. 내일 다시 만나요.")
            );
        }

        [Test]
        public void Blocked_WithoutLimit_SaysTalkIsUnavailable()
        {
            // 대화 횟수가 아닌 이유로 막혔으면 횟수를 말하지 않는다.
            Assert.That(
                PasserbyOfferText.Blocked(null, guest: true),
                Is.EqualTo("지금은 대화할 수 없어요")
            );
        }

        [Test]
        public void OffersSignup_OnlyWhenGuestUsedAllConversations()
        {
            Assert.That(PasserbyOfferText.OffersSignup(0, guest: true), Is.True);
            Assert.That(PasserbyOfferText.OffersSignup(2, guest: true), Is.False);
            // 오늘 대화를 다 쓴 회원과 다른 이유로 막힌 사용자에게는 가입을 권하지 않는다.
            Assert.That(PasserbyOfferText.OffersSignup(0, guest: false), Is.False);
            Assert.That(PasserbyOfferText.OffersSignup(null, guest: false), Is.False);
        }
    }
}
