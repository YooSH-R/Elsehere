using System;
using UnityEngine;

namespace A305.Characters
{
    [Serializable]
    public sealed class SpeechVisemeCue
    {
        public float start;
        public float end;
        public int viseme;
    }

    [Serializable]
    public sealed class SpeechPhonemeCue
    {
        public float start;
        public float end;
        public string phoneme;
    }

    [Serializable]
    public sealed class SpeechVisemeData
    {
        public int version;
        public string provider;
        public float duration;
        public SpeechVisemeCue[] cues;
        public SpeechPhonemeCue[] phonemes;
    }

    public readonly struct SpeechPoseSample
    {
        public readonly CharacterFacePose From;
        public readonly CharacterFacePose To;
        public readonly float Blend;

        public SpeechPoseSample(CharacterFacePose from, CharacterFacePose to, float blend)
        {
            From = from;
            To = to;
            Blend = blend;
        }
    }

    public sealed class SpeechVisemeTimeline
    {
        private readonly SpeechVisemeData data;
        public float Duration => data.duration;
        private readonly SpeechPhonemeArticulation articulation;
        private const float TransitionSeconds = .035f;
        private static readonly SpeechPoseSample Rest = new SpeechPoseSample(
            CharacterFacePose.Neutral,
            CharacterFacePose.Neutral,
            1
        );

        private SpeechVisemeTimeline(
            SpeechVisemeData source,
            SpeechPhonemeArticulation phonemes = null
        )
        {
            data = source;
            articulation = phonemes;
        }

        public static SpeechVisemeTimeline Parse(string json, float clipDuration)
        {
            if (
                string.IsNullOrWhiteSpace(json)
                || !float.IsFinite(clipDuration)
                || clipDuration <= 0
            )
                throw new ArgumentException("Speech requires audio and its viseme timeline.");
            var source = JsonUtility.FromJson<SpeechVisemeData>(json);
            if (
                source == null
                || (source.version != 1 && source.version != 2)
                || (
                    source.provider != "windows-sapi-en-US"
                    && !(source.version == 2 && source.provider == "kokoro-en-US")
                )
                || !float.IsFinite(source.duration)
                || Mathf.Abs(source.duration - clipDuration) > .02f
                || (source.version == 1 && (source.cues == null || source.cues.Length == 0))
            )
                throw new ArgumentException("Unsupported or mismatched speech timeline.");
            if (source.version == 2)
                return new SpeechVisemeTimeline(
                    source,
                    new SpeechPhonemeArticulation(source.phonemes, source.duration)
                );
            float previousEnd = 0;
            foreach (var cue in source.cues)
            {
                if (
                    cue == null
                    || !float.IsFinite(cue.start)
                    || !float.IsFinite(cue.end)
                    || cue.start < previousEnd
                    || cue.end <= cue.start
                    || cue.end > source.duration
                    || cue.viseme < 0
                    || cue.viseme > 21
                )
                    throw new ArgumentException(
                        "Speech cues must be ordered, finite, and inside the audio clip."
                    );
                previousEnd = cue.end;
            }
            return new SpeechVisemeTimeline(source);
        }

        // SAPI en-US IDs. Consonants without a dedicated authored shape use the
        // closest available mouth; these are not a full tongue/dental facial rig.
        public static CharacterFacePose MapSapiViseme(int viseme) =>
            viseme switch
            {
                0 => CharacterFacePose.Neutral,
                1 or 2 or 9 or 11 or 12 => CharacterFacePose.OpenMouth,
                3 or 8 or 10 => CharacterFacePose.OH,
                4 => CharacterFacePose.E,
                5 or 13 or 14 or 19 or 20 => CharacterFacePose.FlatEu,
                6 or 15 => CharacterFacePose.WideI,
                7 or 16 => CharacterFacePose.OU,
                17 => CharacterFacePose.TH,
                18 => CharacterFacePose.FV,
                21 => CharacterFacePose.ClosedLips,
                _ => throw new ArgumentOutOfRangeException(nameof(viseme)),
            };

        public SpeechMouthSample SampleArticulation(float seconds)
        {
            if (articulation != null)
                return articulation.Sample(seconds);
            var sample = Sample(seconds);
            return SpeechMouthSample.Blend(
                SpeechMouthSample.Pose(sample.From),
                SpeechMouthSample.Pose(sample.To),
                sample.Blend
            );
        }

        public SpeechPoseSample Sample(float seconds)
        {
            if (articulation != null)
                throw new InvalidOperationException("Use SampleArticulation for phoneme speech.");
            if (!float.IsFinite(seconds))
                throw new ArgumentException("Audio time must be finite.");
            if (seconds < 0 || seconds >= data.duration)
                return Rest;
            for (int i = 0; i < data.cues.Length; i++)
            {
                var cue = data.cues[i];
                if (seconds < cue.start)
                    return Rest;
                if (seconds >= cue.end)
                    continue;
                var pose = MapSapiViseme(cue.viseme);
                float transition = Mathf.Min(TransitionSeconds, (cue.end - cue.start) * .25f);
                if (seconds < cue.start + transition)
                {
                    var previous =
                        i > 0 && Mathf.Abs(data.cues[i - 1].end - cue.start) < .0001f
                            ? MapSapiViseme(data.cues[i - 1].viseme)
                            : CharacterFacePose.Neutral;
                    return new SpeechPoseSample(
                        previous,
                        pose,
                        Mathf.SmoothStep(0, 1, (seconds - cue.start) / transition)
                    );
                }
                bool followedByGap = i + 1 == data.cues.Length || data.cues[i + 1].start > cue.end;
                if (followedByGap && seconds > cue.end - transition)
                    return new SpeechPoseSample(
                        pose,
                        CharacterFacePose.Neutral,
                        Mathf.SmoothStep(0, 1, (seconds - cue.end + transition) / transition)
                    );
                return new SpeechPoseSample(pose, pose, 1);
            }
            return Rest;
        }
    }
}
