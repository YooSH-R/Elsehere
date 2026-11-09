using NUnit.Framework;

namespace A305.Npc.Tests
{
    public sealed class NpcHurryTests
    {
        [Test]
        public void LeisureWalking_DoesNotHurryEvenWhenTheTargetIsFarAway()
        {
            var hurry = new NpcHurry();
            hurry.Update(false, 100);
            Assert.That(hurry.IsHurrying, Is.False);
            Assert.That(hurry.Speed(true), Is.EqualTo(1.6f));
        }

        [Test]
        public void ScheduledJourney_RunsOnlyWhenItHasFallenBehindAndThePathIsClear()
        {
            var hurry = new NpcHurry();
            hurry.Update(true, 3.99f);
            Assert.That(hurry.IsHurrying, Is.False);
            hurry.Update(true, 4);
            Assert.That(hurry.IsHurrying, Is.True);
            Assert.That(hurry.Speed(true), Is.EqualTo(2.6f));
        }

        [Test]
        public void ANearbyPersonOrCrosswalk_SlowsTheNpcButItIsStillInAHurry()
        {
            var hurry = new NpcHurry();
            hurry.Update(true, 8);
            Assert.That(hurry.IsHurrying, Is.True);
            Assert.That(hurry.Speed(false), Is.EqualTo(1.6f));
            Assert.That(hurry.Speed(true), Is.EqualTo(2.6f));
        }

        [Test]
        public void CatchingUp_DoesNotSwitchBetweenRunningAndWalkingEveryFrame()
        {
            var hurry = new NpcHurry();
            hurry.Update(true, 6);
            hurry.Update(true, 2);
            Assert.That(hurry.IsHurrying, Is.True);
            hurry.Update(true, 1.5f);
            Assert.That(hurry.IsHurrying, Is.False);
            hurry.Update(true, 2);
            Assert.That(hurry.IsHurrying, Is.False);
        }

        [Test]
        public void CancellingTheScheduledJourney_ClearsTheUrgency()
        {
            var hurry = new NpcHurry();
            hurry.Update(true, 6);
            Assert.That(hurry.IsHurrying, Is.True);
            hurry.Update(false, 6);
            Assert.That(hurry.IsHurrying, Is.False);
        }
    }
}
