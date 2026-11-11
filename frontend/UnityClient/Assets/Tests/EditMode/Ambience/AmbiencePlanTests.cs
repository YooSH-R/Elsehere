using NUnit.Framework;

namespace A305.Ambience.Tests
{
    public class AmbiencePlanTests
    {
        [TestCase("HomeScene", null, false, false, AmbienceBed.Room)]
        [TestCase("TownScene", null, false, false, AmbienceBed.Street)]
        [TestCase("HotelScene", null, false, false, AmbienceBed.Hotel)]
        [TestCase("CafeScene", null, false, false, AmbienceBed.Cafe)]
        [TestCase("TownScene", "DINER", false, false, AmbienceBed.Restaurant)]
        [TestCase("TownScene", "LAUNDROMAT", false, false, AmbienceBed.Laundromat)]
        [TestCase("TownScene", "FLOWER_SHOP", false, false, AmbienceBed.QuietShop)]
        [TestCase("TownScene", null, true, true, AmbienceBed.Taxi)]
        [TestCase("TownScene", null, false, true, AmbienceBed.Rain)]
        [TestCase("HotelScene", null, false, true, AmbienceBed.Hotel)]
        [TestCase("EntryScene", null, true, true, AmbienceBed.None)]
        [TestCase("PoliceStationScene", null, false, false, AmbienceBed.QuietShop)]
        public void ActualPlaceSelectsOnlyOneBed(
            string scene,
            string shop,
            bool taxi,
            bool rain,
            AmbienceBed expected
        ) => Assert.That(AmbiencePlan.Resolve(scene, shop, taxi, rain), Is.EqualTo(expected));

        [Test]
        public void ConversationLowersAmbienceWithoutChangingTheOriginalMix()
        {
            Assert.That(AmbiencePlan.Volume(.8f, false), Is.EqualTo(.8f));
            Assert.That(AmbiencePlan.Volume(.8f, true), Is.EqualTo(.12f).Within(.001f));
            Assert.That(AmbiencePlan.Volume(0, true), Is.Zero);
        }

        [Test]
        public void BoardingAndAlightingUseTheSameTaxiDoorVolume()
        {
            Assert.That(
                AmbiencePlan.EffectVolume(AmbienceEffect.TaxiDoorClose, .6f, true),
                Is.EqualTo(AmbiencePlan.EffectVolume(AmbienceEffect.TaxiDoorClose, .6f, false))
            );
            Assert.That(
                AmbiencePlan.EffectVolume(AmbienceEffect.DoorClose, .6f, true),
                Is.EqualTo(.09f).Within(.001f)
            );
        }

        [Test]
        public void OnePassingCarDoesNotRepeatUntilItLeavesAndReturns()
        {
            var gate = new VehicleSoundGate();
            Assert.That(gate.TryPass(0, 8, false), Is.False);
            Assert.That(gate.TryPass(1, 8, true), Is.True);
            Assert.That(gate.TryPass(30, 8, true), Is.False);
            gate.TryPass(31, 20, true);
            Assert.That(gate.TryPass(32, 8, true), Is.True);
        }

        [Test]
        public void HornRequiresNearbyStoppedTrafficWithGreenLightAndHasACooldown()
        {
            var gate = new VehicleSoundGate();
            Assert.That(gate.TryHorn(0, 2, false, false, 5), Is.False);
            Assert.That(gate.TryHorn(1, 2, false, true, 1), Is.False);
            Assert.That(gate.TryHorn(2, 2, false, true, 1), Is.True);
            Assert.That(gate.TryHorn(3, 2, false, true, 5), Is.False);
            Assert.That(gate.TryHorn(50, 20, false, true, 5), Is.False);
        }
    }
}
