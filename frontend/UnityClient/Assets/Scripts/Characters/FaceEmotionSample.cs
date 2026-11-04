using System;
using UnityEngine;

namespace A305.Characters
{
    public enum CharacterEmotion
    {
        Calm,
        WarmSmile,
        Curious,
        Confused,
        Frown,
        Surprised,
        Playful,
        Laugh,
        Concerned,
    }

    [Serializable]
    public struct FaceEmotionSample
    {
        public Vector2 smile;
        public Vector2 browInnerUp;
        public Vector2 browOuterUp;
        public Vector2 browDown;
        public Vector2 eyeSquint;
        public Vector2 eyeWide;
        public Vector2 cheekRaise;
        public Vector2 mouthFrown;
        public float restMouthOpen;

        public FaceEmotionSample(float browDown)
            : this()
        {
            this.browDown = Vector2.one * browDown;
        }

        public static FaceEmotionSample Lerp(
            FaceEmotionSample from,
            FaceEmotionSample to,
            float progress
        ) =>
            new FaceEmotionSample
            {
                smile = Vector2.Lerp(from.smile, to.smile, progress),
                browInnerUp = Vector2.Lerp(from.browInnerUp, to.browInnerUp, progress),
                browOuterUp = Vector2.Lerp(from.browOuterUp, to.browOuterUp, progress),
                browDown = Vector2.Lerp(from.browDown, to.browDown, progress),
                eyeSquint = Vector2.Lerp(from.eyeSquint, to.eyeSquint, progress),
                eyeWide = Vector2.Lerp(from.eyeWide, to.eyeWide, progress),
                cheekRaise = Vector2.Lerp(from.cheekRaise, to.cheekRaise, progress),
                mouthFrown = Vector2.Lerp(from.mouthFrown, to.mouthFrown, progress),
                restMouthOpen = Mathf.Lerp(from.restMouthOpen, to.restMouthOpen, progress),
            };
    }
}
