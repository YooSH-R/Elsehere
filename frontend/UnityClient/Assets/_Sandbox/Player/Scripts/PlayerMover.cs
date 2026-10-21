using A305.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>키 입력으로 플레이어를 카메라 기준 방향으로 움직이고, 움직이는 방향으로 몸을 돌린다.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField]
        private PlayerMovementSettings movementSettings;

        [SerializeField]
        [Tooltip("이동 방향의 기준이 되는 카메라")]
        private Transform cameraTransform;

        [SerializeField]
        [Tooltip("Player/Move")]
        private InputActionReference moveAction;

        [SerializeField]
        [Tooltip("Player/Sprint")]
        private InputActionReference sprintAction;

        [SerializeField]
        [Tooltip("Player/Jump")]
        private InputActionReference jumpAction;

        [SerializeField]
        [Tooltip("모델 애니메이션에 이동 상태를 전달한다. 비워 두면 전달하지 않는다.")]
        private CharacterAnimationDriver animationDriver;

        private CharacterController characterController;
        private PlayerMotor motor;

        public bool IsMovementLocked => motor != null && motor.IsMovementLocked;

        /// <summary>대화처럼 캐릭터가 움직이면 안 되는 동안 이동과 점프를 막는다.</summary>
        public void SetMovementLocked(bool isLocked)
        {
            if (motor == null)
            {
                Debug.LogError("PlayerMover가 준비되지 않아 이동 잠금을 바꾸지 못했습니다.", this);
                return;
            }

            motor.SetMovementLocked(isLocked);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            motor = movementSettings.CreateMotor();
        }

        private void OnEnable()
        {
            // 프로젝트 전체 입력 액션은 기본으로 켜져 있지만, 다른 곳에서 꺼 두었을 때를 대비한다.
            moveAction.action.Enable();
            sprintAction.action.Enable();
            jumpAction.action.Enable();
        }

        private void Update()
        {
            var input = new PlayerMoveInput(
                moveAction.action.ReadValue<Vector2>(),
                cameraTransform.eulerAngles.y,
                sprintAction.action.IsPressed(),
                jumpAction.action.WasPressedThisFrame()
            );
            Vector3 velocity = motor.ComputeVelocity(
                input,
                characterController.isGrounded,
                Time.deltaTime
            );

            characterController.Move(velocity * Time.deltaTime);

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            TurnToward(horizontalVelocity);

            if (animationDriver != null)
            {
                animationDriver.SetLocomotion(
                    horizontalVelocity.magnitude,
                    characterController.isGrounded,
                    velocity.y
                );
            }
        }

        private void TurnToward(Vector3 horizontalVelocity)
        {
            if (horizontalVelocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                movementSettings.TurnSpeedDegreesPerSecond * Time.deltaTime
            );
        }

        private bool HasRequiredReferences()
        {
            bool hasAll = true;
            hasAll &= IsAssigned(movementSettings, nameof(movementSettings));
            hasAll &= IsAssigned(cameraTransform, nameof(cameraTransform));
            hasAll &= IsAssigned(moveAction, nameof(moveAction));
            hasAll &= IsAssigned(sprintAction, nameof(sprintAction));
            hasAll &= IsAssigned(jumpAction, nameof(jumpAction));

            return hasAll;
        }

        private bool IsAssigned(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"PlayerMover의 {fieldName}가 연결되지 않았습니다.", this);
            return false;
        }
    }
}
