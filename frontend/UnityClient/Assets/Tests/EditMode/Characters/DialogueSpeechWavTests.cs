using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace A305.Characters.Tests
{
    public sealed class DialogueSpeechWavTests
    {
        internal static byte[] Wav(bool withTiming = true, bool corruptHash = false)
        {
            var pcm = new byte[32000];
            pcm[0] = 255;
            pcm[1] = 127;
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(pcm)).Replace("-", "");
            var json =
                "{\"version\":2,\"provider\":\"kokoro-en-US\",\"duration\":1,"
                + "\"pcmSha256\":\""
                + (corruptHash ? "wrong" : hash)
                + "\",\"phonemes\":["
                + "{\"start\":0.1,\"end\":0.4,\"phoneme\":\"ay\"},"
                + "{\"start\":0.4,\"end\":0.6,\"phoneme\":\"f\"},"
                + "{\"start\":0.6,\"end\":0.9,\"phoneme\":\"th\"}]}";
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm.Length);
            writer.Write(pcm);
            if (withTiming)
            {
                byte[] payload = Encoding.UTF8.GetBytes(json);
                writer.Write(Encoding.ASCII.GetBytes("a305"));
                writer.Write(payload.Length);
                writer.Write(payload);
                if (payload.Length % 2 != 0)
                    writer.Write((byte)0);
            }
            stream.Position = 4;
            writer.Write((int)stream.Length - 8);
            return stream.ToArray();
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void MissingOrMismatchedTimingKeepsAudioWithoutInventingMouthCues(
            bool timing,
            bool tampered
        )
        {
            var speech = DialogueSpeechWav.Read(Wav(timing, tampered));
            Assert.That(speech.Samples.Length, Is.EqualTo(16000));
            Assert.That(speech.Timeline, Is.Null);
            Assert.That(speech.TimingError, Is.Not.Empty);
        }

        [Test]
        public void TruncatedAudioIsRejectedBeforeItCanPlay()
        {
            byte[] bytes = Wav();
            Array.Resize(ref bytes, bytes.Length - 3);
            Assert.Throws<ArgumentException>(() => DialogueSpeechWav.Read(bytes));
        }

        [Test]
        public void DecodesPcmAndUsesMatchingPhonemesInsteadOfTextLetterGuesses()
        {
            var speech = DialogueSpeechWav.Read(Wav());
            Assert.That(speech.Timeline, Is.Not.Null);
            Assert.That(speech.Frequency, Is.EqualTo(16000));
            Assert.That(speech.Samples.Length, Is.EqualTo(16000));
            Assert.That(speech.Samples[0], Is.EqualTo(32767f / 32768));
            Assert.That(
                speech.Timeline.SampleArticulation(.166f).Weight(CharacterFacePose.OpenMouth),
                Is.GreaterThan(.7)
            );
            Assert.That(
                speech.Timeline.SampleArticulation(.5f).Weight(CharacterFacePose.FV),
                Is.GreaterThan(.8)
            );
            Assert.That(
                speech.Timeline.SampleArticulation(.75f).Weight(CharacterFacePose.TH),
                Is.GreaterThan(.6)
            );
        }
    }
}
