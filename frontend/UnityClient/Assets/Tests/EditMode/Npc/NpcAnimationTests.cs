using NUnit.Framework;

namespace A305.Npc.Tests
{
    /// <summary>NPC가 사물을 쓰는 동안 어떤 동작을 재생하는지.</summary>
    public class NpcAnimationTests
    {
        [TestCase("MAKE_DRINK")]
        [TestCase("SERVE_GUEST")]
        public void WhileUsing_WorkAtAnObject_PlaysWork(string action)
        {
            Assert.That(
                NpcAnimations.WhileUsing(action, seated: false),
                Is.EqualTo(NpcAnimation.Work)
            );
        }

        [TestCase("EAT")]
        [TestCase("DRINK_COFFEE")]
        public void WhileUsing_EatingOrDrinking_PicksItUp(string action)
        {
            Assert.That(
                NpcAnimations.WhileUsing(action, seated: false),
                Is.EqualTo(NpcAnimation.PickUp)
            );
        }

        [Test]
        public void WhileUsing_Seated_SitsWhateverTheAction()
        {
            Assert.That(
                NpcAnimations.WhileUsing("REST", seated: true),
                Is.EqualTo(NpcAnimation.Sit)
            );
        }

        [TestCase("REST")]
        [TestCase("LOOK_AROUND")]
        [TestCase("SOMETHING_NEW")]
        public void WhileUsing_StandingWithoutHands_JustStands(string action)
        {
            Assert.That(
                NpcAnimations.WhileUsing(action, seated: false),
                Is.EqualTo(NpcAnimation.None)
            );
        }
    }
}
