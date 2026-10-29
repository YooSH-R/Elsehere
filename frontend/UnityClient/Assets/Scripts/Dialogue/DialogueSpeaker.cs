using A305.Characters;
using UnityEngine;

namespace A305.Dialogue
{
    // Place on the NPC interaction root, alongside its look target. No global
    // NPC search: the conversation's selected target determines the face.
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField]
        private CharacterSpeechPlayer player;
        public CharacterSpeechPlayer Player => player;
    }
}
