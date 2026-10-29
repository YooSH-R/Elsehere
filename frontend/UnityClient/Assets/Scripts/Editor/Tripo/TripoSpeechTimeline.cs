using System;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    [Serializable]
    public sealed class TripoSpeechCue
    {
        public float start;
        public float end;
        public string viseme;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class TripoSpeechTimeline
    {
        public TripoSpeechCue[] cues = Array.Empty<TripoSpeechCue>();

        public void Validate(float duration)
        {
            if (!Finite(duration) || duration <= 0f || cues == null || cues.Length == 0)
                throw new InvalidOperationException("재생 길이와 발음 타임라인이 필요합니다.");
            float previousEnd = 0f;
            foreach (var cue in cues)
            {
                if (
                    cue == null
                    || !Finite(cue.start)
                    || !Finite(cue.end)
                    || !Finite(cue.weight)
                    || cue.start < previousEnd
                    || cue.end <= cue.start
                    || cue.end > duration
                    || cue.weight < 0f
                    || cue.weight > 1f
                    || Array.IndexOf(TripoFacePreview.Visemes, cue.viseme) < 0
                )
                    throw new InvalidOperationException(
                        "발음 구간의 순서·겹침·길이·이름·강도를 확인하세요."
                    );
                previousEnd = cue.end;
            }
        }

        public TripoSpeechCue Sample(float seconds)
        {
            foreach (var cue in cues)
                if (seconds >= cue.start && seconds < cue.end)
                    return cue;
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
