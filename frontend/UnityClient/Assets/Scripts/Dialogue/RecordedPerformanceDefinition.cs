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

        [Min(1)]
        public float BeatsPerMinute = 110;

        [Min(1)]
        public float BeatsPerAnimationLoop = 4;
        public string AnimationState = "Rhythm";

        public DialogueLine CreateLine() =>
            DialogueLine.FromNpc(English, Korean, speech: Voice.bytes);
    }
}
