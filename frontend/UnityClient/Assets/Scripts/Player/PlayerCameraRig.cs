using UnityEngine;
using UnityEngine.InputSystem;

namespace A305.Player
{
    /// <summary>
    /// 플레이어를 따라가는 카메라. 탐험 중에는 3인칭·1인칭을 오가고,
    /// 대화가 시작되면 1·3인칭과 따로 상대 얼굴 앞에서 얼굴을 비추는 대화 시점으로 고정했다가 대화가 끝나면 직전 시점으로 돌아간다.
    /// 튜토리얼처럼 다가오는 상대를 기다리게 할 때는 플레이어 어깨 너머로 상대를 비추는 구도로 붙잡고 사용자 입력을 받지 않는다.
    /// 시점은 마우스·터치로만 돌고, 키로 걸을 때는 위치만 따라간다.
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
        [Tooltip(
            "Player/CameraRotate(마우스 왼쪽 버튼). 누르고 있는 동안 마우스 이동으로 카메라를 돌린다"
        )]
        private InputActionReference rotateHoldAction;

        [SerializeField]
        [Tooltip("Player/CameraZoom. 마우스 휠")]
        private InputActionReference zoomAction;

        [SerializeField]
        [Tooltip("Player/ToggleView. 1인칭·3인칭 전환")]
        private InputActionReference toggleViewAction;

        private CameraOrbit orbit;
        private CameraCollisionDistance wallDistance;
        private PlayerCharacterDefinition character;
        private Transform dialogueLookTarget;
        private Transform meetingLookTarget;
        private Transform vehicleView;
        private PlayerTouchControls touchControls;
        private Vector3 thirdPersonPivot;
        private bool rotateHeldByUi;

        public CameraViewMode ViewMode => orbit.ViewMode;

        /// <summary>3인칭에서 카메라가 캐릭터 머리 높이 기준점에서 실제로 떨어진 거리. 좁은 실내에서 벽에 밀리면 짧아진다.</summary>
        public float CharacterDistanceMeters { get; private set; } = float.PositiveInfinity;

        /// <summary>이번 프레임에 사용자가 마우스·터치로 카메라를 직접 돌렸는지. 이동 방향 기준을 새로 잡을 때 쓴다.</summary>
        public bool TurnedByUserThisFrame { get; private set; }

        /// <summary>이번 프레임에 사용자가 휠로 가까이·멀리 보기를 했는지</summary>
        public bool ZoomedByUserThisFrame { get; private set; }

        /// <summary>이번 프레임에 사용자가 1인칭·3인칭을 바꿨는지</summary>
        public bool ToggledViewThisFrame { get; private set; }

        /// <summary>대화 상대(보통 NPC의 머리 위치) 앞 정해진 거리·높이에서 상대를 비추는 대화 시점으로 바꾼다.</summary>
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

        /// <summary>다가오는 상대를 기다리는 구도인지. 그동안 사용자는 시점을 돌리거나 줌·시점 전환을 할 수 없다.</summary>
        public bool IsInMeetingView => meetingLookTarget != null;

