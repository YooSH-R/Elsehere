using System;
using UnityEngine;

namespace A305.Characters
{
    // Sandbox-only selector. All characters use the same controls, voice and timeline.
    public sealed class NpcEmotionRoster : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string label;
            public CharacterSpeechPlayer player;
            public GameObject root;
            public Vector3 center,
                front;
            public float cameraSize;
        }

        [SerializeField]
        private Entry[] characters;

        [SerializeField]
        private EmotionPreviewPanel panel;

        [SerializeField]
        private EmotionExpressionProfile profile;

        [SerializeField]
        private CharacterSpeechClip[] clips;

        [SerializeField]
        private Camera previewCamera;
        private Vector2 scroll;

        public void Configure(
            Entry[] entries,
            EmotionPreviewPanel controls,
            EmotionExpressionProfile emotions,
            CharacterSpeechClip[] examples,
            Camera camera
        )
        {
            characters = entries;
            panel = controls;
            profile = emotions;
            clips = examples;
            previewCamera = camera;
        }

        public void Select(int index)
        {
            foreach (var entry in characters)
            {
                entry.player.Stop();
                entry.root.SetActive(false);
            }
            var selected = characters[index];
            selected.root.SetActive(true);
            previewCamera.orthographicSize = selected.cameraSize;
            previewCamera.transform.SetPositionAndRotation(
                selected.center + selected.front * 3,
                Quaternion.LookRotation(-selected.front)
            );
            panel.Configure(selected.player, profile, clips, previewCamera, selected.center);
            panel.ShowEmotion(CharacterEmotion.WarmSmile);
        }

        private void Start() => Select(0);

        private void OnGUI()
        {
            if (characters == null || characters.Length == 0)
                return;
            GUILayout.BeginArea(
                new Rect(
                    Mathf.Max(12, Screen.width - 272),
                    12,
                    260,
                    Mathf.Min(Screen.height - 24, characters.Length * 30 + 48)
                ),
                GUI.skin.box
            );
            GUILayout.Label("캐릭터 선택 · 같은 표정과 음성 비교");
            scroll = GUILayout.BeginScrollView(scroll);
            for (int i = 0; i < characters.Length; i++)
                if (GUILayout.Button(characters[i].label))
                    Select(i);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
