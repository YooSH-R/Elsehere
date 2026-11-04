using UnityEngine;

namespace A305.Characters
{
    [CreateAssetMenu(menuName = "A305/Characters/Speech Clip")]
    public sealed class CharacterSpeechClip : ScriptableObject
    {
        [SerializeField]
        private AudioClip audioClip;

        [SerializeField]
        private TextAsset visemeTimeline;
        public AudioClip Audio => audioClip;
        public TextAsset Timeline => visemeTimeline;

        public void Configure(AudioClip audio, TextAsset timeline)
        {
            audioClip = audio;
            visemeTimeline = timeline;
        }
    }
}
