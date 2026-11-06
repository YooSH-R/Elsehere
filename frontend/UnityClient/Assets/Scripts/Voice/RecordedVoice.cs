using System;
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
        /// 말소리로 볼 만큼 큰 소리가 합쳐서 <see cref="MinSpeechSeconds"/> 이상 있는지 확인한다.
        /// 기존 기준에 미달하는 작은 소리는 녹음의 바탕 음량과 비교해 한 번 더 확인한다.
        /// </summary>
        /// <param name="level">자동 종료에 쓰는 무음 기준 음량(RMS). 작은 목소리의 보조 판정에도 사용한다.</param>
        public bool HasSpeech(float level)
        {
            int window = Mathf.Max(1, Mathf.RoundToInt(SampleRate * WindowSeconds));
            var levels = new float[Samples.Length / window];
            int loudWindows = 0;
            int index = 0;
            for (int start = 0; start + window <= Samples.Length; start += window)
            {
                float sum = 0f;
                for (int i = start; i < start + window; i++)
                    sum += Samples[i] * Samples[i];
                float windowLevel = Mathf.Sqrt(sum / window);
                levels[index++] = windowLevel;
                if (windowLevel >= level)
                    loudWindows++;
            }

            float requiredSamples = MinSpeechSeconds * SampleRate;
            if (loudWindows * window >= requiredSamples)
                return true;
            if (levels.Length == 0)
                return false;

            // 작은 목소리는 고정 무음 기준보다 작을 수 있다. 낮은 음량 구간으로 바탕 잡음을 추정해
            // 일정한 잡음까지 말로 취급하지 않도록 한다. 기존 기준을 넘은 녹음은 위에서 그대로 통과한다.
            Array.Sort(levels);
            float noiseLevel = levels[(levels.Length - 1) / 5];
            float quietSpeechLevel = Mathf.Max(level * .3f, noiseLevel * 1.8f);
            int quietSpeechWindows = 0;
            foreach (float windowLevel in levels)
                if (windowLevel >= quietSpeechLevel)
                    quietSpeechWindows++;

            return quietSpeechWindows * window >= requiredSamples;
        }
    }
}
