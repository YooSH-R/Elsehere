using NUnit.Framework;

namespace A305.Player.Tests
{
    public class LookSensitivityTests
    {
        private float saved;

        [SetUp]
        public void RememberCurrent()
        {
            saved = LookSensitivity.Current;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            LookSensitivity.Current = saved;
        }

        [Test]
        public void Clamp_KeepsValuesInsideRange()
        {
            Assert.That(LookSensitivity.Clamp(1.3f), Is.EqualTo(1.3f));
        }

        [TestCase(0.1f, LookSensitivity.Min)]
        [TestCase(5f, LookSensitivity.Max)]
        [TestCase(float.NaN, LookSensitivity.Default)]
        public void Clamp_LimitsOutOfRangeValues(float value, float expected)
        {
            Assert.That(LookSensitivity.Clamp(value), Is.EqualTo(expected));
        }

        [Test]
        public void Current_KeepsTheChosenValueAfterReload()
        {
            LookSensitivity.Current = 1.5f;
            LookSensitivity.Reload();

            Assert.That(LookSensitivity.Current, Is.EqualTo(1.5f));
        }

        [Test]
        public void Current_StoresOnlyValuesInsideRange()
        {
            LookSensitivity.Current = 9f;

            Assert.That(LookSensitivity.Current, Is.EqualTo(LookSensitivity.Max));
        }

        [Test]
        public void Changed_TellsListenersTheNewValue()
        {
            float received = 0f;
            void OnChanged(float value) => received = value;
            LookSensitivity.Changed += OnChanged;
            try
            {
                LookSensitivity.Current = 0.8f;
            }
            finally
            {
                LookSensitivity.Changed -= OnChanged;
            }

            Assert.That(received, Is.EqualTo(0.8f));
        }
    }
}
