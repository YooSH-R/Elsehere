using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    // Targets are placed inside real phoneme intervals. Interpolation across
    // neighboring targets gives anticipation and carry-over without word rules.
    internal sealed class SpeechPhonemeArticulation
    {
        private readonly List<(float Time, SpeechMouthSample Mouth)> targets = new();
        private readonly float duration;

        public SpeechPhonemeArticulation(SpeechPhonemeCue[] cues, float clipDuration)
        {
            if (cues == null || cues.Length == 0)
                throw new ArgumentException("Phoneme events are required.");
            duration = clipDuration;
            float previousEnd = 0;
            targets.Add((0, SpeechMouthSample.Rest));
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (
                    cue == null
                    || !float.IsFinite(cue.start)
                    || !float.IsFinite(cue.end)
                    || cue.start < previousEnd
                    || cue.end <= cue.start
                    || cue.end > duration
                )
                    throw new ArgumentException(
                        "Phoneme intervals must be ordered and inside the clip."
                    );
                var initial = Target(
                    cue.phoneme,
                    false,
                    i + 1 < cues.Length ? cues[i + 1].phoneme : "_"
                );
                var final = Target(
                    cue.phoneme,
                    true,
                    i + 1 < cues.Length ? cues[i + 1].phoneme : "_"
                );
                float length = cue.end - cue.start;
                if (cue.start > previousEnd)
                {
                    targets.Add((previousEnd, SpeechMouthSample.Rest));
                    targets.Add((cue.start, SpeechMouthSample.Rest));
                }
                if (cue.phoneme is "ay" or "ey" or "aw" or "oy" or "ow")
                {
                    targets.Add((cue.start + length * .22f, initial));
                    targets.Add((cue.start + length * .85f, final));
                }
                else if (cue.phoneme is "p" or "b" or "m" or "_")
                {
                    float edge = Mathf.Min(.04f, length * .2f);
                    targets.Add((cue.start + edge, initial));
                    targets.Add((cue.end - edge, final));
                }
                else
                    targets.Add((cue.start + length * .5f, initial));
                previousEnd = cue.end;
            }
            targets.Add((previousEnd, SpeechMouthSample.Rest));
        }

        private static SpeechMouthSample Target(string phone, bool final, string next)
        {
            if (phone == "h")
                return next == "h" ? SpeechMouthSample.Rest : Target(next, false, "_");
            return phone switch
            {
                "_" => SpeechMouthSample.Rest,
                "ay" => SpeechMouthSample.Pose(
                    final ? CharacterFacePose.WideI : CharacterFacePose.OpenMouth,
                    final ? .4f : .8f
                ),
                "ey" => SpeechMouthSample.Pose(
                    final ? CharacterFacePose.WideI : CharacterFacePose.E,
                    final ? .32f : .65f
                ),
                "aw" => SpeechMouthSample.Pose(
                    final ? CharacterFacePose.OU : CharacterFacePose.OpenMouth,
                    .7f
                ),
                "oy" => SpeechMouthSample.Pose(
                    final ? CharacterFacePose.WideI : CharacterFacePose.OH,
                    final ? .35f : .7f
                ),
                "ow" => SpeechMouthSample.Pose(
                    final ? CharacterFacePose.OU : CharacterFacePose.OH,
                    .7f
                ),
                "y" => SpeechMouthSample.Pose(
                    next is "uw" or "uh" or "ow" ? CharacterFacePose.OU : CharacterFacePose.FlatEu,
                    .4f
                ),
                "uw" or "w" => SpeechMouthSample.Pose(CharacterFacePose.OU, .8f),
                "iy" => SpeechMouthSample.Pose(CharacterFacePose.WideI, .55f),
                "ih" or "ix" => SpeechMouthSample.Pose(CharacterFacePose.WideI, .3f),
                "ae" or "aa" or "ah" => SpeechMouthSample.Pose(CharacterFacePose.OpenMouth, .72f),
                "ax" => SpeechMouthSample.Pose(CharacterFacePose.OpenMouth, .35f),
                "eh" => SpeechMouthSample.Pose(CharacterFacePose.E, .6f),
                "ao" => SpeechMouthSample.Pose(CharacterFacePose.OH, .65f),
                "uh" => SpeechMouthSample.Pose(CharacterFacePose.OU, .45f),
                "p" or "b" or "m" => SpeechMouthSample.Pose(CharacterFacePose.ClosedLips),
                "f" or "v" => SpeechMouthSample.Pose(CharacterFacePose.FV, .85f),
                "th" or "dh" => SpeechMouthSample.Pose(CharacterFacePose.TH, .7f),
                "s" or "z" => SpeechMouthSample.Pose(CharacterFacePose.WideI, .25f),
                "sh" or "ch" or "jh" or "zh" => SpeechMouthSample.Pose(CharacterFacePose.OU, .4f),
                "l" or "n" or "d" or "t" or "dx" or "k" or "g" or "ng" or "r" or "er" =>
                    SpeechMouthSample.Pose(CharacterFacePose.FlatEu, .22f),
                _ => throw new ArgumentException("Unsupported SAPI phoneme: " + phone),
            };
        }

        public SpeechMouthSample Sample(float seconds)
        {
            if (!float.IsFinite(seconds))
                throw new ArgumentException("Audio time must be finite.");
            if (seconds <= 0 || seconds >= duration)
                return SpeechMouthSample.Rest;
            for (int i = 1; i < targets.Count; i++)
            {
                if (seconds > targets[i].Time)
                    continue;
                float progress = Mathf.InverseLerp(targets[i - 1].Time, targets[i].Time, seconds);
                return SpeechMouthSample.Blend(
                    targets[i - 1].Mouth,
                    targets[i].Mouth,
                    Mathf.SmoothStep(0, 1, progress)
                );
            }
            return SpeechMouthSample.Rest;
        }
    }
}
