using UnityEngine;

namespace A305.Ambience
{
    public sealed class FootstepCatalog : ScriptableObject
    {
        public const string ResourcePath = "Ambience/Footsteps";

        [SerializeField]
        private AudioClip[] steps;
        public int Count => steps?.Length ?? 0;

        public AudioClip Step(int index) => Count > 0 ? steps[index % Count] : null;

        public void Configure(AudioClip[] clips) => steps = clips;
    }
}
