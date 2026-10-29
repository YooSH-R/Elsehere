using UnityEngine;

namespace A305.Characters
{
    public readonly struct SpeechMouthSample
    {
        private readonly float[] weights;
        public static SpeechMouthSample Rest => new SpeechMouthSample(null);

        private SpeechMouthSample(float[] values) => weights = values;

        public float Weight(CharacterFacePose pose) => weights == null ? 0 : weights[(int)pose];

        public static SpeechMouthSample Pose(CharacterFacePose pose, float strength = 1)
        {
            var values = new float[(int)CharacterFacePose.FlatEu + 1];
            if (pose != CharacterFacePose.Neutral)
                values[(int)pose] = Mathf.Clamp01(strength);
            return new SpeechMouthSample(values);
        }

        public static SpeechMouthSample Blend(
            SpeechMouthSample first,
            SpeechMouthSample second,
            float progress
        )
        {
            var values = new float[(int)CharacterFacePose.FlatEu + 1];
            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Lerp(
                    first.Weight((CharacterFacePose)i),
                    second.Weight((CharacterFacePose)i),
                    progress
                );
            return new SpeechMouthSample(values);
        }
    }
}
