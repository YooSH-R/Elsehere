using System;
using System.IO;
using System.Text;

namespace A305.Voice
{
    /// <summary>녹음을 모노 16비트 PCM WAV로 만든다. 음성 인식 API(CONV-02)는 16kHz 모노 16비트 WAV를 받는다.</summary>
    public static class WavEncoder
    {
        private const short PcmFormat = 1;
        private const short MonoChannels = 1;
        private const short BitsPerSample = 16;
        private const short BytesPerSample = BitsPerSample / 8;
        private const int FormatChunkSize = 16;
        private const int HeaderSizeAfterRiffSize = 36;

        /// <param name="samples">-1~1 사이의 소리 값. 범위를 넘으면 끝값으로 자른다.</param>
        public static byte[] Encode(float[] samples, int sampleRate)
        {
            int dataSize = samples.Length * BytesPerSample;
            using var stream = new MemoryStream(HeaderSizeAfterRiffSize + 8 + dataSize);
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(HeaderSizeAfterRiffSize + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(FormatChunkSize);
            writer.Write(PcmFormat);
            writer.Write(MonoChannels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * BytesPerSample);
            writer.Write(BytesPerSample);
            writer.Write(BitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (float sample in samples)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, sample));
                writer.Write((short)Math.Round(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
