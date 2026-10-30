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
        public string TimingError { get; private set; }

        [Serializable]
        private sealed class Identity
        {
            public string pcmSha256 = "";
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
            return result;
        }

        private static string Tag(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
