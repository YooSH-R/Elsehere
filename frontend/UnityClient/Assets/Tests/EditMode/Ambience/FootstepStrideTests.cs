using NUnit.Framework;

namespace A305.Ambience.Tests
{
    public sealed class FootstepStrideTests
    {
        [Test]
        public void WalkingGroundedProducesOneStepForEachStride()
        {
            var stride = new FootstepStride();
            int count = 0;
            for (int frame = 0; frame < 90; frame++)
                if (stride.Advance(.03f, .02f, true, true))
                    count++;
            Assert.That(count, Is.EqualTo(3));
        }

        [TestCase(0f, .02f, true, true)]
        [TestCase(.03f, .02f, false, true)]
        [TestCase(.03f, .02f, true, false)]
        [TestCase(3f, .02f, true, true)]
        [TestCase(.03f, 0f, true, true)]
        [TestCase(.03f, .3f, true, true)]
        public void WallJumpLockedTeleportPauseAndStallRemainSilent(
            float meters,
            float seconds,
            bool grounded,
            bool walking
        )
        {
            var stride = new FootstepStride();
            for (int frame = 0; frame < 100; frame++)
                Assert.That(stride.Advance(meters, seconds, grounded, walking), Is.False);
        }

        [Test]
        public void StopDiscardsIncompleteStride()
        {
            var stride = new FootstepStride();
            for (int frame = 0; frame < 20; frame++)
                Assert.That(stride.Advance(.03f, .02f, true, true), Is.False);
            stride.Advance(0, .02f, true, false);
            for (int frame = 0; frame < 20; frame++)
                Assert.That(stride.Advance(.03f, .02f, true, true), Is.False);
        }
    }
}
