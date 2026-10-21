using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>
    /// 플레이어를 따라가는 카메라. 탐험 중에는 3인칭·1인칭을 오가고,
    /// 대화가 시작되면 상대를 바라보는 1인칭으로 고정했다가 대화가 끝나면 직전 시점으로 돌아간다.
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
        private Transform dialogueLookTarget;

        public CameraViewMode ViewMode => orbit.ViewMode;

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
        }

        public void ExitDialogueView()
        {
            orbit.ExitDialogueView();
            dialogueLookTarget = null;
        }

        /// <summary>카메라를 플레이어 뒤로 돌려 바로 그 자리에 놓는다. 플레이어를 순간 이동시킨 뒤 호출한다.</summary>
        public void SnapBehindTarget()
        {
            orbit.SetYaw(followTarget.eulerAngles.y);
            Pose pose = GetDesiredPose();
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private void Awake()
        {
            if (!HasRequiredReferences())
            {
                enabled = false;
                return;
            }

            orbit = cameraSettings.CreateOrbit();
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
            ApplyInput();

            Pose desiredPose = GetDesiredPose();
            // 프레임 속도와 관계없이 같은 빠르기로 따라가도록 지수 감쇠로 섞는다.
            float blend = 1f - Mathf.Exp(-cameraSettings.FollowSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, desiredPose.position, blend),
                Quaternion.Slerp(transform.rotation, desiredPose.rotation, blend)
            );
        }

        private void ApplyInput()
        {
            if (rotateHoldAction.action.IsPressed())
            {
                orbit.Rotate(lookAction.action.ReadValue<Vector2>());
            }

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

        private Pose GetDesiredPose()
        {
            Vector3 eye = followTarget.position + Vector3.up * cameraSettings.EyeHeightMeters;

            switch (orbit.ViewMode)
            {
                case CameraViewMode.FirstPerson:
                    return orbit.GetFirstPersonPose(eye);
                case CameraViewMode.Dialogue:
                    return CameraOrbit.GetDialoguePose(eye, dialogueLookTarget.position);
                default:
                    return GetThirdPersonPoseInFrontOfWalls();
            }
        }

        private Pose GetThirdPersonPoseInFrontOfWalls()
        {
            Vector3 pivot = followTarget.position + Vector3.up * cameraSettings.PivotHeightMeters;
            Pose pose = orbit.GetThirdPersonPose(pivot);
            Vector3 pivotToCamera = pose.position - pivot;

            // pivot에서 카메라 쪽으로 구를 굴려 벽에 닿으면 그 앞까지 당긴다.
            // pivot은 플레이어 콜라이더 안에 있어 시작점이 겹친 플레이어 자신은 검사에서 빠진다.
            bool isBlocked = Physics.SphereCast(
                pivot,
                cameraSettings.CollisionRadiusMeters,
                pivotToCamera.normalized,
                out RaycastHit hit,
                pivotToCamera.magnitude,
                cameraSettings.CollisionLayers,
                QueryTriggerInteraction.Ignore
            );
            if (isBlocked)
            {
                pose.position = pivot + pivotToCamera.normalized * hit.distance;
            }

            return pose;
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
