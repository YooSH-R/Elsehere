using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    public class PointerDeltaScaleTests
    {
        [Test]
        public void ToLogicalPixels_OnHighDensityScreen_DividesByPixelRatio()
        {
            Vector2 delta = PointerDeltaScale.ToLogicalPixels(new Vector2(10f, -4f), 2f);

            Assert.That(delta, Is.EqualTo(new Vector2(5f, -2f)));
        }

        [Test]
        public void ToLogicalPixels_OnStandardScreen_KeepsDelta()
        {
            Vector2 delta = PointerDeltaScale.ToLogicalPixels(new Vector2(10f, -4f), 1f);

            Assert.That(delta, Is.EqualTo(new Vector2(10f, -4f)));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void ToLogicalPixels_WithInvalidPixelRatio_KeepsDelta(float pixelRatio)
        {
            Vector2 delta = PointerDeltaScale.ToLogicalPixels(new Vector2(10f, -4f), pixelRatio);

            Assert.That(delta, Is.EqualTo(new Vector2(10f, -4f)));
        }
    }
}
