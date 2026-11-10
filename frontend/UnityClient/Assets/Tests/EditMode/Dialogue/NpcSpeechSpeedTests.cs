using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class NpcSpeechSpeedTests
    {
        private float saved;

        [SetUp]
        public void RememberCurrent()
        {
            saved = NpcSpeechSpeed.Current;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            NpcSpeechSpeed.Current = saved;
        }

        [Test]
        public void Range_MatchesTheServerSettingRange()
        {
            Assert.That(NpcSpeechSpeed.Min, Is.EqualTo(0.8f));
            Assert.That(NpcSpeechSpeed.Max, Is.EqualTo(1.5f));
            Assert.That(NpcSpeechSpeed.Default, Is.EqualTo(1.1f));
        }

        [TestCase(0.1f, NpcSpeechSpeed.Min)]
        [TestCase(5f, NpcSpeechSpeed.Max)]
        [TestCase(float.NaN, NpcSpeechSpeed.Default)]
        public void Clamp_LimitsOutOfRangeValues(float value, float expected)
        {
            Assert.That(NpcSpeechSpeed.Clamp(value), Is.EqualTo(expected));
        }

        [Test]
        public void Clamp_RoundsToTheSliderStep()
        {
            Assert.That(NpcSpeechSpeed.Clamp(1.26f), Is.EqualTo(1.3f).Within(0.0001f));
        }

        [Test]
        public void Current_KeepsTheChosenValueAfterReload()
        {
            NpcSpeechSpeed.Current = 1.4f;
            NpcSpeechSpeed.Reload();

            Assert.That(NpcSpeechSpeed.Current, Is.EqualTo(1.4f).Within(0.0001f));
        }

        [Test]
        public void ForRequest_SendsTheChosenValueWithoutFloatNoise()
        {
            NpcSpeechSpeed.Current = 1.3f;

            Assert.That(NpcSpeechSpeed.ForRequest, Is.EqualTo(1.3d));
        }
    }
}
