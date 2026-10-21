using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 이동 입력과 카메라 방향으로 플레이어가 이번 프레임에 움직일 속도를 계산한다.
    /// 수직 속도(점프·낙하)는 프레임 사이에 이어지므로 이 객체가 기억한다.
    /// </summary>
    public sealed class PlayerMotor
    {
        // 땅에 서 있을 때도 살짝 아래로 눌러야 CharacterController가 경사·계단에서 땅에 붙어 있다고 판단한다.
        private const float GroundedDownwardSpeedMetersPerSecond = -2f;

        private readonly float walkSpeedMetersPerSecond;
        private readonly float sprintSpeedMetersPerSecond;
        private readonly float jumpStartSpeedMetersPerSecond;
        private readonly float gravityMetersPerSecondSquared;
        private float verticalSpeedMetersPerSecond;

        public PlayerMotor(
            float walkSpeedMetersPerSecond,
            float sprintSpeedMetersPerSecond,
            float jumpHeightMeters,
            float gravityMetersPerSecondSquared
        )
        {
            this.walkSpeedMetersPerSecond = walkSpeedMetersPerSecond;
            this.sprintSpeedMetersPerSecond = sprintSpeedMetersPerSecond;
            this.gravityMetersPerSecondSquared = gravityMetersPerSecondSquared;
            jumpStartSpeedMetersPerSecond = Mathf.Sqrt(
                2f * gravityMetersPerSecondSquared * jumpHeightMeters
            );
        }

        /// <summary>대화처럼 캐릭터가 움직이면 안 되는 동안 이동과 점프를 막는다. 중력은 계속 적용한다.</summary>
        public bool IsMovementLocked { get; private set; }

        public void SetMovementLocked(bool isLocked)
        {
            IsMovementLocked = isLocked;
        }

        public Vector3 ComputeVelocity(PlayerMoveInput input, bool isGrounded, float deltaTimeSeconds)
        {
            Vector3 horizontalVelocity = IsMovementLocked
                ? Vector3.zero
                : ComputeHorizontalVelocity(input);
            bool canJump = isGrounded && input.IsJumpPressed && !IsMovementLocked;

            if (canJump)
            {
                verticalSpeedMetersPerSecond = jumpStartSpeedMetersPerSecond;
            }
            else if (isGrounded)
            {
                verticalSpeedMetersPerSecond = GroundedDownwardSpeedMetersPerSecond;
            }
            else
            {
                verticalSpeedMetersPerSecond -= gravityMetersPerSecondSquared * deltaTimeSeconds;
            }

            return new Vector3(
                horizontalVelocity.x,
                verticalSpeedMetersPerSecond,
                horizontalVelocity.z
            );
        }

        private Vector3 ComputeHorizontalVelocity(PlayerMoveInput input)
        {
            // 키보드 대각선은 길이가 1보다 커지므로 1로 줄이고, 덜 기울인 스틱은 그 값을 유지한다.
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 cameraRelativeDirection =
                Quaternion.Euler(0f, input.CameraYawDegrees, 0f) * new Vector3(move.x, 0f, move.y);
            float speed = input.IsSprinting
                ? sprintSpeedMetersPerSecond
                : walkSpeedMetersPerSecond;

            return cameraRelativeDirection * speed;
        }
    }
}
