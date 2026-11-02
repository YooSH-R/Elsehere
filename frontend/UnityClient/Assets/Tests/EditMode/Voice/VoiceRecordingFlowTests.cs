using System.Collections.Generic;
using NUnit.Framework;

namespace A305.Voice.Tests
{
    // [말하기] 녹음의 진행. 브라우저 마이크 대신 FakeMicrophone으로 권한 답, 녹음 끝, 자동 종료를 확인한다.
    public class VoiceRecordingFlowTests
    {
        private const float Quiet = 0.005f;
        private const float Speaking = 0.2f;

        private FakeMicrophone microphone;
        private VoiceRecordingFlow flow;
        private List<MicrophoneAccess> announcedAccess;
        private List<RecordedVoice> finished;

        [SetUp]
        public void CreateFlow()
        {
            microphone = new FakeMicrophone();
            flow = new VoiceRecordingFlow(
                microphone,
                new RecordingSession(maxSeconds: 15f, silenceSeconds: 3f, silenceLevel: 0.02f)
            );
            announcedAccess = new List<MicrophoneAccess>();
            finished = new List<RecordedVoice>();
            flow.AccessChanged += announcedAccess.Add;
            flow.RecordingFinished += finished.Add;
        }

        [TestCase(MicrophoneAccess.Granted)]
        [TestCase(MicrophoneAccess.Denied)]
        public void RequestAccess_AnnouncesBrowserAnswerOnce(MicrophoneAccess answer)
        {
            flow.RequestAccess();
            flow.Update(10f);
            Assert.That(announcedAccess, Is.Empty);

            microphone.Access = answer;
            flow.Update(10.25f);
            flow.Update(10.5f);

            Assert.That(microphone.AccessRequestCount, Is.EqualTo(1));
            Assert.That(announcedAccess, Is.EqualTo(new[] { answer }));
        }

        [Test]
        public void StoppedByUser_FinishesWithSixteenKilohertzWav()
        {
            microphone.Access = MicrophoneAccess.Granted;
            microphone.CurrentLevel = Speaking;
            // 브라우저에서 흔한 48kHz로 0.1초 받은 소리.
            microphone.Captured = new CapturedAudio(new float[4800], sampleRate: 48000);

            flow.StartRecording(10f);
            flow.Update(11f);
            flow.StopRecording(12f);

            Assert.That(flow.IsRecording, Is.False);
            Assert.That(microphone.IsOpen, Is.False);
            Assert.That(finished, Has.Count.EqualTo(1));
            RecordedVoice voice = finished[0];
            Assert.That(voice.EndReason, Is.EqualTo(RecordingEndReason.UserStopped));
            Assert.That(voice.DurationSeconds, Is.EqualTo(2f).Within(0.001f));
            Assert.That(voice.SampleRate, Is.EqualTo(16000));
            Assert.That(voice.Samples.Length, Is.EqualTo(1600));
            Assert.That(voice.WavBytes.Length, Is.EqualTo(44 + 1600 * 2));
        }

        [Test]
        public void SpeakingWhileRecording_HearsVoiceUntilStopped()
        {
            microphone.Access = MicrophoneAccess.Granted;
            microphone.CurrentLevel = Speaking;
            flow.StartRecording(10f);
            Assert.That(flow.IsHearingVoice, Is.False);

            flow.Update(10.25f);
            Assert.That(flow.IsHearingVoice, Is.True);

            flow.StopRecording(10.5f);
            Assert.That(flow.IsHearingVoice, Is.False);
        }

        [Test]
        public void QuietForSilenceSeconds_FinishesBySilence()
        {
            microphone.CurrentLevel = Quiet;
            flow.StartRecording(10f);

            flow.Update(12.5f);
            Assert.That(finished, Is.Empty);
            flow.Update(13f);

            Assert.That(finished, Has.Count.EqualTo(1));
            Assert.That(finished[0].EndReason, Is.EqualTo(RecordingEndReason.Silence));
            Assert.That(microphone.IsOpen, Is.False);
        }

        [Test]
        public void StoppedBeforeMicrophoneOpened_FinishesWithEmptyWav()
        {
            flow.StartRecording(10f);
            flow.StopRecording(10.25f);

            Assert.That(finished, Has.Count.EqualTo(1));
            Assert.That(finished[0].Samples, Is.Empty);
            Assert.That(finished[0].WavBytes.Length, Is.EqualTo(44));
        }

