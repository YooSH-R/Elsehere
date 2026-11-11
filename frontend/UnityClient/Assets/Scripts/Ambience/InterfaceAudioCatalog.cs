using A305.World;
using UnityEngine;

namespace A305.Ambience
{
    public sealed class InterfaceAudioCatalog : ScriptableObject
    {
        public const string ResourcePath = "Ambience/InterfaceAudio";

        [SerializeField]
        private AudioClip[] cues;

        [SerializeField]
        private AudioClip entryMusic;
        public AudioClip Music => entryMusic;

        public AudioClip Cue(UiSound sound)
        {
            int index = (int)(sound == UiSound.PrimaryClick ? UiSound.Click : sound);
            return cues != null && index < cues.Length ? cues[index] : null;
        }

        public void Configure(AudioClip[] effects, AudioClip music)
        {
            cues = effects;
            entryMusic = music;
        }
    }
}
