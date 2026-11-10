using UnityEngine;

namespace A305.Ambience
{
    /// <summary>승인된 장소 반복음 9개와 실제 사건에 쓰는 단발 효과 5개.</summary>
    public sealed class ServiceAmbienceCatalog : ScriptableObject
    {
        public const string ResourcePath = "Ambience/ServiceAmbience";

        [SerializeField]
        private AudioClip[] beds;

        [SerializeField]
        private AudioClip[] effects;

        public AudioClip Bed(AmbienceBed bed) =>
            beds != null && (int)bed >= 0 && (int)bed < beds.Length ? beds[(int)bed] : null;

        public AudioClip Effect(AmbienceEffect effect) =>
            effects != null && (int)effect >= 0 && (int)effect < effects.Length
                ? effects[(int)effect]
                : null;

        public void Configure(AudioClip[] beds, AudioClip[] effects)
        {
            this.beds = beds;
            this.effects = effects;
        }
    }
}
