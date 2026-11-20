using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>정해진 순서대로만 하는 퀘스트 동안에는 지금 단계에 나오는 사람만 말을 걸 수 있고, 나머지는 지금 차례를 까닭으로 막는다.</summary>
    public sealed class ShowcaseTalkLockTests
    {
        [TearDown]
        public void Clear() => ShowcaseTalkLock.Clear();

        [Test]
        public void OnlyTheCurrentStepsPeopleCanBeTalkedTo()
        {
            ShowcaseTalkLock.Set("지금은 호텔 차례예요.", new[] { "HOTEL_STAFF" });

            Assert.That(ShowcaseTalkLock.IsActive, Is.True);
            Assert.That(ShowcaseTalkLock.ReasonFor("HOTEL_STAFF"), Is.Null);
            Assert.That(
                ShowcaseTalkLock.ReasonFor("LAUNDROMAT_STAFF"),
                Is.EqualTo("지금은 호텔 차례예요.")
            );
            Assert.That(ShowcaseTalkLock.Reason, Is.EqualTo("지금은 호텔 차례예요."));
        }

        [Test]
        public void ClearingLetsEveryoneTalkAgain()
        {
            ShowcaseTalkLock.Set("지금은 호텔 차례예요.", new[] { "HOTEL_STAFF" });

            ShowcaseTalkLock.Clear();

            Assert.That(ShowcaseTalkLock.IsActive, Is.False);
            Assert.That(ShowcaseTalkLock.ReasonFor("LAUNDROMAT_STAFF"), Is.Null);
            Assert.That(ShowcaseTalkLock.Reason, Is.Null);
        }
    }
}
