using System;
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

        public FaceEmotionSample Sample(
            float seconds,
            EmotionExpressionProfile profile,
            FaceEmotionSample initial = default,
            bool holdLast = false
        )
        {
            if (!float.IsFinite(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (seconds < 0 || seconds >= duration)
                return default;
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (seconds < cue.start)
                    return default;
                if (seconds >= cue.end)
                    continue;
                var target = profile.Sample(cue.emotion, cue.intensity);
                float transition = Mathf.Min(TransitionSeconds, (cue.end - cue.start) * .4f);
                if (seconds < cue.start + transition)
                {
                    var from =
                        i > 0 && Mathf.Abs(cues[i - 1].end - cue.start) < .0001f
                            ? profile.Sample(cues[i - 1].emotion, cues[i - 1].intensity)
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
    }
}
