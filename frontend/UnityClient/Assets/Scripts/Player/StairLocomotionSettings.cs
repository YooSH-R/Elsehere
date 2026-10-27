using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(menuName = "A305/Player/Stair Locomotion", fileName = "StairLocomotion")]
    public sealed class StairLocomotionSettings : ScriptableObject
    {
        [SerializeField, Min(.05f)]
        private float stepTravelMeters = .10f;

        [SerializeField, Min(.01f)]
        private float swingClearanceMeters = .055f;

        [SerializeField, Min(.01f)]
        private float bodySmoothSeconds = .11f;

        [SerializeField, Min(.1f)]
        private float lookAheadMeters = .5f;
        public float StepTravelMeters => stepTravelMeters;
        public float SwingClearanceMeters => swingClearanceMeters;
        public float BodySmoothSeconds => bodySmoothSeconds;
        public float LookAheadMeters => lookAheadMeters;
    }
}
