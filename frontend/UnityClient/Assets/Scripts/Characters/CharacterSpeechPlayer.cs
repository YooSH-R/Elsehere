using System;
using UnityEngine;

namespace A305.Characters
{
    public sealed class CharacterSpeechPlayer : MonoBehaviour
    {
        [SerializeField]
        private CharacterFaceController face;

        [SerializeField]
        private AudioSource speechSource;

        [SerializeField]
        private CharacterSpeechClip speech;

        [SerializeField]
        private SpeechExpressionProfile expressions;

        [SerializeField]
        private EmotionExpressionProfile emotionProfile;

        [SerializeField]
        private CharacterEmotion speakingMood = CharacterEmotion.WarmSmile;

        [SerializeField, Range(0, 1)]
        private float moodIntensity = 1;
        private SpeechVisemeTimeline timeline;
        private SpeechEmotionTrack emotionTrack;
        private EmotionExpressionProfile activeEmotions;
        private AudioClip activeAudio;
        private bool active;
        private bool paused;
        public bool IsPlaying => active && !paused;
        public bool IsPaused => active && paused;
        public CharacterSpeechClip Speech => speech;
        public CharacterFaceController Face => face;
        public SpeechExpressionProfile Expressions => expressions;
        public bool IsConfigured => face != null && speechSource != null;
        public float PlaybackTime =>
            speechSource != null && speechSource.clip != null
                ? (float)speechSource.timeSamples / speechSource.clip.frequency
                : 0;

        public void Configure(
            CharacterFaceController target,
            AudioSource source,
            CharacterSpeechClip clip
        )
        {
            Stop();
            face = target;
            speechSource = source;
            speech = clip;
            timeline = null;
            activeAudio = null;
        }

        public void ConfigureExpressions(SpeechExpressionProfile profile) => expressions = profile;

        // Sets the mood for subsequent utterances. An explicit per-utterance
        // emotion track always takes priority, including its neutral gaps.
        public void ConfigureMood(
            EmotionExpressionProfile profile,
            CharacterEmotion mood,
            float intensity = 1
        )
        {
            if (
                !Enum.IsDefined(typeof(CharacterEmotion), mood)
                || !float.IsFinite(intensity)
                || intensity < 0
                || intensity > 1
            )
                throw new ArgumentOutOfRangeException(
                    nameof(intensity),
                    "기분 종류와 강도를 확인하세요."
                );
            emotionProfile = profile;
            speakingMood = mood;
            moodIntensity = intensity;
        }

        private SpeechVisemeTimeline ReadTimeline()
        {
            if (
                face == null
                || speechSource == null
                || speech == null
                || speech.Audio == null
                || speech.Timeline == null
            )
                throw new InvalidOperationException(
                    "Connect a face, AudioSource, and matching speech clip/timeline."
                );
            return SpeechVisemeTimeline.Parse(speech.Timeline.text, speech.Audio.length);
        }

        public void Play() =>
            Play(
                speech != null ? speech.Audio : null,
                ReadTimeline(),
                speech.CreateEmotionTrack(),
                speech.EmotionProfile
            );

        // Runtime dialogue supplies the downloaded voice and its own timing.
        // A legacy voice without timing still plays, without invented mouth cues.
        public void Play(
            AudioClip audio,
            SpeechVisemeTimeline phonemes,
            SpeechEmotionTrack reactions = null,
            EmotionExpressionProfile profile = null
        )
        {
            if (face == null || speechSource == null || audio == null)
                throw new InvalidOperationException(
                    "Connect a face, AudioSource and runtime voice."
                );
            if (phonemes != null && Mathf.Abs(phonemes.Duration - audio.length) > .02f)
                throw new ArgumentException("Phoneme timing does not match the voice length.");
            if (reactions == null && emotionProfile != null)
            {
                reactions = new SpeechEmotionTrack(
                    new[]
                    {
                        new SpeechEmotionCue
                        {
                            start = 0,
                            end = audio.length,
                            emotion = speakingMood,
                            intensity = moodIntensity,
                        },
                    },
                    audio.length
                );
                profile = emotionProfile;
            }
            if (
                reactions != null
                && (profile == null || Mathf.Abs(reactions.Duration - audio.length) > .02f)
            )
                throw new ArgumentException("표정 시간표와 음성 길이·프로필을 확인하세요.");
            Stop();
            timeline = phonemes;
            emotionTrack = reactions;
            activeEmotions = profile;
            activeAudio = audio;
            speechSource.playOnAwake = false;
            speechSource.loop = false;
            speechSource.clip = audio;
            speechSource.timeSamples = 0;
            active = true;
            paused = false;
            ApplyAtAudioTime(0);
            speechSource.Play();
        }

        public void Pause()
        {
            if (!active || paused)
                return;
            ApplyAtAudioTime(PlaybackTime);
            speechSource.Pause();
            paused = true;
        }

        public void Resume()
        {
            if (!active || !paused)
                return;
            speechSource.UnPause();
            paused = false;
        }

        public void Seek(float seconds)
        {
            if (!float.IsFinite(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (activeAudio == null)
                ApplyAtAudioTime(0);
            speechSource.clip = activeAudio;
            float time = Mathf.Clamp(seconds, 0, activeAudio.length);
            speechSource.timeSamples = Mathf.Min(
                activeAudio.samples - 1,
                Mathf.RoundToInt(time * activeAudio.frequency)
            );
            ApplyAtAudioTime(time);
        }

        public void Stop()
        {
            if (speechSource != null)
                speechSource.Stop();
            active = paused = false;
            activeAudio = null;
            timeline = null;
            emotionTrack = null;
            activeEmotions = null;
            if (face != null)
                face.SetPose(CharacterFacePose.Neutral);
        }

        // Offline rendering uses the same evaluator as real AudioSource playback.
        public void ApplyAtAudioTime(float seconds)
        {
            if (activeAudio == null)
            {
                timeline = ReadTimeline();
                activeAudio = speech.Audio;
                emotionTrack = speech.CreateEmotionTrack();
                activeEmotions = speech.EmotionProfile;
            }
            var mouth =
                timeline != null ? timeline.SampleArticulation(seconds) : SpeechMouthSample.Rest;
            var expression =
                expressions != null
                    ? expressions.Sample(seconds, activeAudio.length)
                    : new SpeechExpressionSample(0, 0);
            if (emotionTrack != null && activeEmotions != null)
                face.ApplySpeech(
                    mouth,
                    emotionTrack.Sample(seconds, activeEmotions),
                    expression.Blink
                );
            else
                face.ApplySpeech(mouth, expression.Smile, expression.Blink);
        }

        private void LateUpdate()
        {
            if (!active || paused)
                return;
            if (!speechSource.isPlaying)
            {
                Stop();
                return;
            }
            ApplyAtAudioTime(PlaybackTime);
        }

        private void OnDisable() => Stop();
    }
}
