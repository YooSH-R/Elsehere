using UnityEngine;

namespace A305.Characters
{
    public sealed class CharacterSpeechPreviewPanel : MonoBehaviour
    {
        [SerializeField]
        private CharacterSpeechPlayer player;

        public void Configure(CharacterSpeechPlayer target) => player = target;

        private void OnGUI()
        {
            if (player == null || player.Speech == null)
                return;
            GUILayout.BeginArea(
                new Rect(12, 12, Mathf.Min(360, Screen.width - 24), 190),
                GUI.skin.box
            );
            GUILayout.Label("Cafe Female - Offline TTS Lip Sync");
            GUILayout.Label(player.Speech.name);
            GUILayout.Label(
                player.IsPaused ? "Paused"
                : player.IsPlaying ? "Speaking"
                : "Ready"
            );
            GUILayout.Label($"Audio: {player.PlaybackTime:F2} / {player.Speech.Audio.length:F2} s");
            if (GUILayout.Button("Play / Replay"))
                player.Play();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(player.IsPaused ? "Resume" : "Pause"))
            {
                if (player.IsPaused)
                    player.Resume();
                else
                    player.Pause();
            }
            if (GUILayout.Button("Stop"))
                player.Stop();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
