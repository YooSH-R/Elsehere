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

        public float StickDeadZone => stickDeadZone;

        public float LookSensitivity => lookSensitivity;
    }
}
