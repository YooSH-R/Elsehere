using NUnit.Framework;

namespace A305.Voice.Tests
{
    // getUserMedia가 실패하면 브라우저가 DOMException 이름을 준다. 화면 안내가 달라지므로 권한 거부와 장치 문제를 나눈다.
    public class BrowserMicrophoneErrorsTests
    {
        [TestCase("NotAllowedError")]
        [TestCase("SecurityError")]
        [TestCase("PermissionDeniedError")]
        public void PermissionErrors_AreDenied(string errorName)
        {
            Assert.That(
                BrowserMicrophoneErrors.ToAccess(errorName),
                Is.EqualTo(MicrophoneAccess.Denied)
            );
        }

        [TestCase("NotFoundError")]
        [TestCase("NotReadableError")]
        [TestCase("OverconstrainedError")]
        [TestCase("AbortError")]
        public void DeviceErrorsAndUnknownErrors_AreNoDevice(string errorName)
        {
            Assert.That(
                BrowserMicrophoneErrors.ToAccess(errorName),
                Is.EqualTo(MicrophoneAccess.NoDevice)
            );
        }

        [Test]
        public void MissingApiOrInsecurePage_IsUnsupported()
        {
            Assert.That(
                BrowserMicrophoneErrors.ToAccess(BrowserMicrophoneErrors.UnsupportedName),
                Is.EqualTo(MicrophoneAccess.Unsupported)
            );
        }
    }
}
