using UnityEngine;

namespace A305.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterFaceController))]
    public sealed class CharacterIdleBlink : MonoBehaviour
    {
        private CharacterFaceController face;
        private SpeechExpressionProfile timing;
        private float startedAt;
        private float previousSeconds = float.NaN;
        public bool Suspended { get; set; }

        private void OnEnable()
        {
            startedAt = Time.time;
            previousSeconds = float.NaN;
        }

        public void ApplyAtTime(float seconds)
        {
            if (Suspended)
                return;
            if (face == null)
                face = GetComponent<CharacterFaceController>();
            if (face == null || !face.SupportsPose(CharacterFacePose.Blink))
                return;
            // The speech player owns the complete facial state, including its blink.
            var speech = GetComponentInParent<CharacterSpeechPlayer>();
            if (speech != null && (speech.IsPlaying || speech.IsPaused || speech.IsHoldingEmotion))
                return;
            if (timing == null)
                timing = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
            float current = Mathf.Max(0, seconds);
            float blink = timing.Sample(current % 14f, 15f).Blink;
            float elapsed = current - previousSeconds;
            if (elapsed > 0 && elapsed <= .25f)
            {
                // 낮은 프레임률에서 30ms의 완전 닫힘 구간을 건너뛰어도 한 프레임은 닫는다.
                for (float sample = previousSeconds; sample < current; sample += .01f)
                    blink = Mathf.Max(blink, timing.Sample(sample % 14f, 15f).Blink);
            }
            previousSeconds = current;
            face.ApplyBlink(blink);
        }

        private void LateUpdate() => ApplyAtTime(Time.time - startedAt);

        private void OnDestroy()
        {
            if (timing == null)
                return;
            if (Application.isPlaying)
                Destroy(timing);
            else
                DestroyImmediate(timing);
        }
    }
}
