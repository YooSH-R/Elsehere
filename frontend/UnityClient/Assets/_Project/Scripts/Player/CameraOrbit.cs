using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 카메라의 시점 상태(3인칭·1인칭·대화)와 회전·거리를 관리하고 카메라 위치를 계산한다.
    /// 대화가 끝나면 대화 직전의 시점으로 돌아간다.
    /// </summary>
    public sealed class CameraOrbit
    {
        private readonly CameraOrbitLimits limits;
        private CameraViewMode viewModeBeforeDialogue;

        public CameraOrbit(
            CameraOrbitLimits limits,
            float startDistanceMeters,
            float startPitchDegrees
        )
        {
            this.limits = limits;
            ViewMode = CameraViewMode.ThirdPerson;
            DistanceMeters = Mathf.Clamp(
                startDistanceMeters,
                limits.MinDistanceMeters,
                limits.MaxDistanceMeters
            );
            PitchDegrees = ClampPitch(startPitchDegrees);
        }

        public CameraViewMode ViewMode { get; private set; }

        public float YawDegrees { get; private set; }

        /// <summary>양수는 아래를 내려다보는 각도.</summary>
        public float PitchDegrees { get; private set; }

        /// <summary>3인칭에서 카메라와 플레이어 사이의 거리. 1인칭·대화 중에도 값을 유지한다.</summary>
        public float DistanceMeters { get; private set; }

        /// <summary>마우스를 위로 움직이면 위를, 아래로 움직이면 아래를 보도록 돌린다.</summary>
        public void Rotate(Vector2 pointerDeltaPixels)
        {
            if (ViewMode == CameraViewMode.Dialogue)
            {
                return;
            }

            YawDegrees += pointerDeltaPixels.x * limits.RotateDegreesPerPixel;
            PitchDegrees = ClampPitch(
                PitchDegrees - pointerDeltaPixels.y * limits.RotateDegreesPerPixel
            );
        }

        /// <summary>양수는 가까워지고 음수는 멀어진다. 3인칭에서만 적용한다.</summary>
        public void Zoom(float scrollSteps)
        {
            if (ViewMode != CameraViewMode.ThirdPerson)
            {
                return;
            }

            DistanceMeters = Mathf.Clamp(
                DistanceMeters - scrollSteps * limits.ZoomStepMeters,
                limits.MinDistanceMeters,
                limits.MaxDistanceMeters
            );
        }

        public void ToggleFirstPerson()
        {
            if (ViewMode == CameraViewMode.Dialogue)
            {
                return;
            }

            ViewMode =
                ViewMode == CameraViewMode.ThirdPerson
                    ? CameraViewMode.FirstPerson
                    : CameraViewMode.ThirdPerson;
            PitchDegrees = ClampPitch(PitchDegrees);
        }

        public void EnterDialogueView()
        {
            if (ViewMode == CameraViewMode.Dialogue)
            {
                return;
            }

            viewModeBeforeDialogue = ViewMode;
            ViewMode = CameraViewMode.Dialogue;
        }

        public void ExitDialogueView()
        {
            if (ViewMode != CameraViewMode.Dialogue)
            {
                return;
            }

            ViewMode = viewModeBeforeDialogue;
        }

        /// <summary>pivot을 바라보며 그 뒤쪽 위에 있는 카메라 위치. 벽에 막히는 경우는 호출한 쪽에서 처리한다.</summary>
        public Pose GetThirdPersonPose(Vector3 pivot)
        {
            Quaternion rotation = Quaternion.Euler(PitchDegrees, YawDegrees, 0f);
            Vector3 position = pivot + rotation * new Vector3(0f, 0f, -DistanceMeters);

            return new Pose(position, rotation);
        }

        public Pose GetFirstPersonPose(Vector3 eye)
        {
            return new Pose(eye, Quaternion.Euler(PitchDegrees, YawDegrees, 0f));
        }

        public static Pose GetDialoguePose(Vector3 eye, Vector3 lookTarget)
        {
            return new Pose(eye, Quaternion.LookRotation(lookTarget - eye));
        }

        private float ClampPitch(float pitchDegrees)
        {
            if (ViewMode == CameraViewMode.FirstPerson)
            {
                return Mathf.Clamp(
                    pitchDegrees,
                    limits.FirstPersonMinPitchDegrees,
                    limits.FirstPersonMaxPitchDegrees
                );
            }

            return Mathf.Clamp(
                pitchDegrees,
                limits.ThirdPersonMinPitchDegrees,
                limits.ThirdPersonMaxPitchDegrees
            );
        }
    }
}
