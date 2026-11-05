using System;
using System.Collections.Generic;
using UnityEngine;

namespace A305.Characters
{
    [CreateAssetMenu(menuName = "A305/Characters/Emotion Expression Profile")]
    public sealed class EmotionExpressionProfile : ScriptableObject
    {
        [Serializable]
        private struct Preset
        {
            public CharacterEmotion emotion;
            public FaceEmotionSample face;
        }

        [
            SerializeField,
            Tooltip("좌우는 캐릭터 기준입니다. 발음 중 입꼬리 변형은 재생기가 제한합니다.")
        ]
        private Preset[] presets = CreateDefaults();

        private static Preset[] CreateDefaults() =>
            new Preset[]
            {
                new Preset { emotion = CharacterEmotion.Calm },
                new Preset
                {
                    emotion = CharacterEmotion.WarmSmile,
                    face = new FaceEmotionSample
                    {
                        smile = new Vector2(.55f, .55f),
                        cheekRaise = new Vector2(.25f, .25f),
                        eyeSquint = new Vector2(.15f, .15f),
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Curious,
                    face = new FaceEmotionSample
                    {
                        browOuterUp = new Vector2(.9f, .12f),
                        browDown = new Vector2(0, .2f),
                        eyeWide = new Vector2(.2f, 0),
                        smile = new Vector2(.12f, .12f),
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Confused,
                    face = new FaceEmotionSample
                    {
                        browInnerUp = new Vector2(.8f, .8f),
                        browOuterUp = new Vector2(.3f, .3f),
                        eyeWide = new Vector2(.85f, .85f),
                        mouthFrown = new Vector2(.7f, .7f),
                        restMouthOpen = .3f,
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Frown,
                    face = new FaceEmotionSample
                    {
                        browDown = new Vector2(.85f, .85f),
                        eyeSquint = new Vector2(.25f, .25f),
                        mouthFrown = new Vector2(.65f, .65f),
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Surprised,
                    face = new FaceEmotionSample
                    {
                        browInnerUp = new Vector2(.6f, .6f),
                        browOuterUp = new Vector2(.85f, .85f),
                        eyeWide = new Vector2(.7f, .7f),
                        restMouthOpen = .35f,
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Playful,
                    face = new FaceEmotionSample
                    {
                        smile = new Vector2(1, .15f),
                        browOuterUp = new Vector2(.7f, 0),
                        eyeClosure = new Vector2(0, 1),
                        cheekRaise = new Vector2(.3f, .1f),
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Laugh,
                    face = new FaceEmotionSample
                    {
                        smile = new Vector2(.9f, .9f),
                        eyeSquint = new Vector2(.95f, .95f),
                        cheekRaise = new Vector2(.8f, .8f),
                        browOuterUp = new Vector2(.15f, .15f),
                        restMouthOpen = .55f,
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Concerned,
                    face = new FaceEmotionSample
                    {
                        browInnerUp = new Vector2(.85f, .85f),
                        mouthFrown = new Vector2(.3f, .3f),
                        eyeSquint = new Vector2(.08f, .08f),
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Sad,
                    face = new FaceEmotionSample
                    {
                        browInnerUp = Vector2.one,
                        eyeClosure = new Vector2(.42f, .42f),
                        mouthFrown = Vector2.one,
                        restMouthOpen = .05f,
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.BigLaugh,
                    face = new FaceEmotionSample
                    {
                        smile = Vector2.one,
                        eyeSquint = Vector2.one,
                        cheekRaise = new Vector2(.6f, .6f),
                        browOuterUp = new Vector2(.4f, .4f),
                        restMouthOpen = 1,
                        restLaughOpen = 1,
                    },
                },
                new Preset
                {
                    emotion = CharacterEmotion.Indifferent,
                    face = new FaceEmotionSample
                    {
                        eyeClosure = new Vector2(.55f, .55f),
                        browDown = new Vector2(.08f, .08f),
                        mouthFrown = new Vector2(.25f, .25f),
                    },
                },
            };

        public void AddMissingDefaults()
        {
            var merged = new List<Preset>(presets);
            foreach (var preset in CreateDefaults())
                if (!merged.Exists(existing => existing.emotion == preset.emotion))
                    merged.Add(preset);
            presets = merged.ToArray();
        }

        public FaceEmotionSample Sample(CharacterEmotion emotion, float intensity = 1)
        {
            if (!float.IsFinite(intensity) || intensity < 0 || intensity > 1)
                throw new ArgumentOutOfRangeException(nameof(intensity));
            foreach (var preset in presets)
                if (preset.emotion == emotion)
                    return FaceEmotionSample.Lerp(default, preset.face, intensity);
            throw new ArgumentOutOfRangeException(nameof(emotion));
        }
    }
}
