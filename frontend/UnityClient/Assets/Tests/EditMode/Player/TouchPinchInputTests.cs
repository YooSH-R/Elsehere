using NUnit.Framework;
using UnityEngine;

namespace A305.Player.Tests
{
    /// <summary>시점 영역에 댄 두 손가락 사이가 얼마나 벌어지고 좁아졌는지 모은다. 벌어지면 +, 좁아지면 -다.</summary>
    public class TouchPinchInputTests
    {
        [Test]
        public void SpreadingTwoFingers_AddsTheGrowthOfTheGapBetweenThem()
        {
            var pinch = new TouchPinchInput();
            pinch.Press(1, new Vector2(100f, 0f));
            pinch.Press(2, new Vector2(200f, 0f));

            pinch.Move(1, new Vector2(50f, 0f));
            pinch.Move(2, new Vector2(250f, 0f));

            Assert.That(pinch.IsPinching, Is.True);
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(100f).Within(.001f));
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(0f), "한 번 읽으면 비웁니다.");
        }

        [Test]
        public void BringingTwoFingersTogether_IsNegative()
        {
            var pinch = new TouchPinchInput();
            pinch.Press(1, new Vector2(0f, 0f));
            pinch.Press(2, new Vector2(0f, 300f));

            pinch.Move(2, new Vector2(0f, 120f));

            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(-180f).Within(.001f));
        }

        [Test]
        public void OneFingerAlone_DoesNotZoom()
        {
            var pinch = new TouchPinchInput();
            pinch.Press(1, Vector2.zero);
            pinch.Move(1, new Vector2(300f, 0f));

            Assert.That(pinch.IsPinching, Is.False);
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(0f));
        }

        [Test]
        public void LiftingAFinger_EndsThePinch_AndAThirdFingerIsIgnored()
        {
            var pinch = new TouchPinchInput();
            pinch.Press(1, Vector2.zero);
            pinch.Press(2, new Vector2(100f, 0f));
            pinch.Press(3, new Vector2(500f, 0f));
            pinch.Move(3, new Vector2(900f, 0f));
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(0f), "세 번째 손가락");

            pinch.Release(2);
            pinch.Move(1, new Vector2(-300f, 0f));

            Assert.That(pinch.IsPinching, Is.False);
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(0f));
        }

        [Test]
        public void Reset_ForgetsFingersAndPendingSpread()
        {
            var pinch = new TouchPinchInput();
            pinch.Press(1, Vector2.zero);
            pinch.Press(2, new Vector2(100f, 0f));
            pinch.Move(2, new Vector2(200f, 0f));

            pinch.Reset();

            Assert.That(pinch.IsPinching, Is.False);
            Assert.That(pinch.ConsumeSpread(), Is.EqualTo(0f));
        }
    }
}
