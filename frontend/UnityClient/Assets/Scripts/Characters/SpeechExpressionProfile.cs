using System;
using UnityEngine;

namespace A305.Characters
{
    public readonly struct SpeechExpressionSample
    {
        public readonly float Smile;
        public readonly float Blink;

        public SpeechExpressionSample(float smile, float blink)
        {
            Smile = smile;
            Blink = blink;
        }
    }

    [CreateAssetMenu(menuName = "A305/Characters/Speech Expression Profile")]
    public sealed class SpeechExpressionProfile : ScriptableObject
    {
        [SerializeField, Range(0, .5f)]
        private float smileStrength = .32f;

        [SerializeField, Min(0)]
        private float firstBlinkSeconds = 1.25f;

        [SerializeField, Min(1)]
        private float blinkIntervalSeconds = 3.4f;

        public SpeechExpressionSample Sample(float seconds, float clipDuration)
        {
            if (!float.IsFinite(seconds) || !float.IsFinite(clipDuration))
                throw new ArgumentException("Expression time must be finite.");
            if (seconds <= 0 || seconds >= clipDuration)
                return new SpeechExpressionSample(0, 0);
            float envelope =
                Mathf.SmoothStep(0, 1, seconds / .25f)
                * Mathf.SmoothStep(0, 1, (clipDuration - seconds) / .25f);
            float blink = 0;
            float start = Mathf.Max(0, firstBlinkSeconds);
            int cycle = 0;
            while (start <= seconds)
            {
                float phase = seconds - start;
                if (phase < .07f)
                    blink = Mathf.SmoothStep(0, 1, phase / .07f);
                else if (phase < .10f)
                    blink = 1;
                else if (phase < .23f)
                    blink = 1 - Mathf.SmoothStep(0, 1, (phase - .10f) / .13f);
                start += Mathf.Max(1, blinkIntervalSeconds) + (cycle++ % 2 == 0 ? .25f : -.15f);
            }
            return new SpeechExpressionSample(Mathf.Clamp(smileStrength, 0, .5f) * envelope, blink);
        }
    }
}
