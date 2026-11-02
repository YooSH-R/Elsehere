namespace A305.Voice.Tests
{
    /// <summary>테스트용 마이크. 권한 답·음량·받은 소리를 테스트가 정하고, 열고 닫은 기록을 남긴다.</summary>
    public sealed class FakeMicrophone : IMicrophone
    {
        public MicrophoneAccess? Access { get; set; }
        public float CurrentLevel { get; set; }
        public CapturedAudio Captured { get; set; } =
            new CapturedAudio(new float[0], sampleRate: 0);

        /// <summary>녹음을 시작하자마자 소리를 받는지. false면 테스트가 <see cref="IsCapturing"/>을 켜 마이크가 열린 때를 정한다.</summary>
        public bool CapturesOnStart { get; set; } = true;

        public bool IsCapturing { get; set; }

        public int AccessRequestCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsKeptOpen { get; private set; }

        public void RequestAccess()
        {
            AccessRequestCount++;
        }

        public void KeepOpen()
        {
            IsKeptOpen = true;
        }

        public void Release()
        {
            IsKeptOpen = false;
        }

        public void StartRecording()
        {
            StartCount++;
            IsOpen = true;
            IsCapturing = CapturesOnStart;
        }

        public CapturedAudio StopRecording()
        {
            StopCount++;
            IsOpen = false;
            IsCapturing = false;
            return Captured;
        }
    }
}
