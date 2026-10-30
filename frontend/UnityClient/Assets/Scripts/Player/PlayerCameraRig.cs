using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>
    /// 플레이어를 따라가는 카메라. 탐험 중에는 3인칭·1인칭을 오가고,
    /// 대화가 시작되면 상대를 바라보는 1인칭으로 고정했다가 대화가 끝나면 직전 시점으로 돌아간다.
    /// 3인칭에서 걷는 동안에는 캐릭터 뒤로 천천히 돌아가 등 뒤에서 바라본다.
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField]
        private PlayerCameraSettings cameraSettings;

        [SerializeField]
        [Tooltip("카메라가 따라갈 플레이어")]
        private Transform followTarget;

        [SerializeField]
        [Tooltip("Player/Look. 마우스가 움직인 양")]
        private InputActionReference lookAction;

        [SerializeField]
        [Tooltip("Player/CameraRotate. 누르고 있는 동안 마우스 이동으로 카메라를 돌린다")]
        private InputActionReference rotateHoldAction;

        [SerializeField]
        [Tooltip("Player/CameraZoom. 마우스 휠")]
        private InputActionReference zoomAction;

        [SerializeField]
        [Tooltip("Player/ToggleView. 1인칭·3인칭 전환")]
        private InputActionReference toggleViewAction;

        private CameraOrbit orbit;
        private CameraCollisionDistance wallDistance;
        private PlayerMover mover;
        private PlayerCharacterDefinition character;
        private Transform dialogueLookTarget;
        private Transform vehicleView;
        private PlayerTouchControls touchControls;

        public CameraViewMode ViewMode => orbit.ViewMode;

        /// <summary>이번 프레임에 사용자가 마우스·터치로 카메라를 직접 돌렸는지. 이동 방향 기준을 새로 잡을 때 쓴다.</summary>
        public bool TurnedByUserThisFrame { get; private set; }

        /// <summary>대화 상대(보통 NPC의 머리 위치)를 바라보는 1인칭 시점으로 바꾼다.</summary>
        public void EnterDialogueView(Transform lookTarget)
        {
            if (lookTarget == null)
            {
                Debug.LogError("대화 시점에서 바라볼 대상이 없습니다.", this);
                return;
            }

            dialogueLookTarget = lookTarget;
            orbit.EnterDialogueView();
            Pose pose = GetDesiredPose(snap: true);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        public void ExitDialogueView()
        {
            orbit.ExitDialogueView();
            dialogueLookTarget = null;
            // 1인칭 눈높이에서 캐릭터 표시가 먼저 복구되면 얼굴 안쪽을 보게 되므로 바로 뒤로 복귀한다.
            SnapBehindTarget();
        }

        /// <summary>카메라를 플레이어 뒤로 돌려 바로 그 자리에 놓는다. 플레이어를 순간 이동시킨 뒤 호출한다.</summary>
        public void SnapBehindTarget()
        {
            orbit.SetYaw(followTarget.eulerAngles.y);
            Pose pose = GetDesiredPose(snap: true);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>차체에 붙은 승객 눈높이를 그대로 따라간다. 일반 대화 카메라의 벽 보정은 적용하지 않는다.</summary>
        public void EnterVehicleView(Transform anchor)
        {
            vehicleView = anchor;
            if (anchor != null)
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        public void ExitVehicleView()
        {
            vehicleView = null;
            SnapBehindTarget();
        }

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            var appearance = followTarget.GetComponent<PlayerCharacterAppearance>();
            character = appearance != null ? appearance.ActiveCharacter : null;
            orbit = cameraSettings.CreateOrbit();
            wallDistance = new CameraCollisionDistance(cameraSettings.CollisionRecoverSharpness);
            mover = followTarget.GetComponent<PlayerMover>();
            touchControls = followTarget.GetComponentInChildren<PlayerTouchControls>(true);
            SnapBehindTarget();
        }

        private void OnEnable()
        {
            lookAction.action.Enable();
            rotateHoldAction.action.Enable();
            zoomAction.action.Enable();
            toggleViewAction.action.Enable();
        }

        private void LateUpdate()
        {
            if (vehicleView != null)
            {
                transform.SetPositionAndRotation(vehicleView.position, vehicleView.rotation);
                return;
            }
            ApplyInput();
            // 직접 돌리는 동안에는 사용자가 고른 방향을 존중하고, 걸을 때만 등 뒤로 돌아간다.
            if (mover != null && mover.IsMoving && !TurnedByUserThisFrame)
            {
                orbit.FollowBehind(followTarget.eulerAngles.y, Time.deltaTime);
            }

            Pose desiredPose = GetDesiredPose(snap: false);
            // 프레임 속도와 관계없이 같은 빠르기로 따라가도록 지수 감쇠로 섞는다.
            float blend = 1f - Mathf.Exp(-cameraSettings.FollowSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, desiredPose.position, blend),
                Quaternion.Slerp(transform.rotation, desiredPose.rotation, blend)
            );
        }

        private void ApplyInput()
        {
            Vector2 turn = Vector2.zero;
            if (touchControls != null)
            {
                turn += touchControls.ConsumeLookDelta();
            }

            // 터치가 마우스 입력으로도 전달되는 브라우저에서 회전을 중복 적용하지 않는다.
            if (
                (touchControls == null || !touchControls.IsInteracting)
                && rotateHoldAction.action.IsPressed()
            )
            {
                turn += lookAction.action.ReadValue<Vector2>();
            }

            orbit.Rotate(turn);
            TurnedByUserThisFrame =
                turn != Vector2.zero && orbit.ViewMode != CameraViewMode.Dialogue;

            // 휠 한 번의 값은 브라우저·장치마다 크기가 달라 방향만 사용한다.
            float scroll = zoomAction.action.ReadValue<float>();
            if (scroll != 0f)
            {
                orbit.Zoom(Mathf.Sign(scroll));
            }

            if (toggleViewAction.action.WasPressedThisFrame())
            {
                orbit.ToggleFirstPerson();
            }
        }

        /// <param name="snap">순간 이동한 뒤처럼 벽 거리를 섞지 않고 바로 맞출지</param>
        private Pose GetDesiredPose(bool snap)
        {
            float eyeHeight =
                character != null && character.CameraEyeHeightMeters > 0
                    ? character.CameraEyeHeightMeters
                    : cameraSettings.EyeHeightMeters;
            Vector3 eye = followTarget.position + Vector3.up * eyeHeight;

            switch (orbit.ViewMode)
            {
                case CameraViewMode.FirstPerson:
                    return orbit.GetFirstPersonPose(eye);
                case CameraViewMode.Dialogue:
                    Camera camera = GetComponent<Camera>();
                    return DialogueFraming.Pose(
                        eye,
                        dialogueLookTarget.position,
                        camera.fieldOfView,
                        camera.aspect,
                        new Vector2(.64f, .66f)
                    );
                default:
                    return GetThirdPersonPoseInFrontOfWalls(snap);
            }
        }

        private Pose GetThirdPersonPoseInFrontOfWalls(bool snap)
        {
            float pivotHeight =
                character != null && character.CameraPivotHeightMeters > 0
                    ? character.CameraPivotHeightMeters
                    : cameraSettings.PivotHeightMeters;
            Vector3 pivot = followTarget.position + Vector3.up * pivotHeight;
            Pose pose = orbit.GetThirdPersonPose(pivot);
            Vector3 pivotToCamera = pose.position - pivot;
            Vector3 direction = pivotToCamera.normalized;
            float clearMeters = FindClearDistance(pivot, direction, pivotToCamera.magnitude);
            // 벽에 닿으면 바로 당기고, 풀리면 천천히 물러나 거리가 튀지 않게 한다.
            float distance = snap
                ? wallDistance.Snap(clearMeters)
                : wallDistance.Follow(clearMeters, Time.deltaTime);
            pose.position = pivot + direction * distance;

            return pose;
        }

        /// <summary>
        /// pivot에서 카메라 쪽으로 구를 굴려 처음 닿는 벽까지의 거리. 가로등·나무 기둥·사람처럼 가는 물체는
        /// 잠깐 가려도 카메라를 당기지 않고 지나쳐 본다.
        /// </summary>
        private float FindClearDistance(Vector3 pivot, Vector3 direction, float maxMeters)
        {
            float clearMeters = maxMeters;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot,
                cameraSettings.CollisionRadiusMeters,
                direction,
                maxMeters,
                cameraSettings.CollisionLayers,
                QueryTriggerInteraction.Ignore
            );
            foreach (RaycastHit hit in hits)
            {
                // pivot은 플레이어 콜라이더 안에 있어, 시작점에서 겹친 물체는 거리 0으로 나온다.
                if (
                    hit.distance <= 0f
                    || hit.collider.transform.IsChildOf(followTarget)
                    || CameraCollisionDistance.IsThinObstacle(hit.collider.bounds.size)
                )
                {
                    continue;
                }

                clearMeters = Mathf.Min(clearMeters, hit.distance);
            }

            return clearMeters;
        }

        private bool HasRequiredReferences()
        {
            bool hasAll = true;
            hasAll &= IsAssigned(cameraSettings, nameof(cameraSettings));
            hasAll &= IsAssigned(followTarget, nameof(followTarget));
            hasAll &= IsAssigned(lookAction, nameof(lookAction));
            hasAll &= IsAssigned(rotateHoldAction, nameof(rotateHoldAction));
            hasAll &= IsAssigned(zoomAction, nameof(zoomAction));
            hasAll &= IsAssigned(toggleViewAction, nameof(toggleViewAction));

            return hasAll;
        }

        private bool IsAssigned(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"PlayerCameraRig의 {fieldName}가 연결되지 않았습니다.", this);
            return false;
        }
    }
}
