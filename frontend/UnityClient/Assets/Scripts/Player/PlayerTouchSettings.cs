using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(fileName = "PlayerTouchSettings", menuName = "A305/Player/Touch Settings")]
    public class PlayerTouchSettings : ScriptableObject
    {
        [SerializeField]
        [Range(0f, 0.9f)]
        private float stickDeadZone = 0.1f;

        [SerializeField]
        [Min(0f)]
        private float lookSensitivity = 1f;

        [SerializeField]
        [Range(0.5f, 1f)]
        [Tooltip("조이스틱을 이만큼(가장자리까지의 비율) 넘게 밀면 달린다. 그보다 덜 밀면 걷는다.")]
        private float runThreshold = 0.9f;

        public float StickDeadZone => stickDeadZone;

        public float RunThreshold => runThreshold;

        public float LookSensitivity => lookSensitivity;
    }
}
