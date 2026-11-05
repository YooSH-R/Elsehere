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

        [SerializeField]
        private EmotionExpressionProfile emotionProfile;

        [SerializeField]
        private SpeechEmotionCue[] emotionCues = System.Array.Empty<SpeechEmotionCue>();
        public AudioClip Audio => audioClip;
        public TextAsset Timeline => visemeTimeline;
        public EmotionExpressionProfile EmotionProfile => emotionProfile;

        public void ConfigureEmotions(EmotionExpressionProfile profile, SpeechEmotionCue[] cues)
        {
            if (cues != null && cues.Length > 0)
            {
                if (audioClip == null || profile == null)
                    throw new System.ArgumentException(
                        "표정 시간표에는 음성과 표정 프로필이 필요합니다."
                    );
                _ = new SpeechEmotionTrack(cues, audioClip.length);
            }
            emotionProfile = profile;
            emotionCues =
                cues == null
                    ? System.Array.Empty<SpeechEmotionCue>()
                    : System.Array.ConvertAll(
                        cues,
                        cue => new SpeechEmotionCue
                        {
                            start = cue.start,
                            end = cue.end,
                            emotion = cue.emotion,
                            intensity = cue.intensity,
                        }
                    );
        }

        public SpeechEmotionTrack CreateEmotionTrack() =>
            emotionProfile != null && emotionCues.Length > 0
                ? new SpeechEmotionTrack(emotionCues, audioClip.length)
                : null;

        public void Configure(AudioClip audio, TextAsset timeline)
        {
            audioClip = audio;
            visemeTimeline = timeline;
            emotionProfile = null;
            emotionCues = System.Array.Empty<SpeechEmotionCue>();
        }
    }
}
