using UnityEngine;

namespace A305.Player
{
    [CreateAssetMenu(
        fileName = "PlayerMovementSettings",
        menuName = "A305/Player/Movement Settings"
    )]
    public class PlayerMovementSettings : ScriptableObject
    {
        [SerializeField]
        [Min(0f)]
        private float walkSpeedMetersPerSecond = 2.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Shift를 누르고 있을 때의 속도")]
        private float sprintSpeedMetersPerSecond = 5f;

        [SerializeField]
        [Min(0f)]
        private float jumpHeightMeters = 1f;

        [SerializeField]
        [Min(0.1f)]
        private float gravityMetersPerSecondSquared = 20f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이동하는 방향으로 몸을 돌리는 속도")]
        private float turnSpeedDegreesPerSecond = 720f;

        public float TurnSpeedDegreesPerSecond => turnSpeedDegreesPerSecond;

        public PlayerMotor CreateMotor()
        {
            return new PlayerMotor(
                walkSpeedMetersPerSecond,
                sprintSpeedMetersPerSecond,
                jumpHeightMeters,
                gravityMetersPerSecondSquared
            );
        }
    }
}
