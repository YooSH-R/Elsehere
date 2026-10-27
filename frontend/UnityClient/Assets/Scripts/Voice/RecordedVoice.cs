namespace A305.Voice
{
    /// <summary>음성 인식에 보낼 녹음 하나. 16kHz 모노 16비트 WAV와 다시 듣기용 샘플을 함께 둔다.</summary>
    public sealed class RecordedVoice
    {
        public RecordedVoice(
            byte[] wavBytes,
            float[] samples,
            int sampleRate,
            float durationSeconds,
            RecordingEndReason endReason
        )
        {
            WavBytes = wavBytes;
            Samples = samples;
            SampleRate = sampleRate;
            DurationSeconds = durationSeconds;
            EndReason = endReason;
        }

        /// <summary>CONV-02 <c>audio</c>에 <c>audio/wav</c>로 담아 보낸다.</summary>
        public byte[] WavBytes { get; }

        public float[] Samples { get; }
        public int SampleRate { get; }
        public float DurationSeconds { get; }
        public RecordingEndReason EndReason { get; }
    }
}
