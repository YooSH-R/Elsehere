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
        private EmotionExpressionProfile runtimeEmotions;
        private AudioClip activeAudio;
        private bool active;
        private bool paused;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private bool awaitingAudioResume;
        private bool streaming;
        private bool keepEmotionAfterSpeech;
        private float streamTimeOffset;
        private float emotionTimeOffset;
        private float expressionReleaseAt = -1;
        private FaceEmotionSample initialEmotion;
        private FaceEmotionSample lastEmotion;
        private FaceEmotionSample heldEmotion;
        private bool hasRestingMood;
        private bool ApplicationSuspended => !applicationFocused || applicationPaused;
        public bool IsPlaying => active && !IsPaused;
        public bool IsPaused => active && (paused || ApplicationSuspended);
        public bool IsHoldingEmotion => !active && expressionReleaseAt >= 0;
        public bool HasRestingMood => hasRestingMood;
        private FaceEmotionSample RestingExpression =>
            hasRestingMood ? emotionProfile.Sample(speakingMood, moodIntensity) : default;
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

        // NPC의 평소 인상을 대기·중단·감정 정보 없는 음성에 함께 사용한다.
        public void ConfigureRestingMood(CharacterEmotion mood)
        {
            ConfigureMood(ResolveEmotionProfile(), mood);
            hasRestingMood = true;
            if (!active && !IsHoldingEmotion)
                face.ApplySpeech(SpeechMouthSample.Rest, RestingExpression, 0);
        }

        private EmotionExpressionProfile ResolveEmotionProfile()
        {
            if (emotionProfile != null)
                return emotionProfile;
            if (runtimeEmotions == null)
            {
                runtimeEmotions = ScriptableObject.CreateInstance<EmotionExpressionProfile>();
                runtimeEmotions.hideFlags = HideFlags.DontSave;
            }
            return runtimeEmotions;
        }

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
            EmotionExpressionProfile profile = null,
            bool continueSpeech = false,
            bool holdEmotionAfterSpeech = false
        )
        {
            if (face == null || speechSource == null || audio == null)
                throw new InvalidOperationException(
                    "Connect a face, AudioSource and runtime voice."
                );
            if (phonemes != null && Mathf.Abs(phonemes.Duration - audio.length) > .02f)
                throw new ArgumentException("Phoneme timing does not match the voice length.");
            if (reactions != null && profile == null)
                profile = ResolveEmotionProfile();
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
            var previousEmotion = continueSpeech && streaming ? lastEmotion : RestingExpression;
            float previousEmotionSeconds =
                continueSpeech && streaming && reactions != null
                    ? reactions.ContinuationSeconds(emotionTrack, emotionTimeOffset)
                    : 0;
            float previousDuration =
                continueSpeech && streaming && activeAudio != null
                    ? streamTimeOffset + activeAudio.length
                    : 0;
            Stop();
            streaming = continueSpeech;
            keepEmotionAfterSpeech = holdEmotionAfterSpeech && reactions != null;
            initialEmotion = previousEmotion;
            streamTimeOffset = previousDuration;
            emotionTimeOffset = previousEmotionSeconds;
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
            if (ApplicationSuspended)
                speechSource.Pause();
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
            paused = false;
            if (!ApplicationSuspended)
                speechSource.UnPause();
        }

        private void OnApplicationFocus(bool focused)
        {
            bool wasSuspended = ApplicationSuspended;
            applicationFocused = focused;
            SynchronizeApplicationPlayback(wasSuspended);
        }

        private void OnApplicationPause(bool pause)
        {
            bool wasSuspended = ApplicationSuspended;
            applicationPaused = pause;
            SynchronizeApplicationPlayback(wasSuspended);
        }

        private void SynchronizeApplicationPlayback(bool wasSuspended)
        {
            if (wasSuspended == ApplicationSuspended || !active || paused)
                return;
            ApplyAtAudioTime(PlaybackTime);
            if (ApplicationSuspended)
                speechSource.Pause();
            else
                ResumeAudio();
        }

        private void ResumeAudio()
        {
            // Web Audio 컨텍스트는 창 복귀 프레임보다 늦게 재개될 수 있다.
            // 실제 음성이 재개되기 전의 isPlaying=false는 자연 종료가 아니다.
            awaitingAudioResume = true;
            speechSource.UnPause();
            ApplyAtAudioTime(PlaybackTime);
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
            awaitingAudioResume = false;
            activeAudio = null;
            timeline = null;
            emotionTrack = null;
            activeEmotions = null;
            streaming = false;
            keepEmotionAfterSpeech = false;
            streamTimeOffset = 0;
            emotionTimeOffset = 0;
            expressionReleaseAt = -1;
            initialEmotion = lastEmotion = heldEmotion = default;
            if (face != null)
                face.ApplySpeech(SpeechMouthSample.Rest, RestingExpression, 0);
        }

        public void StopForListening()
        {
            if (IsHoldingEmotion)
                return;
            float seconds = PlaybackTime;
            if (!active || emotionTrack == null || seconds <= 0)
            {
                Stop();
                return;
            }
            ApplyAtAudioTime(seconds);
            emotionTrack = emotionTrack.Truncate(seconds);
            speechSource.Stop();
            active = paused = awaitingAudioResume = false;
            activeAudio = null;
            timeline = null;
            streaming = false;
            keepEmotionAfterSpeech = true;
            expressionReleaseAt = Time.unscaledTime;
            heldEmotion = lastEmotion;
            face.ApplySpeech(SpeechMouthSample.Rest, heldEmotion, 0);
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
                    ? expressions.Sample(
                        streamTimeOffset + seconds,
                        streamTimeOffset + activeAudio.length + (streaming ? 1 : 0)
                    )
                    : new SpeechExpressionSample(0, 0);
            if (emotionTrack != null && activeEmotions != null)
            {
                lastEmotion = emotionTrack.Sample(
                    seconds,
                    activeEmotions,
                    initialEmotion,
                    streaming || keepEmotionAfterSpeech,
                    emotionTimeOffset
                );
                face.ApplySpeech(mouth, lastEmotion, expression.Blink);
            }
            else
                face.ApplySpeech(mouth, expression.Smile, expression.Blink);
        }

        private void LateUpdate()
        {
            if (!active)
            {
                if (expressionReleaseAt >= 0)
                {
                    if (keepEmotionAfterSpeech)
                    {
                        ApplyListeningExpression();
                        return;
                    }
                    // 다음 구절이 바로 오면 이어 받고, 음성이 끝나면 잠시 뒤 자연스럽게 푼다.
                    float release = Mathf.Clamp01(
                        (Time.unscaledTime - expressionReleaseAt - .15f) / .24f
                    );
                    lastEmotion = FaceEmotionSample.Lerp(
                        SampleHeldEmotion(Time.unscaledTime - expressionReleaseAt),
                        default,
                        Mathf.SmoothStep(0, 1, release)
                    );
                    face.ApplySpeech(SpeechMouthSample.Rest, lastEmotion, 0);
                    if (release >= 1)
                        Stop();
                }
                else if (hasRestingMood)
                {
                    float blink =
                        expressions != null
                            ? expressions.Sample(Time.unscaledTime % 14f, 15f).Blink
                            : 0;
                    face.ApplySpeech(SpeechMouthSample.Rest, RestingExpression, blink);
                }
                return;
            }
            if (IsPaused)
                return;
            if (speechSource.clip != activeAudio)
            {
                Stop();
                return;
            }
            if (!speechSource.isPlaying)
            {
                if (awaitingAudioResume)
                    return;
                if (streaming || keepEmotionAfterSpeech)
                {
                    ApplyAtAudioTime(Mathf.Max(0, activeAudio.length - .0001f));
                    heldEmotion = lastEmotion;
                    expressionReleaseAt = Time.unscaledTime;
                    active = false;
                    face.ApplySpeech(SpeechMouthSample.Rest, heldEmotion, 0);
                }
                else
                    Stop();
                return;
            }
            awaitingAudioResume = false;
            ApplyAtAudioTime(PlaybackTime);
        }

        private void ApplyListeningExpression()
        {
            float elapsed = Time.unscaledTime - expressionReleaseAt;
            float seconds = (streamTimeOffset + emotionTrack.Duration + elapsed) % 14f;
            float blink = expressions != null ? expressions.Sample(seconds, 15f).Blink : 0;
            lastEmotion = SampleHeldEmotion(elapsed);
            face.ApplySpeech(SpeechMouthSample.Rest, lastEmotion, blink);
        }

        private FaceEmotionSample SampleHeldEmotion(float elapsed) =>
            emotionTrack != null && activeEmotions != null
                ? emotionTrack.Sample(
                    emotionTrack.Duration + elapsed,
                    activeEmotions,
                    initialEmotion,
                    holdLast: true,
                    initialEmotionSeconds: emotionTimeOffset
                )
                : heldEmotion;

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (runtimeEmotions != null)
                Destroy(runtimeEmotions);
        }
    }
}
