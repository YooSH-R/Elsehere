using UnityEngine;

namespace A305.Dialogue
{
    [CreateAssetMenu(menuName = "A305/Dialogue/Recorded Performance")]
    public sealed class RecordedPerformanceDefinition : ScriptableObject
    {
        public TextAsset Voice;

        [TextArea]
        public string English;

        [TextArea]
        public string Korean;

        public TextAsset GuideVoice;

        [TextArea]
        public string GuideEnglish;

        [TextArea]
        public string GuideKorean;

        public DialogueLine CreateGuideLine() =>
            DialogueLine.FromNpc(GuideEnglish, GuideKorean, speech: GuideVoice.bytes);

        [Min(1)]
        public float BeatsPerMinute = 110;

        [Min(1)]
        public float BeatsPerAnimationLoop = 4;
        public string AnimationState = "Rhythm";

        [Tooltip("저장 음성과 같은 초 단위 시간으로 평가하는 하반신 공연 클립")]
        public AnimationClip LowerBodyMotion;

        public DialogueLine CreateLine() =>
            DialogueLine.FromNpc(English, Korean, speech: Voice.bytes);
    }
}