        /// <summary>
        /// 다가오는 상대(보통 NPC의 머리 위치)를 플레이어 오른쪽 어깨 너머로 비추는 구도로 붙잡는다(<see cref="MeetingFraming"/>).
        /// 상대가 걸어오는 동안 따라가고, 대화가 시작되면 대화 시점이 우선한다.
        /// </summary>
        public void EnterMeetingView(Transform lookTarget)
        {
            if (lookTarget == null)
            {
                Debug.LogError("기다리는 구도에서 바라볼 대상이 없습니다.", this);
                return;
            }

            meetingLookTarget = lookTarget;
            if (orbit.ViewMode != CameraViewMode.Dialogue)
            {
                Pose pose = GetDesiredPose(snap: true);
                transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        public void ExitMeetingView()
        {
            if (meetingLookTarget == null)
            {
                return;
            }

            meetingLookTarget = null;
            if (orbit.ViewMode != CameraViewMode.Dialogue)
            {
                SnapBehindTarget();
            }
        }

        /// <summary>카메라가 바라보는 수평 방향(마우스로 돌린 결과).</summary>
        public float ViewYawDegrees => orbit.YawDegrees;

        /// <summary>카메라를 플레이어 뒤로 돌려 바로 그 자리에 놓는다. 플레이어를 순간 이동시킨 뒤 호출한다.</summary>
        public void SnapBehindTarget()
        {
            SnapView(followTarget.eulerAngles.y);
        }

        /// <summary>카메라를 주어진 수평 방향으로 돌려 바로 그 자리에 놓는다.</summary>
        public void SnapView(float yawDegrees)
        {
            orbit.SetYaw(yawDegrees);
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

            if (meetingLookTarget != null)
            {
                IgnoreInput();
            }
            else
            {
                ApplyInput();
            }

            Pose desiredPose = GetDesiredPose(snap: false);
            // 프레임 속도와 관계없이 같은 빠르기로 따라가도록 지수 감쇠로 섞는다.
            float blend = 1f - Mathf.Exp(-cameraSettings.FollowSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, desiredPose.position, blend),
                Quaternion.Slerp(transform.rotation, desiredPose.rotation, blend)
            );
            CharacterDistanceMeters = Vector3.Distance(transform.position, thirdPersonPivot);
        }

        private void ApplyInput()
        {
            Vector2 turn = Vector2.zero;
            if (touchControls != null)
            {
                turn += touchControls.ConsumeLookDelta();
            }

            // 스크롤 목록·미니맵처럼 끌기를 받는 UI 위에서 누른 끌기는 그 UI가 쓰므로 시점을 돌리지 않는다.
            if (rotateHoldAction.action.WasPressedThisFrame())
            {
                rotateHeldByUi =
                    Pointer.current != null
                    && UiDragTarget.IsAt(Pointer.current.position.ReadValue());
            }

            // 터치가 마우스 입력으로도 전달되는 브라우저에서 회전을 중복 적용하지 않는다.
            if (
                (touchControls == null || !touchControls.IsInteracting)
                && rotateHoldAction.action.IsPressed()
                && !rotateHeldByUi
            )
            {
                turn += PointerDeltaScale.ToLogicalPixels(
                    lookAction.action.ReadValue<Vector2>(),
                    BrowserPointer.DevicePixelRatio
                );
            }

            // 사용자가 마이페이지에서 고른 감도 배율을 마우스·터치 회전에 함께 적용한다.
            orbit.Rotate(turn * LookSensitivity.Current);
            TurnedByUserThisFrame =
                turn != Vector2.zero && orbit.ViewMode != CameraViewMode.Dialogue;

            // 휠 한 번의 값은 브라우저·장치마다 크기가 달라 방향만 사용한다.
            float scroll = zoomAction.action.ReadValue<float>();
            // 스크롤 UI 위에서는 휠을 UI만 사용한다. 목록의 끝에 닿아도 카메라로 넘기지 않는다.
            ZoomedByUserThisFrame =
                scroll != 0f
                && (
                    Mouse.current == null
                    || !UiScrollTarget.IsAt(Mouse.current.position.ReadValue())
                );
            if (ZoomedByUserThisFrame)
            {
                orbit.Zoom(Mathf.Sign(scroll));
            }

            ToggledViewThisFrame = toggleViewAction.action.WasPressedThisFrame();
            if (ToggledViewThisFrame)
            {
                orbit.ToggleFirstPerson();
            }
        }

        // 기다리는 구도에서는 정한 구도를 지키도록 마우스·휠·키 입력을 버린다. 터치로 끈 양도 쌓이지 않게 비운다.
        private void IgnoreInput()
        {
            if (touchControls != null)
            {
                touchControls.ConsumeLookDelta();
            }

            TurnedByUserThisFrame = false;
            ZoomedByUserThisFrame = false;
            ToggledViewThisFrame = false;
        }

        /// <param name="snap">순간 이동한 뒤처럼 벽 거리를 섞지 않고 바로 맞출지</param>
        private Pose GetDesiredPose(bool snap)
        {
            float eyeHeight =
                character != null && character.CameraEyeHeightMeters > 0
                    ? character.CameraEyeHeightMeters
                    : cameraSettings.EyeHeightMeters;
            Vector3 eye = followTarget.position + Vector3.up * eyeHeight;
            if (meetingLookTarget != null && orbit.ViewMode != CameraViewMode.Dialogue)
            {
                return GetMeetingPoseInFrontOfWalls(eye);
            }

            switch (orbit.ViewMode)
            {
                case CameraViewMode.FirstPerson:
                    return orbit.GetFirstPersonPose(eye);
                case CameraViewMode.Dialogue:
                    // 대화 시점은 1·3인칭과 따로 NPC 얼굴 기준으로 놓는다. 플레이어 눈은 카메라를 얼굴의 어느 쪽에 둘지만 정한다.
                    Camera camera = GetComponent<Camera>();
                    Vector3 face = dialogueLookTarget.position;
                    return DialogueFraming.Pose(
                        DialogueFraming.CameraPosition(
                            face,
                            eye,
                            cameraSettings.DialogueDistanceMeters,
                            cameraSettings.DialogueHeightOffsetMeters
                        ),
                        face,
                        camera.fieldOfView,
                        camera.aspect,
                        cameraSettings.DialogueFacePosition
                    );
                default:
                    return GetThirdPersonPoseInFrontOfWalls(snap);
            }
        }

        // 숙소 문을 막 나온 자리처럼 플레이어 뒤가 벽이면 카메라를 벽 앞으로 당긴 뒤 상대를 비춘다.
        private Pose GetMeetingPoseInFrontOfWalls(Vector3 eye)
        {
            Vector3 face = meetingLookTarget.position;
            Vector3 eyeToCamera =
                MeetingFraming.CameraPosition(
                    eye,
                    face,
                    cameraSettings.MeetingBackMeters,
                    cameraSettings.MeetingSideMeters,
                    cameraSettings.MeetingHeightMeters
                ) - eye;
            Vector3 direction = eyeToCamera.normalized;
            float clearMeters = FindClearDistance(eye, direction, eyeToCamera.magnitude);
            Camera camera = GetComponent<Camera>();
            return MeetingFraming.Pose(
                eye + direction * clearMeters,
                face,
                camera.fieldOfView,
                camera.aspect
            );
        }

        private Pose GetThirdPersonPoseInFrontOfWalls(bool snap)
        {
            float pivotHeight =
                character != null && character.CameraPivotHeightMeters > 0
                    ? character.CameraPivotHeightMeters
                    : cameraSettings.PivotHeightMeters;
            Vector3 pivot = followTarget.position + Vector3.up * pivotHeight;
            thirdPersonPivot = pivot;
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
