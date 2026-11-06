using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace A305.Characters
{
    // The server preserves WAV bytes, including the a305 phoneme chunk. Decode
    // PCM here so WebGL and the Editor use the same samples and playback clock.
    public sealed class DialogueSpeechWav
    {
        public float[] Samples { get; private set; }
        public int Frequency { get; private set; }
        public int Channels { get; private set; }
        public SpeechVisemeTimeline Timeline { get; private set; }
        public SpeechEmotionTrack Emotions { get; private set; }
        public string EmotionError { get; private set; }
        public string TimingError { get; private set; }

        [Serializable]
        private sealed class Identity
        {
            public string pcmSha256 = "";
        }

        [Serializable]
        private sealed class EmotionPayload
        {
            public int version;
            public float duration;
            public string pcmSha256;
            public EmotionCue[] cues;
        }

        [Serializable]
        private sealed class EmotionCue
        {
            public float start;
            public float end;
            public string emotion;
            public float intensity;
        }

        private static SpeechEmotionTrack ReadEmotions(string json, byte[] pcm, float duration)
        {
            var payload = JsonUtility.FromJson<EmotionPayload>(json);
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(pcm)).Replace("-", "");
            if (
                payload == null
                || payload.version != 1
                || !float.IsFinite(payload.duration)
                || Mathf.Abs(payload.duration - duration) > .02f
                || !string.Equals(actual, payload.pcmSha256, StringComparison.OrdinalIgnoreCase)
                || payload.cues == null
                || payload.cues.Length > 256
            )
                throw new ArgumentException("표정 시간표가 이 음성과 일치하지 않습니다.");
            var cues = new SpeechEmotionCue[payload.cues.Length];
            for (int index = 0; index < cues.Length; index++)
            {
                var cue = payload.cues[index];
                if (
                    cue == null
                    || string.IsNullOrEmpty(cue.emotion)
                    || !Enum.TryParse(
                        cue.emotion.Replace("_", ""),
                        true,
                        out CharacterEmotion emotion
                    )
                    || !Enum.IsDefined(typeof(CharacterEmotion), emotion)
                    || char.IsDigit(cue.emotion[0])
                )
                    throw new ArgumentException("지원하지 않는 표정 종류입니다.");
                cues[index] = new SpeechEmotionCue
                {
                    start = cue.start,
                    end = cue.end,
                    emotion = emotion,
                    intensity = cue.intensity,
                };
            }
            return new SpeechEmotionTrack(cues, duration);
        }

        public static DialogueSpeechWav Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 32 * 1024 * 1024)
                throw new ArgumentException("Missing or oversized NPC WAV.");
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (
                Tag(reader) != "RIFF"
                || reader.ReadUInt32() != bytes.Length - 8
                || Tag(reader) != "WAVE"
            )
                throw new ArgumentException("Invalid RIFF WAV header.");
            int channels = 0,
                frequency = 0;
            byte[] pcm = null;
            string json = null;
            string emotionJson = null;
            int emotionChunks = 0;
            while (reader.BaseStream.Position < bytes.Length)
            {
                if (reader.BaseStream.Position + 8 > bytes.Length)
                    throw new ArgumentException("Truncated WAV header.");
                string tag = Tag(reader);
                uint size = reader.ReadUInt32();
                long end = reader.BaseStream.Position + size;
                if (end + (size % 2) > bytes.Length)
                    throw new ArgumentException("Truncated WAV data.");
                switch (tag)
                {
                    case "fmt ":
                        if (channels != 0 || size < 16 || reader.ReadUInt16() != 1)
                            throw new ArgumentException("NPC speech must be PCM16 WAV.");
                        channels = reader.ReadUInt16();
                        frequency = reader.ReadInt32();
                        int byteRate = reader.ReadInt32();
                        int alignment = reader.ReadUInt16();
                        if (
                            (channels != 1 && channels != 2)
                            || frequency < 8000
                            || frequency > 96000
                            || alignment != channels * 2
                            || byteRate != frequency * alignment
                            || reader.ReadUInt16() != 16
                        )
                            throw new ArgumentException("Unsupported NPC PCM format.");
                        break;
                    case "data":
                        if (pcm != null)
                            throw new ArgumentException("Duplicate PCM chunk.");
                        pcm = reader.ReadBytes((int)size);
                        break;
                    case "a305":
                        if (json != null || size > 512 * 1024)
                            throw new ArgumentException("Invalid phoneme chunk.");
                        json = Encoding.UTF8.GetString(reader.ReadBytes((int)size));
                        break;
                    case "e305":
                        emotionChunks++;
                        if (emotionChunks == 1 && size <= 512 * 1024)
                            emotionJson = Encoding.UTF8.GetString(reader.ReadBytes((int)size));
                        break;
                }
                reader.BaseStream.Position = end + size % 2;
            }
            if (channels == 0 || pcm == null || pcm.Length == 0 || pcm.Length % (channels * 2) != 0)
                throw new ArgumentException("WAV has no complete PCM frames.");
            var result = new DialogueSpeechWav
            {
                Samples = new float[pcm.Length / 2],
                Frequency = frequency,
                Channels = channels,
                TimingError =
                    "This voice has no phoneme timing. Use a Kokoro model with duration output.",
            };
            for (int i = 0; i < result.Samples.Length; i++)
                result.Samples[i] = (short)(pcm[i * 2] | pcm[i * 2 + 1] << 8) / 32768f;
            if (json != null)
            {
                try
                {
                    var identity = JsonUtility.FromJson<Identity>(json);
                    using var sha = SHA256.Create();
                    string actual = BitConverter.ToString(sha.ComputeHash(pcm)).Replace("-", "");
                    if (
                        identity == null
                        || !string.Equals(
                            actual,
                            identity.pcmSha256,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        throw new ArgumentException("Phonemes do not match this voice.");
                    result.Timeline = SpeechVisemeTimeline.Parse(
                        json,
                        (float)result.Samples.Length / channels / frequency
                    );
                    result.TimingError = null;
                }
                catch (ArgumentException error)
                {
                    // Legacy or damaged timing must not disable the real voice.
                    result.TimingError = error.Message;
                }
            }
            if (emotionChunks > 0)
            {
                try
                {
                    if (emotionChunks != 1 || emotionJson == null)
                        throw new ArgumentException("표정 시간표가 중복되었거나 너무 큽니다.");
                    result.Emotions = ReadEmotions(
                        emotionJson,
                        pcm,
                        (float)result.Samples.Length / channels / frequency
                    );
                }
                catch (ArgumentException error)
                {
                    result.EmotionError = error.Message;
                }
            }
            return result;
        }

        private static string Tag(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
