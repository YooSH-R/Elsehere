using NUnit.Framework;

namespace A305.Voice.Tests
{
    // 녹음 규칙(API 명세 CONV-02): 최대 시간에 닿거나 조용한 상태가 정한 시간만큼 이어지면 자동 종료.
    // 아래 15초·3초는 규칙을 확인하려고 고른 값이다. 실제 값은 Data/Voice/VoiceRecorderSettings.asset에 있다.
    public class RecordingSessionTests
    {
        private const float Quiet = 0.005f;
        private const float Speaking = 0.2f;

        private RecordingSession session;

        [SetUp]
        public void StartSession()
        {
            session = new RecordingSession(
                maxSeconds: 15f,
                silenceSeconds: 3f,
                silenceLevel: 0.02f
            );
            session.Start(now: 10f);
        }

        [Test]
        public void Started_IsRecordingWithoutEndReason()
        {
            Assert.That(session.IsRecording, Is.True);
            Assert.That(session.EndReason, Is.Null);
        }

        [Test]
        public void QuietFromStart_EndsAfterSilenceSeconds()
        {
            Assert.That(session.Update(12.9f, Quiet), Is.False);

            bool ended = session.Update(13f, Quiet);

            Assert.That(ended, Is.True);
            Assert.That(session.IsRecording, Is.False);
            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.Silence));
        }

        [Test]
        public void Speaking_RestartsSilenceCountFromNextQuietFrame()
        {
            // 시각은 부동소수점 오차가 없는 값(0.25 단위)으로 두어 3초 경계가 흔들리지 않게 한다.
            session.Update(12.5f, Quiet);
            session.Update(12.75f, Speaking);
            session.Update(13f, Quiet);

            Assert.That(session.Update(15.75f, Quiet), Is.False);
            Assert.That(session.Update(16f, Quiet), Is.True);
            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.Silence));
        }

        [Test]
        public void SpeakingUntilMaxSeconds_EndsAtMaxDuration()
        {
            Assert.That(session.Update(24.9f, Speaking), Is.False);

            bool ended = session.Update(25f, Speaking);

            Assert.That(ended, Is.True);
            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.MaxDuration));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void StoppedByUser_EndsByUserWithElapsedTime()
        {
            session.Update(11f, Speaking);

            session.Stop(12.4f);

            Assert.That(session.IsRecording, Is.False);
            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.UserStopped));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(2.4f).Within(0.001f));
        }

        [Test]
        public void LeavingTab_EndsAsInterrupted()
        {
            session.Interrupt(11f);

            Assert.That(session.IsRecording, Is.False);
            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.Interrupted));
        }

        [Test]
        public void LoudFrame_HearsVoiceUntilNextQuietFrame()
        {
            Assert.That(session.IsHearingVoice, Is.False);

            session.Update(10.25f, Speaking);
            Assert.That(session.IsHearingVoice, Is.True);

            session.Update(10.5f, Quiet);
            Assert.That(session.IsHearingVoice, Is.False);
        }

        [Test]
        public void StoppedWhileSpeaking_NoLongerHearsVoice()
        {
            session.Update(10.25f, Speaking);

            session.Stop(10.5f);

            Assert.That(session.IsHearingVoice, Is.False);
        }

        [Test]
        public void AfterEnding_LaterUpdatesAndStopsDoNotChangeResult()
        {
            session.Stop(11f);

            Assert.That(session.Update(30f, Quiet), Is.False);
            session.Interrupt(31f);

            Assert.That(session.EndReason, Is.EqualTo(RecordingEndReason.UserStopped));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
