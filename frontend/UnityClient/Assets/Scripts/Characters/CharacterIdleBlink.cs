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
        public bool Suspended { get; set; }

        private void OnEnable() => startedAt = Time.time;

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
            if (speech != null && (speech.IsPlaying || speech.IsPaused))
                return;
            if (timing == null)
                timing = ScriptableObject.CreateInstance<SpeechExpressionProfile>();
            face.ApplyBlink(timing.Sample(Mathf.Max(0, seconds) % 14f, 15f).Blink);
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
