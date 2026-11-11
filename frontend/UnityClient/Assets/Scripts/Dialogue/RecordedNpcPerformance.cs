using A305.Characters;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>저장 음성의 재생 시각 하나로 입모양과 전용 몸동작을 맞춘다.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class RecordedNpcPerformance : MonoBehaviour
    {
        [SerializeField]
        private RecordedPerformanceDefinition definition;

        [SerializeField]
        private CharacterSpeechPlayer speech;

        [SerializeField]
        private AudioSource source;

        [SerializeField]
        private Animator animator;
        private AudioClip playingClip;
        private float previousSpeed;
        private bool driving;
        public RecordedPerformanceDefinition Definition => definition;
        public bool IsPerforming => driving;

        public bool Accepts(string text) =>
            definition != null && definition.Voice != null && PerformanceRequest.Matches(text);

        public bool Owns(DialogueLine line) =>
            definition != null && line?.Speech != null && line.English == definition.English;

        public bool Play(DialogueServerLink link)
        {
            if (
                definition == null
                || definition.Voice == null
                || speech == null
                || source == null
                || animator == null
                || link == null
            )
                return false;
            StopMotion();
            if (!link.PlayRecordedSpeech(definition.Voice.bytes))
                return false;
            playingClip = source.clip;
            previousSpeed = animator.speed;
            animator.speed = 0;
            driving = true;
            SampleMotion(0);
            return true;
        }

        private void LateUpdate()
        {
            if (!driving)
                return;
            if (source.clip != playingClip || (!speech.IsPlaying && !speech.IsPaused))
            {
                StopMotion();
                return;
            }
            SampleMotion(speech.PlaybackTime);
        }

        // 검수 렌더도 런타임과 같은 시간→동작 변환을 사용한다.
        public void SampleMotion(float seconds)
        {
            if (!driving)
                return;
            float loopSeconds = 60 * definition.BeatsPerAnimationLoop / definition.BeatsPerMinute;
            animator.Play(definition.AnimationState, 0, Mathf.Max(0, seconds) / loopSeconds);
            animator.Update(0);
        }

        private void StopMotion()
        {
            if (!driving)
                return;
            animator.speed = previousSpeed;
            driving = false;
            playingClip = null;
        }

        private void OnDisable() => StopMotion();
    }
}
