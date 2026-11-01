using UnityEngine;

namespace A305.Voice
{
    /// <summary>음성 인식에 보낼 녹음 하나. 16kHz 모노 16비트 WAV와 다시 듣기용 샘플을 함께 둔다.</summary>
    public sealed class RecordedVoice
    {
        // "Yes" 한 마디(약 0.3초)는 넘기고 짧은 누름·잡음은 거르는 말소리 길이
        private const float MinSpeechSeconds = .25f;
        private const float WindowSeconds = .02f;

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

        /// <summary>
        /// 말소리로 볼 만큼 큰 소리가 합쳐서 <see cref="MinSpeechSeconds"/> 이상 있는지. 짧게 눌렀거나 말하지 않은 녹음을 STT에 보내면
        /// 엉뚱한 문장이 나오므로 이때는 전사하지 않는다.
        /// </summary>
        /// <param name="level">이 음량(RMS)보다 큰 구간을 말소리로 본다. 자동 종료의 무음 기준과 같은 값을 쓴다.</param>
        public bool HasSpeech(float level)
        {
            int window = Mathf.Max(1, Mathf.RoundToInt(SampleRate * WindowSeconds));
            int loudWindows = 0;
            for (int start = 0; start + window <= Samples.Length; start += window)
            {
                float sum = 0f;
                for (int i = start; i < start + window; i++)
                    sum += Samples[i] * Samples[i];
                if (Mathf.Sqrt(sum / window) >= level)
                    loudWindows++;
            }

            return loudWindows * window >= MinSpeechSeconds * SampleRate;
        }
    }
}
