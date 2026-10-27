using NUnit.Framework;

namespace A305.Player.Tests
{
    public class TouchControlsAvailabilityTests
    {
        [Test]
        public void DesktopBrowser_HidesControls_EvenWhenTouchscreenIsRegistered()
        {
            // Unity Web은 PC 브라우저에도 Touchscreen 장치를 등록한다. 터치 노트북도 같은 값이다.
            bool shouldShow = TouchControlsAvailability.ShouldShow(
                isWebBrowser: true,
                hasTouchscreen: true,
                isMobileBrowser: false,
                isPrimaryPointerCoarse: false
            );

            Assert.That(shouldShow, Is.False);
        }

        [Test]
        public void MobileBrowser_ShowsControls()
        {
            bool shouldShow = TouchControlsAvailability.ShouldShow(
                isWebBrowser: true,
                hasTouchscreen: true,
                isMobileBrowser: true,
                isPrimaryPointerCoarse: true
            );

            Assert.That(shouldShow, Is.True);
        }

        [Test]
        public void DesktopUserAgentWithFingerAsPrimaryPointer_ShowsControls()
        {
            // iPadOS Safari는 기본으로 Mac UA를 보내 Application.isMobilePlatform이 false다.
            bool shouldShow = TouchControlsAvailability.ShouldShow(
                isWebBrowser: true,
                hasTouchscreen: true,
                isMobileBrowser: false,
                isPrimaryPointerCoarse: true
            );

            Assert.That(shouldShow, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OutsideBrowser_FollowsTouchscreenDevice(bool hasTouchscreen)
        {
            // Editor의 Device Simulator와 PlayMode 테스트는 Touchscreen 장치를 붙여 터치 조작을 확인한다.
            bool shouldShow = TouchControlsAvailability.ShouldShow(
                isWebBrowser: false,
                hasTouchscreen: hasTouchscreen,
                isMobileBrowser: false,
                isPrimaryPointerCoarse: false
            );

            Assert.That(shouldShow, Is.EqualTo(hasTouchscreen));
        }
    }
}
