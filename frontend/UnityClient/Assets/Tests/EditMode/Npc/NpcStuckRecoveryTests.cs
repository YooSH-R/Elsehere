using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    public class NpcStuckRecoveryTests
    {
        [Test]
        public void RecoversOnlyAfterContinuousBlockedTime()
        {
            var recovery = new NpcStuckRecovery();
            Assert.That(Tick(recovery, 0, 299), Is.False);
            Assert.That(Tick(recovery, 0, 1), Is.True);
            Assert.That(Tick(recovery, 0, 1), Is.False);
        }

        [Test]
        public void WalkingToANewPositionRestartsTheTimer()
        {
            var recovery = new NpcStuckRecovery();
            Tick(recovery, 0, 299);
            Assert.That(Tick(recovery, .3f, 1), Is.False);
            Assert.That(Tick(recovery, .3f, 299), Is.True);
        }

        [Test]
        public void SignalWaitingDoesNotAccumulateBlockedTime()
        {
            var recovery = new NpcStuckRecovery();
            Tick(recovery, 0, 299);
            Assert.That(
                recovery.ShouldRecover(Vector3.zero, Vector3.forward * 10, true, true, 600, 300),
                Is.False
            );
            Assert.That(Tick(recovery, 0, 299), Is.False);
            Assert.That(Tick(recovery, 0, 1), Is.True);
        }

        [Test]
        public void ConversationOrArrivalCancelsTheTimer()
        {
            var recovery = new NpcStuckRecovery();
            Tick(recovery, 0, 299);
            Assert.That(
                recovery.ShouldRecover(Vector3.zero, Vector3.forward * 10, false, false, 600, 300),
                Is.False
            );
            Assert.That(Tick(recovery, 0, 1), Is.False);
            Assert.That(
                recovery.ShouldRecover(
                    Vector3.forward * 10,
                    Vector3.forward * 10,
                    true,
                    false,
                    600,
                    300
                ),
                Is.False
            );
        }

        [Test]
        public void AChangedDestinationStartsANewJourney()
        {
            var recovery = new NpcStuckRecovery();
            Tick(recovery, 0, 299);
            Assert.That(
                recovery.ShouldRecover(Vector3.zero, Vector3.right * 10, true, false, 1, 300),
                Is.False
            );
        }

        private static bool Tick(NpcStuckRecovery recovery, float position, float seconds) =>
            recovery.ShouldRecover(
                Vector3.right * position,
                Vector3.forward * 10,
                true,
                false,
                seconds,
                300
            );
    }
}
