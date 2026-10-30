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
        // 빠르게 출발·정지할 때 애니메이션 자세가 한 프레임에 바뀌면 골반이 순간이동해 보인다.
        // 실제 이동은 바로 바꾸고, 애니메이션에 전달하는 속도는 초당 이만큼만 바뀌게 한다.
        // 서 있다가 달리면 약 0.25초에 걸쳐 바뀐다.
        private const float AnimationSpeedChangePerSecond = 30f;

        // 이 속도 이하에서의 변화는 튀는 폭이 작아 원래대로 바로 전달한다. 계단 디딤 보정이
        // 천천히 걸을 때의 걷기 박자에 맞춰져 있어 박자가 밀리지 않게 한다.
        private const float InstantAnimationSpeedMetersPerSecond = 1f;

        // 캡슐 아래 둥근 면이 가구 모서리에 걸리면 CharacterController는 땅에 섰다고 보지만, 발밑은 비어 있어
        // 가구 옆 공중에 떠 보인다. 발밑에 턱 높이 안으로 땅이 없으면 땅이 아닌 것으로 보고 모서리 바깥으로 민다.
        private const float EdgeSlideSpeedMetersPerSecond = 2f;

        // 발밑 땅을 찾는 작은 구. 바닥 타일 사이 틈은 건너뛰고, 가구 모서리처럼 옆으로 떨어진 면은 찾지 않는다.
        private const float GroundProbeRadiusMeters = .1f;

        // 이보다 위를 향한 면에 닿았을 때만 발밑을 받친 면으로 본다. 벽에 닿은 점은 빼기 위해서다.
        private const float GroundContactMinNormalY = .1f;

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
        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private float animationSpeed;

        // 마지막 이동에서 발밑을 받친 면의 닿은 점. 모서리에 걸렸을 때 밀어낼 방향을 정한다.
        private Vector3? groundContactPoint;

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
            groundContactPoint = null;
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

            // 카메라가 플레이어 뒤로 다시 맞춰져도 누르고 있는 키는 걷기 시작할 때의 카메라 방향으로 읽는다.
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
            bool isOnEdge = characterController.isGrounded && !HasGroundUnderFeet();
            bool isGrounded = characterController.isGrounded && !isOnEdge;
            Vector3 velocity = motor.ComputeVelocity(input, isGrounded, Time.deltaTime);
            Vector3 edgeSlide = isOnEdge
                ? AwayFromEdge() * EdgeSlideSpeedMetersPerSecond
                : Vector3.zero;

            groundContactPoint = null;
            characterController.Move((velocity + edgeSlide) * Time.deltaTime);

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            IsMoving = horizontalVelocity.sqrMagnitude > 0.0001f;
            TurnToward(horizontalVelocity);

            if (animationDriver != null)
            {
                float speed = horizontalVelocity.magnitude;
                animationSpeed =
                    Mathf.Max(animationSpeed, speed) <= InstantAnimationSpeedMetersPerSecond
                        ? speed
                        : Mathf.MoveTowards(
                            animationSpeed,
                            speed,
                            AnimationSpeedChangePerSecond * Time.deltaTime
                        );
                animationDriver.SetLocomotion(animationSpeed, isGrounded, velocity.y);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > GroundContactMinNormalY)
            {
                groundContactPoint = hit.point;
            }
        }

        /// <summary>발 바로 아래 턱 높이 안에 디딜 땅이 있는지</summary>
        private bool HasGroundUnderFeet()
        {
            Vector3 feet =
                transform.position
                + characterController.center
                + Vector3.down * (characterController.height / 2f);
            // 구가 시작부터 바닥에 닿아 있으면 찾지 못하므로 발보다 조금 위에서 내린다.
            Vector3 start = feet + Vector3.up * (GroundProbeRadiusMeters * 2f);
            int count = Physics.SphereCastNonAlloc(
                start,
                GroundProbeRadiusMeters,
                Vector3.down,
                groundHits,
                GroundProbeRadiusMeters + characterController.stepOffset,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            for (int index = 0; index < count; index++)
            {
                if (!groundHits[index].transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>모서리에 닿은 점에서 플레이어 쪽으로 향하는 수평 방향</summary>
        private Vector3 AwayFromEdge()
        {
            if (groundContactPoint == null)
            {
                return Vector3.zero;
            }

            Vector3 away = transform.position - groundContactPoint.Value;
            away.y = 0f;
            return away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero;
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
