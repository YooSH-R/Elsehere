using System.Linq;
using NUnit.Framework;

namespace A305.Voice.Tests
{
    // 브라우저는 보통 44.1kHz·48kHz로 녹음하므로 음성 인식에 보낼 16kHz로 줄인다.
    public class AudioResamplerTests
    {
        [Test]
        public void SameRate_ReturnsSameSamples()
        {
            float[] samples = { 0.1f, -0.2f, 0.3f };

            Assert.That(AudioResampler.Resample(samples, 16000, 16000), Is.EqualTo(samples));
        }

        [TestCase(48000, 48000, 16000)]
        [TestCase(44100, 44100, 16000)]
        public void Downsampling_KeepsDurationAtTargetRate(
            int sourceRate,
            int sourceCount,
            int expectedCount
        )
        {
            float[] samples = Enumerable.Repeat(0.5f, sourceCount).ToArray();

            float[] resampled = AudioResampler.Resample(samples, sourceRate, 16000);

            Assert.That(resampled.Length, Is.EqualTo(expectedCount));
        }

        [Test]
        public void Downsampling_KeepsConstantLevel()
        {
            float[] samples = Enumerable.Repeat(0.5f, 4800).ToArray();

            float[] resampled = AudioResampler.Resample(samples, 48000, 16000);

            Assert.That(resampled, Is.All.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Downsampling_AveragesSamplesInEachOutputStep()
        {
            // 48kHz → 16kHz는 입력 3개가 출력 1개가 된다. 건너뛰지 않고 평균해 잡음이 접혀 들어오는 것을 줄인다.
            float[] samples = { 1f, 1f, 1f, 0f, 0f, 0f, 0.3f, 0.6f, 0.9f };

            float[] resampled = AudioResampler.Resample(samples, 48000, 16000);

            Assert.That(resampled, Is.EqualTo(new[] { 1f, 0f, 0.6f }).Within(0.0001f));
        }
    }
}
