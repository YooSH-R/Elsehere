namespace A305.Voice
{
    /// <summary>마이크에서 받은 그대로의 모노 소리. 샘플레이트는 기기·브라우저마다 다르다.</summary>
    public sealed class CapturedAudio
    {
        public CapturedAudio(float[] samples, int sampleRate)
        {
            Samples = samples;
            SampleRate = sampleRate;
        }

        public float[] Samples { get; }
        public int SampleRate { get; }
    }
}