        [Test]
        public void LeavingTab_FinishesAsInterrupted()
        {
            flow.StartRecording(10f);
            flow.Interrupt(11f);

            Assert.That(finished, Has.Count.EqualTo(1));
            Assert.That(finished[0].EndReason, Is.EqualTo(RecordingEndReason.Interrupted));
            Assert.That(microphone.IsOpen, Is.False);
        }

        [Test]
        public void StartingAgainWhileRecording_KeepsFirstRecording()
        {
            flow.StartRecording(10f);
            flow.StartRecording(11f);
            flow.StopRecording(12f);

            Assert.That(microphone.StartCount, Is.EqualTo(1));
            Assert.That(finished[0].DurationSeconds, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void MicrophoneFailsWhileRecording_CancelsAndAnnouncesAnswer()
        {
            // 권한을 묻지 않고 바로 눌렀거나, 거부했던 권한으로 다시 누른 경우. 브라우저는 녹음을 시작할 때 마이크를 연다.
            flow.StartRecording(10f);
            flow.Update(10.25f);
            Assert.That(flow.IsRecording, Is.True);

            microphone.Access = MicrophoneAccess.Denied;
            flow.Update(10.5f);
            flow.Update(10.75f);

            Assert.That(flow.IsRecording, Is.False);
            Assert.That(microphone.IsOpen, Is.False);
            Assert.That(finished, Is.Empty);
            Assert.That(announcedAccess, Is.EqualTo(new[] { MicrophoneAccess.Denied }));
        }

        [Test]
        public void MicrophoneStillOpening_IsPreparingUntilSoundArrives()
        {
            microphone.CapturesOnStart = false;
            flow.StartRecording(10f);
            flow.Update(10.25f);

            Assert.That(flow.IsRecording, Is.True);
            Assert.That(flow.IsPreparing, Is.True);

            microphone.IsCapturing = true;
            flow.Update(10.5f);

            Assert.That(flow.IsPreparing, Is.False);
        }

        [Test]
        public void MicrophoneAlreadyOpen_IsNotPreparing()
        {
            flow.StartRecording(10f);

            Assert.That(flow.IsRecording, Is.True);
            Assert.That(flow.IsPreparing, Is.False);
        }

        // 마이크가 열리는 동안은 말해도 녹음되지 않으므로 그 시간을 무음으로 세면 말하기도 전에 끝난다.
        [Test]
        public void SilenceIsCountedFromWhenSoundArrives()
        {
            microphone.CurrentLevel = Quiet;
            microphone.CapturesOnStart = false;
            flow.StartRecording(10f);
            flow.Update(14f);
            Assert.That(finished, Is.Empty);

            microphone.IsCapturing = true;
            flow.Update(14.25f);
            flow.Update(17f);
            Assert.That(finished, Is.Empty);
            flow.Update(17.25f);

            Assert.That(finished, Has.Count.EqualTo(1));
            Assert.That(finished[0].EndReason, Is.EqualTo(RecordingEndReason.Silence));
            Assert.That(finished[0].DurationSeconds, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void StoppedWhilePreparing_FinishesWithEmptyWav()
        {
            microphone.CapturesOnStart = false;
            flow.StartRecording(10f);
            flow.Update(10.25f);
            flow.StopRecording(10.5f);

            Assert.That(flow.IsRecording, Is.False);
            Assert.That(flow.IsPreparing, Is.False);
            Assert.That(finished, Has.Count.EqualTo(1));
            Assert.That(finished[0].Samples, Is.Empty);
        }

        [Test]
        public void KeepMicrophoneOpen_HoldsMicrophoneUntilReleased()
        {
            flow.KeepMicrophoneOpen();
            Assert.That(microphone.IsKeptOpen, Is.True);

            flow.ReleaseMicrophone();
            Assert.That(microphone.IsKeptOpen, Is.False);
        }

        [Test]
        public void StoppingOrLeavingWithoutRecording_FinishesNothing()
        {
            flow.StopRecording(10f);
            flow.Interrupt(11f);

            Assert.That(microphone.StopCount, Is.EqualTo(0));
            Assert.That(finished, Is.Empty);
        }
    }
}
