using UnityEngine;

namespace A305.World
{
    /// <summary>횡단보도 건너편을 보는 사람 모양 신호. 점멸은 이미 건너는 사람의 마무리 시간이다.</summary>
    public sealed class PedestrianSignalHead : MonoBehaviour
    {
        [SerializeField]
        private bool crossesMainRoad;

        [SerializeField]
        private Vector3 crossingPoint;

        [SerializeField]
        private Renderer[] standingPerson;

        [SerializeField]
        private Renderer[] walkingPerson;

        [SerializeField]
        private Material redOn;

        [SerializeField]
        private Material greenOn;

        [SerializeField]
        private Material off;

        public bool CrossesMainRoad => crossesMainRoad;
        public Vector3 CrossingPoint => crossingPoint;

        public void Show(PedestrianLight light, float seconds)
        {
            Set(standingPerson, light == PedestrianLight.Stop, redOn);
            bool green =
                light == PedestrianLight.Walk
                || (light == PedestrianLight.Clearance && Mathf.Repeat(seconds, 1f) < .5f);
            Set(walkingPerson, green, greenOn);
        }

        private void Set(Renderer[] parts, bool lit, Material onMaterial)
        {
            if (parts == null)
                return;
            foreach (Renderer part in parts)
                if (part != null)
                    part.sharedMaterial = lit ? onMaterial : off;
        }
    }
}
