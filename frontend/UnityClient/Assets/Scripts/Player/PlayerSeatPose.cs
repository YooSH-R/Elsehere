using A305.Characters;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 의자·벤치·소파에 앉은 동안 기존 Humanoid 골격을 앉는 면 높이에 맞춰 구부린다(<see cref="SeatedLegs"/>). 일어서면 앉기 전에 서 있던
    /// 곳으로 돌아간다.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerSeatPose : MonoBehaviour
    {
        private PlayerMover mover;
        private CharacterController controller;
        private Transform visual;
        private SeatedLegs legs;
        private Behaviour[] poseCorrections;
        private bool[] correctionStates;
        private Vector3 originalVisualPosition;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private Transform seat;
        private float cushionTopMeters;
        private float frontEdgeMeters;
        private float seatCameraYaw;

        public bool IsSeated => seat != null;

        private void Awake()
        {
            mover = GetComponent<PlayerMover>();
            controller = GetComponent<CharacterController>();
        }

        /// <param name="seatPoint">앉을 자리. 원점은 바닥, 앞(+z)은 앉아서 보는 방향이다.</param>
        /// <param name="cushionTopMeters">자리 원점에서 쿠션 윗면까지의 높이</param>
        /// <param name="frontEdgeMeters">자리 원점에서 앉는 면 앞 모서리까지의 거리</param>
        public bool Sit(Transform seatPoint, float cushionTopMeters, float frontEdgeMeters)
        {
            if (seat != null || seatPoint == null || mover.IsMovementLocked)
                return false;

            var appearance = GetComponent<PlayerCharacterAppearance>();
            visual = appearance != null ? appearance.VisualRoot : null;
            legs = SeatedLegs.Find(
                visual != null ? visual.GetComponentInChildren<Animator>() : null
            );
            if (legs == null)
                return false;

            // 서 있던 곳은 캡슐이 들어가 있던 빈 자리라, 일어설 때 탁자·팔걸이·벽에 끼지 않는다.
            standingPosition = transform.position;
            standingRotation = seatPoint.rotation;
            seatCameraYaw = SeatView.CameraYawDegrees(
                transform.position,
                seatPoint.position,
                seatPoint.eulerAngles.y
            );
            this.cushionTopMeters = cushionTopMeters;
            this.frontEdgeMeters = frontEdgeMeters;
            originalVisualPosition = visual.localPosition;
            poseCorrections = visual.GetComponentsInChildren<Behaviour>(true);
            correctionStates = new bool[poseCorrections.Length];
            for (int i = 0; i < poseCorrections.Length; i++)
            {
                Behaviour correction = poseCorrections[i];
                if (
                    correction is LittleMungGroundContact
                    || correction is LittleMungTerrainContact
                    || correction is LittleMungStairLocomotion
                    || correction is LittleMungArmClearance
                )
                {
                    correctionStates[i] = correction.enabled;
                    correction.enabled = false;
                }
            }
            var animationDriver = GetComponent<CharacterAnimationDriver>();
            if (animationDriver != null)
                animationDriver.SetLocomotion(0f, true, 0f);
            mover.SetMovementLocked(true);
            mover.enabled = false;
            controller.enabled = false;
            seat = seatPoint;
            transform.SetPositionAndRotation(
                new Vector3(seatPoint.position.x, standingPosition.y, seatPoint.position.z),
                seatPoint.rotation
            );
            if (mover.CameraRig != null)
                mover.CameraRig.SnapView(seatCameraYaw);
            return true;
        }

        private void LateUpdate()
        {
            if (seat == null)
                return;

            // Animator가 매 프레임 기본 자세를 갱신한 뒤 몸을 옮긴다.
            legs.Apply(visual, originalVisualPosition, seat, cushionTopMeters, frontEdgeMeters);
        }

        public void Stand()
        {
            if (seat == null)
                return;

            seat = null;
            visual.localPosition = originalVisualPosition;
            for (int i = 0; i < poseCorrections.Length; i++)
            {
                if (correctionStates[i])
                    poseCorrections[i].enabled = true;
            }
            mover.Teleport(standingPosition, standingRotation);
            if (mover.CameraRig != null)
                mover.CameraRig.SnapView(seatCameraYaw);
            mover.SetMovementLocked(false);
            mover.enabled = true;
        }

        private void OnDisable() => Stand();
    }
}
