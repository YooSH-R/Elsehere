using A305.Voice;
using NUnit.Framework;

namespace A305.Dialogue.Tests
{
    public class VoiceInputNoticeTests
    {
        [Test]
        public void MicrophoneDenied_TellsHowToAllowPermissionOrType()
        {
            string notice = VoiceInputNotice.MicrophoneUnavailable(MicrophoneAccess.Denied);

            Assert.That(notice, Does.Contain("권한"));
            Assert.That(notice, Does.Contain("텍스트로 입력"));
        }

        [Test]
        public void MicrophoneMissing_AsksToCheckDeviceOrType()
        {
            string notice = VoiceInputNotice.MicrophoneUnavailable(MicrophoneAccess.NoDevice);

            Assert.That(notice, Does.Contain("마이크를 찾지 못했어요"));
            Assert.That(notice, Does.Contain("텍스트로 입력"));
        }

        [Test]
        public void MicrophoneUnsupported_SaysBrowserCannotRecordAndAsksToType()
        {
            string notice = VoiceInputNotice.MicrophoneUnavailable(MicrophoneAccess.Unsupported);

            Assert.That(notice, Does.Contain("브라우저"));
            Assert.That(notice, Does.Contain("텍스트로 입력"));
        }

        [Test]
        public void TooShortRecording_AsksToKeepPressingWhileSpeaking()
        {
            Assert.That(VoiceInputNotice.TooShort, Does.Contain("누른 채"));
        }
    }
}
