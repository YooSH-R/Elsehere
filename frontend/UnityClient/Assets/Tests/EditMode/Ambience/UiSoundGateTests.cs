using A305.World;
using NUnit.Framework;

namespace A305.Ambience.Tests
{
    public sealed class UiSoundGateTests
    {
        [Test]
        public void OrdinaryClicksAreSoftAndImportantActionsKeepTheirLevel()
        {
            Assert.That(UiSoundGate.Level(UiSound.Click), Is.LessThan(.15f));
            Assert.That(UiSoundGate.Level(UiSound.PrimaryClick), Is.EqualTo(.28f));
            Assert.That(UiSoundGate.Level(UiSound.Menu), Is.EqualTo(.25f));
        }

        [Test]
        public void MutingDoesNotConsumeTheNextAudibleClick()
        {
            var gate = new UiSoundGate();
            Assert.That(gate.TryPlay(UiSound.Click, false, 0), Is.False);
            Assert.That(gate.TryPlay(UiSound.Click, true, 0), Is.True);
        }

        [Test]
        public void FastClicksAndDuplicateCoinEventsDoNotStack()
        {
            var gate = new UiSoundGate();
            Assert.That(gate.TryPlay(UiSound.Click, true, 0), Is.True);
            Assert.That(gate.TryPlay(UiSound.Click, true, .03), Is.False);
            Assert.That(gate.TryPlay(UiSound.Click, true, .1), Is.True);
            Assert.That(gate.TryPlay(UiSound.Coin, true, .2), Is.True);
            Assert.That(gate.TryPlay(UiSound.Coin, true, .3), Is.False);
            Assert.That(gate.TryPlay(UiSound.Coin, true, 1.2), Is.True);
        }

        [Test]
        public void QuestJingleKeepsSimultaneousCoinJingleFromOverlapping()
        {
            var gate = new UiSoundGate();
            Assert.That(gate.TryPlay(UiSound.Quest, true, 1), Is.True);
            Assert.That(gate.TryPlay(UiSound.Coin, true, 1.4), Is.False);
            Assert.That(gate.TryPlay(UiSound.Coin, true, 2), Is.True);
        }

        [Test]
        public void SeparatePurchasesCanPlayAfterTheShortSpendSound()
        {
            var gate = new UiSoundGate();
            Assert.That(gate.TryPlay(UiSound.Spend, true, 0), Is.True);
            Assert.That(gate.TryPlay(UiSound.Spend, true, .1), Is.False);
            Assert.That(gate.TryPlay(UiSound.Spend, true, .4), Is.True);
        }

        [TestCase("EntryScene", false, true, 0f)]
        [TestCase("EntryScene", true, false, 0f)]
        [TestCase("TownScene", true, true, 0f)]
        [TestCase("EntryScene", true, true, .18f)]
        public void MusicNeedsTheEntryScreenAndUserGesture(
            string scene,
            bool gesture,
            bool enabled,
            float volume
        )
        {
            Assert.That(EntryMusicPlan.Volume(scene, gesture, enabled), Is.EqualTo(volume));
        }
    }
}
