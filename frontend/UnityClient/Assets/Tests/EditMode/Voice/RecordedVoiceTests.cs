using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace A305.Voice.Tests
{
    /// <summary>
    /// 짧게 누르거나 말소리가 거의 없는 녹음은 STT에 보내면 엉뚱한 문장이 나오므로 말소리가 있는 녹음만 전사한다.
    /// </summary>
    public class RecordedVoiceTests
    {
        private const int SampleRate = 16000;

        // VoiceRecorderSettings의 기본 무음 기준과 같다.
        private const float SilenceLevel = .02f;

        [Test]
        public void QuickTap_IsNotSpeech()
        {
            RecordedVoice voice = Voice(Tone(.15f, .3f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.False);
        }

        [Test]
        public void HoldWithoutSpeaking_IsNotSpeech()
        {
            RecordedVoice voice = Voice(Tone(1.5f, .005f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.False);
        }

        [Test]
        public void ClickInsideSilence_IsNotSpeech()
        {
            RecordedVoice voice = Voice(Tone(.5f, 0f), Tone(.05f, .5f), Tone(.5f, 0f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.False);
        }

        [Test]
        public void ShortAnswer_IsSpeech()
        {
            // "Yes" 한 마디 정도의 길이
            RecordedVoice voice = Voice(Tone(.3f, 0f), Tone(.3f, .2f), Tone(.3f, 0f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.True);
        }

        [Test]
        public void QuietAnswer_IsSpeech()
        {
            RecordedVoice voice = Voice(Tone(.3f, 0f), Tone(.5f, .01f), Tone(.3f, 0f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.True);
        }

        [Test]
        public void SteadyQuietNoise_IsNotSpeech()
        {
            RecordedVoice voice = Voice(Tone(1.5f, .012f));

            Assert.That(voice.HasSpeech(SilenceLevel), Is.False);
        }

        private static RecordedVoice Voice(params float[][] parts)
        {
            var samples = new List<float>();
            foreach (float[] part in parts)
                samples.AddRange(part);
            return new RecordedVoice(
                new byte[0],
                samples.ToArray(),
                SampleRate,
                samples.Count / (float)SampleRate,
                RecordingEndReason.UserStopped
            );
        }

        private static float[] Tone(float seconds, float amplitude)
        {
            var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * 220f * i / SampleRate);
            return samples;
        }
    }
}
