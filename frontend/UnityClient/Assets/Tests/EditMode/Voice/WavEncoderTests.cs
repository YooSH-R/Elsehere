using System;
using System.Text;
using NUnit.Framework;

namespace A305.Voice.Tests
{
    // 음성 인식 API(CONV-02)에 보내는 16kHz 모노 16비트 PCM WAV 형식을 확인한다.
    public class WavEncoderTests
    {
        [Test]
        public void Encode_WritesMono16BitPcmHeader()
        {
            byte[] wav = WavEncoder.Encode(new float[3], 16000);

            Assert.That(wav.Length, Is.EqualTo(44 + 3 * 2));
            Assert.That(Text(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt32(wav, 4), Is.EqualTo(36 + 6));
            Assert.That(Text(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(Text(wav, 12, 4), Is.EqualTo("fmt "));
            Assert.That(BitConverter.ToInt32(wav, 16), Is.EqualTo(16));
            Assert.That(BitConverter.ToInt16(wav, 20), Is.EqualTo(1), "PCM");
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo(1), "채널 수");
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(16000), "샘플레이트");
            Assert.That(BitConverter.ToInt32(wav, 28), Is.EqualTo(32000), "초당 바이트");
            Assert.That(BitConverter.ToInt16(wav, 32), Is.EqualTo(2), "샘플당 바이트");
            Assert.That(BitConverter.ToInt16(wav, 34), Is.EqualTo(16), "샘플당 비트");
            Assert.That(Text(wav, 36, 4), Is.EqualTo("data"));
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(6), "데이터 크기");
        }

        [Test]
        public void Encode_ConvertsSamplesTo16BitIntegers()
        {
            byte[] wav = WavEncoder.Encode(new[] { 0f, 1f, -1f, 0.25f }, 16000);

            Assert.That(
                new[] { Sample(wav, 0), Sample(wav, 1), Sample(wav, 2), Sample(wav, 3) },
                Is.EqualTo(new short[] { 0, 32767, -32767, 8192 })
            );
        }

        [Test]
        public void Encode_ClampsSamplesOutsideRange()
        {
            byte[] wav = WavEncoder.Encode(new[] { 1.5f, -2f }, 16000);

            Assert.That(Sample(wav, 0), Is.EqualTo(32767));
            Assert.That(Sample(wav, 1), Is.EqualTo(-32767));
        }

        private static string Text(byte[] bytes, int offset, int count)
        {
            return Encoding.ASCII.GetString(bytes, offset, count);
        }

        private static short Sample(byte[] wav, int index)
        {
            return BitConverter.ToInt16(wav, 44 + index * 2);
        }
    }
}
