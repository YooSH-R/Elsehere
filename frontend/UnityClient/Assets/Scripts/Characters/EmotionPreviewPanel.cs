using UnityEngine;

namespace A305.Characters
{
    public sealed class EmotionPreviewPanel : MonoBehaviour
    {
        private static readonly string[] Labels =
        {
            "평온",
            "따뜻한 미소",
            "의문",
            "당황",
            "찡그림",
            "놀람",
            "장난기",
            "웃음",
            "걱정·공감",
            "슬픔",
            "폭소",
            "무신경·시큰둥",
            "분노",
            "격한 분노",
            "쑥스러움",
            "누그러짐",
        };

        [SerializeField]
        private CharacterSpeechPlayer player;

        [SerializeField]
        private EmotionExpressionProfile emotions;

        [SerializeField]
        private CharacterSpeechClip[] clips;

        [SerializeField]
        private Camera previewCamera;

        [SerializeField]
        private Vector3 center;

        [SerializeField]
        private Vector3 front;
        private int selected;
        private CharacterEmotion pose;
        private float strength = 1;
        private float cursor;
        private bool manual = true;

        public void Configure(
            CharacterSpeechPlayer target,
            EmotionExpressionProfile profile,
            CharacterSpeechClip[] speech,
            Camera camera,
            Vector3 pivot
        )
        {
            player = target;
            emotions = profile;
            clips = speech;
            previewCamera = camera;
            center = pivot;
            front = camera.transform.position - center;
        }

        public void ShowEmotion(CharacterEmotion emotion, float intensity = 1)
        {
            player.Stop();
            pose = emotion;
            strength = intensity;
            manual = true;
            player.Face.ApplySpeech(SpeechMouthSample.Rest, emotions.Sample(pose, strength), 0);
        }

        public void PlayExample(int index)
        {
            selected = index;
            manual = false;
            player.Configure(player.Face, player.GetComponent<AudioSource>(), clips[selected]);
            player.Play();
        }

        public void PlaySelectedEmotion()
        {
            manual = false;
            var clip = clips[selected];
            player.Configure(player.Face, player.GetComponent<AudioSource>(), clip);
            var timeline = SpeechVisemeTimeline.Parse(clip.Timeline.text, clip.Audio.length);
            var reactions = new SpeechEmotionTrack(
                new[]
                {
                    new SpeechEmotionCue
                    {
                        start = 0,
                        end = clip.Audio.length,
                        emotion = pose,
                        intensity = strength,
                    },
                },
                clip.Audio.length
            );
            player.Play(clip.Audio, timeline, reactions, emotions);
        }

        public void Scrub(float seconds)
        {
            if (!player.IsPlaying && !player.IsPaused)
                PlayExample(selected);
            player.Pause();
            manual = false;
            cursor = seconds;
            player.Seek(cursor);
        }

        private void OnGUI()
        {
            if (player == null || emotions == null || clips == null || clips.Length == 0)
                return;
            float width = Mathf.Min(360, Screen.width - 24);
            GUILayout.BeginArea(
                new Rect(12, 12, width, Mathf.Min(540, Screen.height - 24)),
                GUI.skin.box
            );
            GUILayout.Label(player.name + " · 말하기 표정 검수");
            GUILayout.Label("로컬 녹음·검수용 표정 구간 / 서버 호출 없음");
            for (int row = 0; row < (Labels.Length + 2) / 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 3 + column;
                    if (index >= Labels.Length)
                        break;
                    if (GUILayout.Button(Labels[index]))
                        ShowEmotion((CharacterEmotion)index, strength);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"표정 강도 {strength:P0}");
            float value = GUILayout.HorizontalSlider(strength, 0, 1);
            if (manual && !Mathf.Approximately(value, strength))
                ShowEmotion(pose, value);
            strength = value;
            if (GUILayout.Button(Labels[(int)pose] + " 표정으로 대사 재생"))
                PlaySelectedEmotion();
            if (GUILayout.Button("의문 → 장난기 → 웃음 재생"))
                PlayExample(0);
            if (GUILayout.Button("찡그림 → 공감 → 평온 재생"))
                PlayExample(1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(player.IsPaused ? "이어서 재생" : "일시 정지"))
            {
                if (player.IsPaused)
                    player.Resume();
                else
                    player.Pause();
            }
            if (GUILayout.Button("정지 · 중립"))
                ShowEmotion(CharacterEmotion.Calm);
            GUILayout.EndHorizontal();
            if (player.IsPlaying)
                cursor = player.PlaybackTime;
            float duration = clips[selected].Audio.length;
            GUILayout.Label($"음성 시각 {cursor:F2} / {duration:F2}초");
            float scrub = GUILayout.HorizontalSlider(cursor, 0, duration);
            if (!Mathf.Approximately(scrub, cursor))
                Scrub(scrub);
            GUILayout.BeginHorizontal();
            foreach (float angle in new[] { -30f, 0, 30f })
                if (
                    GUILayout.Button(
                        angle == 0 ? "정면"
                        : angle < 0 ? "왼쪽 사선"
                        : "오른쪽 사선"
                    )
                )
                {
                    previewCamera.transform.position =
                        center + Quaternion.AngleAxis(angle, Vector3.up) * front;
                    previewCamera.transform.LookAt(center);
                }
            GUILayout.EndHorizontal();
            string[] missing = CharacterFaceController.MissingEmotionChannels(
                player.Face.transform
            );
            GUILayout.Label(
                missing.Length == 0
                    ? "감정 채널 14개 연결됨"
                    : "누락: " + string.Join(", ", missing)
            );
            bool hasLaughOpening = System.Array.Exists(
                player.Face.GetComponentsInChildren<SkinnedMeshRenderer>(),
                skin =>
                    skin.sharedMesh != null
                    && skin.sharedMesh.GetBlendShapeIndex("mouthLaughOpen") >= 0
            );
            GUILayout.Label(
                hasLaughOpening ? "폭소용 입 연결됨" : "폭소용 입 없음 · 기본 입 벌림 사용"
            );
            GUILayout.Label(
                "웃음 음성 합성은 포함하지 않습니다. 말하는 동안 입은 발음 시간표를 따릅니다."
            );
            GUILayout.EndArea();
        }
    }
}
