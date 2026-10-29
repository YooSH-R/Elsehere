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
        private SpeechVisemeTimeline timeline;
        private bool active;
        private bool paused;
        public bool IsPlaying => active && !paused;
        public bool IsPaused => active && paused;
        public CharacterSpeechClip Speech => speech;
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
        }

        public void ConfigureExpressions(SpeechExpressionProfile profile) => expressions = profile;

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

        public void Play()
        {
            var nextTimeline = ReadTimeline();
            Stop();
            timeline = nextTimeline;
            speechSource.playOnAwake = false;
            speechSource.loop = false;
            speechSource.clip = speech.Audio;
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

        public void Stop()
        {
            if (speechSource != null)
                speechSource.Stop();
            active = paused = false;
            if (face != null)
                face.SetPose(CharacterFacePose.Neutral);
        }

        // Offline rendering uses the same evaluator as real AudioSource playback.
        public void ApplyAtAudioTime(float seconds)
        {
            if (timeline == null)
                timeline = ReadTimeline();
            var mouth = timeline.SampleArticulation(seconds);
            var expression =
                expressions != null
                    ? expressions.Sample(seconds, speech.Audio.length)
                    : new SpeechExpressionSample(0, 0);
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
