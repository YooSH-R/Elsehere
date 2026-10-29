using A305.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>
    /// 키 입력으로 플레이어를 카메라 기준 방향으로 움직이고, 움직이는 방향으로 몸을 돌린다.
    /// 키를 누르고 있는 동안은 걷기 시작할 때의 카메라 방향을 기준으로 삼는다(<see cref="MoveReferenceYaw"/>).
    /// </summary>
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
        private PlayerTouchControls touchControls;
        private PlayerCameraRig cameraRig;
        private readonly MoveReferenceYaw moveReference = new MoveReferenceYaw();

        public PlayerCameraRig CameraRig =>
            cameraTransform != null ? cameraTransform.GetComponent<PlayerCameraRig>() : null;

        public bool IsMovementLocked => motor != null && motor.IsMovementLocked;

        /// <summary>이번 프레임에 수평으로 움직였는지. 점프·낙하만 하는 경우는 포함하지 않는다.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>대화처럼 캐릭터가 움직이면 안 되는 동안 이동과 점프를 막는다.</summary>
        public void SetMovementLocked(bool isLocked)
        {
            if (motor == null)
            {
                Debug.LogError("PlayerMover가 준비되지 않아 이동 잠금을 바꾸지 못했습니다.", this);
                return;
            }

            motor.SetMovementLocked(isLocked);
            if (touchControls != null)
            {
                touchControls.SetInputLocked(isLocked);
            }
        }

        /// <summary>플레이어를 바로 옮긴다. 다른 공간에 들어온 뒤 출입구 앞에 세울 때 쓴다.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // CharacterController가 켜진 채 옮기면 다음 Move에서 이전 위치로 돌아갈 수 있어 잠시 끈다.
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = true;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            var appearance = GetComponent<PlayerCharacterAppearance>();
            if (
                appearance != null
                && appearance.ActiveCharacter != null
                && appearance.ActiveCharacter.MovementSettings != null
            )
                movementSettings = appearance.ActiveCharacter.MovementSettings;
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            motor = movementSettings.CreateMotor();
            touchControls = GetComponentInChildren<PlayerTouchControls>(true);
            cameraRig = CameraRig;
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
            Vector2 move = moveAction.action.ReadValue<Vector2>();
            if (touchControls != null)
            {
                move = Vector2.ClampMagnitude(move + touchControls.Move, 1f);
            }

            // 카메라가 등 뒤로 따라 도는 동안에도 누르고 있는 키는 걷기 시작할 때의 카메라 방향으로 읽는다.
            float referenceYaw = moveReference.Resolve(
                move,
                cameraTransform.eulerAngles.y,
                cameraRig != null && cameraRig.TurnedByUserThisFrame
            );
            var input = new PlayerMoveInput(
                move,
                referenceYaw,
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
            IsMoving = horizontalVelocity.sqrMagnitude > 0.0001f;
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
