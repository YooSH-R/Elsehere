using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    [Serializable]
    public sealed class SpeechEmotionCue
    {
        public float start;
        public float end;
        public CharacterEmotion emotion;
        public float intensity = 1;
    }

    public sealed class SpeechEmotionTrack
    {
        private readonly SpeechEmotionCue[] cues;
        private readonly float duration;
        public float Duration => duration;
        private const float TransitionSeconds = .24f;

        public SpeechEmotionTrack(SpeechEmotionCue[] cues, float duration)
        {
            if (!float.IsFinite(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            this.duration = duration;
            this.cues = new SpeechEmotionCue[cues?.Length ?? 0];
            float previousEnd = 0;
            for (int i = 0; i < this.cues.Length; i++)
            {
                var cue = cues[i];
                if (
                    cue == null
                    || !float.IsFinite(cue.start)
                    || !float.IsFinite(cue.end)
                    || cue.start < previousEnd
                    || cue.end <= cue.start
                    || cue.end > duration
                    || !float.IsFinite(cue.intensity)
                    || cue.intensity < 0
                    || cue.intensity > 1
                    || !Enum.IsDefined(typeof(CharacterEmotion), cue.emotion)
                )
                    throw new ArgumentException(
                        "표정 구간은 음성 안에서 겹치지 않고 시간순이어야 합니다."
                    );
                this.cues[i] = new SpeechEmotionCue
                {
                    start = cue.start,
                    end = cue.end,
                    emotion = cue.emotion,
                    intensity = cue.intensity,
                };
                previousEnd = cue.end;
            }
        }

        // 실제 대화는 감정 종류와 시각을 유지하면서 완성된 표정을 사용한다.
        // 원본 시간표는 검수용 강도 조절과 음성 메타데이터 검증에 그대로 둔다.
        public SpeechEmotionTrack WithFullStrength()
        {
            var completed = new SpeechEmotionCue[cues.Length];
            for (int i = 0; i < cues.Length; i++)
                completed[i] = new SpeechEmotionCue
                {
                    start = cues[i].start,
                    end = cues[i].end,
                    emotion = cues[i].emotion,
                    intensity = 1,
                };
            return new SpeechEmotionTrack(completed, duration);
        }

        // 녹음으로 음성을 끊으면 아직 말하지 않은 구절의 감정으로 넘어가지 않는다.
        internal SpeechEmotionTrack Truncate(float endSeconds)
        {
            var played = new List<SpeechEmotionCue>();
            foreach (var cue in cues)
            {
                if (cue.start >= endSeconds)
                    break;
                played.Add(
                    new SpeechEmotionCue
                    {
                        start = cue.start,
                        end = Mathf.Min(cue.end, endSeconds),
                        emotion = cue.emotion,
                        intensity = cue.intensity,
                    }
                );
            }
            return new SpeechEmotionTrack(played.ToArray(), endSeconds);
        }

        public FaceEmotionSample Sample(
            float seconds,
            EmotionExpressionProfile profile,
            FaceEmotionSample initial = default,
            bool holdLast = false,
            float initialEmotionSeconds = 0
        )
        {
            if (!float.IsFinite(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!float.IsFinite(initialEmotionSeconds) || initialEmotionSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(initialEmotionSeconds));
            if (seconds < 0)
                return default;
            if (seconds >= duration)
                return holdLast && cues.Length > 0 && cues[cues.Length - 1].end == duration
                    ? SampleCue(cues.Length - 1, seconds, profile, initialEmotionSeconds)
                    : default;
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (seconds < cue.start)
                    return default;
                if (seconds >= cue.end)
                    continue;
                var target = SampleCue(i, seconds, profile, initialEmotionSeconds);
                float transition = Mathf.Min(TransitionSeconds, (cue.end - cue.start) * .4f);
                if (seconds < cue.start + transition)
                {
                    var from =
                        i > 0 && Mathf.Abs(cues[i - 1].end - cue.start) < .0001f
                            ? SampleCue(i - 1, cue.start, profile, initialEmotionSeconds)
                        : i == 0 && cue.start == 0 ? initial
                        : default;
                    return FaceEmotionSample.Lerp(
                        from,
                        target,
                        Mathf.SmoothStep(0, 1, (seconds - cue.start) / transition)
                    );
                }
                bool hasGap =
                    i + 1 == cues.Length
                        ? !holdLast || cue.end < duration
                        : cues[i + 1].start > cue.end;
                if (hasGap && seconds > cue.end - transition)
                    return FaceEmotionSample.Lerp(
                        target,
                        default,
                        Mathf.SmoothStep(0, 1, (seconds - cue.end + transition) / transition)
                    );
                return target;
            }
            return default;
        }

        internal float ContinuationSeconds(SpeechEmotionTrack previous, float previousOffset)
        {
            if (
                previous == null
                || cues.Length == 0
                || previous.cues.Length == 0
                || cues[0].start != 0
            )
                return 0;
            int last = previous.cues.Length - 1;
            if (
                previous.cues[last].end != previous.duration
                || previous.cues[last].emotion != cues[0].emotion
            )
                return 0;
            float start = previous.ContinuousStart(last);
            return previous.duration - start + (start == 0 ? previousOffset : 0);
        }

        private FaceEmotionSample SampleCue(
            int index,
            float seconds,
            EmotionExpressionProfile profile,
            float initialEmotionSeconds
        )
        {
            var cue = cues[index];
            var face = profile.Sample(cue.emotion, cue.intensity);
            if (cue.emotion != CharacterEmotion.Playful)
                return face;
            float start = ContinuousStart(index);
            float continued = start == 0 ? initialEmotionSeconds : 0;
            // 얼굴 표정은 유지해도 윙크는 대사 끝과 음성 조각 경계를 지나 다시 눈을 뜬다.
            face.eyeClosure *= WinkClosure(seconds - start + continued - TransitionSeconds);
            return face;
        }

        private float ContinuousStart(int index)
        {
            int first = index;
            while (
                first > 0
                && cues[first - 1].emotion == cues[index].emotion
                && Mathf.Abs(cues[first - 1].end - cues[first].start) < .0001f
            )
                first--;
            return cues[first].start;
        }

        private static float WinkClosure(float seconds)
        {
            if (seconds < 0)
                return 0;
            if (seconds < .1f)
                return Mathf.SmoothStep(0, 1, seconds / .1f);
            if (seconds < .16f)
                return 1;
            if (seconds < .34f)
                return 1 - Mathf.SmoothStep(0, 1, (seconds - .16f) / .18f);
            return 0;
        }
    }
}
