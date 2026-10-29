using System;

namespace A305.Voice
{
    /// <summary>
    /// 누르고 말하기 녹음의 진행. 마이크 권한 답을 알리고, 매 프레임 음량과 시각을 <see cref="RecordingSession"/>에 넘겨
    /// 자동 종료를 판단하고, 끝나면 16kHz 모노 WAV로 만들어 <see cref="RecordingFinished"/>로 알린다.
    /// </summary>
    /// <remarks>시각은 부르는 쪽이 넘겨 Unity 객체 없이 테스트한다. 화면 연결은 <see cref="VoiceRecorder"/>가 맡는다.</remarks>
    public sealed class VoiceRecordingFlow
    {
        /// <summary>음성 인식 API(CONV-02)가 받는 샘플레이트.</summary>
        public const int TargetSampleRate = 16000;

        private readonly IMicrophone microphone;
        private readonly RecordingSession session;
        private bool isWaitingForAccess;

        public VoiceRecordingFlow(IMicrophone microphone, RecordingSession session)
        {
            this.microphone = microphone;
            this.session = session;
        }

        public event Action<MicrophoneAccess> AccessChanged;
        public event Action<RecordedVoice> RecordingFinished;

        public MicrophoneAccess? Access => microphone.Access;
        public bool IsRecording => session.IsRecording;
        public float CurrentLevel => IsRecording ? microphone.CurrentLevel : 0f;
        public float ElapsedSeconds => session.ElapsedSeconds;

        public void RequestAccess()
        {
            microphone.RequestAccess();
            isWaitingForAccess = true;
        }

        public void StartRecording(float now)
        {
            if (IsRecording)
            {
                return;
            }

            microphone.StartRecording();
            session.Start(now);
        }

        /// <summary>[누르고 말하기] 버튼을 뗐을 때 부른다.</summary>
        public void StopRecording(float now)
        {
            if (!IsRecording)
            {
                return;
            }

            session.Stop(now);
            Finish();
        }

        /// <summary>녹음 중 다른 탭·창으로 갔을 때 부른다.</summary>
        public void Interrupt(float now)
        {
            if (!IsRecording)
            {
                return;
            }

            session.Interrupt(now);
            Finish();
        }

        /// <summary>매 프레임 부른다.</summary>
        public void Update(float now)
        {
            if (IsRecording && HasMicrophoneFailed)
            {
                CancelRecording(now);
                return;
            }

            // 브라우저는 권한 요청에 비동기로 답하므로 답이 올 때까지 매 프레임 확인한다.
            if (isWaitingForAccess && microphone.Access != null)
            {
                isWaitingForAccess = false;
                AccessChanged?.Invoke(microphone.Access.Value);
            }

            if (IsRecording && session.Update(now, microphone.CurrentLevel))
            {
                Finish();
            }
        }

        private bool HasMicrophoneFailed =>
            microphone.Access != null && microphone.Access != MicrophoneAccess.Granted;

        // 녹음을 시작하며 마이크를 열지 못했다. 받은 소리가 없으므로 녹음 결과 대신 권한 답을 알린다.
        private void CancelRecording(float now)
        {
            session.Interrupt(now);
            microphone.StopRecording();
            isWaitingForAccess = false;
            AccessChanged?.Invoke(microphone.Access.Value);
        }

        private void Finish()
        {
            CapturedAudio captured = microphone.StopRecording();
            // 마이크가 열리기 전에 끝나면 받은 소리가 없고 샘플레이트도 모른다.
            float[] samples =
                captured.Samples.Length == 0
                    ? Array.Empty<float>()
                    : AudioResampler.Resample(
                        captured.Samples,
                        captured.SampleRate,
                        TargetSampleRate
                    );
            RecordingFinished?.Invoke(
                new RecordedVoice(
                    WavEncoder.Encode(samples, TargetSampleRate),
                    samples,
                    TargetSampleRate,
                    session.ElapsedSeconds,
                    session.EndReason ?? RecordingEndReason.UserStopped
                )
            );
        }
    }
}
