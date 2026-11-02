using A305.Characters;
using UnityEngine;

namespace A305.Dialogue
{
    // Offline review only; this component never creates a conversation or calls TTS.
    public sealed class CafeDialogueSpeechPreview : MonoBehaviour
    {
        [SerializeField]
        private DialogueServerLink link;

        [SerializeField]
        private CharacterSpeechPlayer speaker;

        [SerializeField]
        private TextAsset coffeeChoice;

        [SerializeField]
        private TextAsset cafeThanks;

        public void PlayCoffeeChoice() => Play(coffeeChoice);

        public void PlayThanks() => Play(cafeThanks);

        public void Stop() => link.StopSpeech();

        private void Play(TextAsset voice)
        {
            link.SetSpeaker(speaker);
            link.PlayRecordedSpeech(voice.bytes);
        }
    }
}
