using UnityEngine;

namespace A305.Player
{
    /// <summary>한 프레임 동안 들어온 이동 입력과 그 기준이 되는 카메라 방향.</summary>
    public readonly struct PlayerMoveInput
    {
        public PlayerMoveInput(
            Vector2 move,
            float cameraYawDegrees,
            bool isSprinting,
            bool isJumpPressed
        )
        {
            Move = move;
            CameraYawDegrees = cameraYawDegrees;
            IsSprinting = isSprinting;
            IsJumpPressed = isJumpPressed;
        }

        /// <summary>x는 좌우, y는 앞뒤. 키보드는 -1~1, 스틱은 기울인 만큼의 값.</summary>
        public Vector2 Move { get; }

        public float CameraYawDegrees { get; }

        public bool IsSprinting { get; }

        public bool IsJumpPressed { get; }
    }
}
