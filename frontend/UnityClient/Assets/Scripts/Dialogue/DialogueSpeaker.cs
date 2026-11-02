using A305.Characters;
using UnityEngine;

namespace A305.Dialogue
{
    // Scoped to one NPC root; never borrow a nearby NPC's face or voice source.
    public sealed class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField]
        private CharacterSpeechPlayer player;

        [SerializeField, TextArea]
        private string bindingNotice;
        private SpeechExpressionProfile defaultExpressions;

        public string BindingNotice => bindingNotice;
        public CharacterSpeechPlayer Player => Prepare();

        private CharacterSpeechPlayer Prepare()
        {
            if (player != null && !player.transform.IsChildOf(transform))
                return Reject("The assigned speech player belongs to another NPC.");
            if (player != null && !player.gameObject.activeInHierarchy)
                player = null;
            if (player == null)
            {
                var players = GetComponentsInChildren<CharacterSpeechPlayer>();
                if (players.Length > 1)
                    return Reject(
                        "Multiple speech players: assign this NPC's active player explicitly."
                    );
                if (players.Length == 1)
                    player = players[0];
            }

            var face = player != null ? player.Face : null;
            if (face != null && !face.transform.IsChildOf(transform))
                return Reject("The assigned face belongs to another NPC.");
            if (face == null)
            {
                var faces = GetComponentsInChildren<CharacterFaceController>();
                if (faces.Length > 1)
                    return Reject(
                        "Multiple faces: assign this NPC's active speech player explicitly."
                    );
                if (faces.Length == 1)
                    face = faces[0];
            }
            string[] missing = CharacterFaceController.MissingChannels(
                face != null ? face.transform : transform
            );
            if (missing.Length > 0)
                return Reject("Missing face Blend Shapes: " + string.Join(", ", missing));

            if (face == null)
                face = gameObject.AddComponent<CharacterFaceController>();
            if (player == null)
                player = face.gameObject.AddComponent<CharacterSpeechPlayer>();
            if (!player.IsConfigured)
            {
                // A dedicated source avoids overwriting ambient/interaction audio.
                var source = player.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                player.Configure(face, source, player.Speech);
            }
            if (player.Expressions == null)
            {
                if (defaultExpressions == null)
                {
                    defaultExpressions = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
                    defaultExpressions.hideFlags = HideFlags.DontSave;
                }
                player.ConfigureExpressions(defaultExpressions);
            }
            bindingNotice = null;
            return player;
        }

        private CharacterSpeechPlayer Reject(string reason)
        {
            if (
                player != null
                && player.transform.IsChildOf(transform)
                && (player.Face == null || player.Face.transform.IsChildOf(transform))
            )
                player.Stop();
            player = null;
            bindingNotice = reason;
            return null;
        }

        private void OnDestroy()
        {
            if (defaultExpressions != null)
                Destroy(defaultExpressions);
        }
    }
}
