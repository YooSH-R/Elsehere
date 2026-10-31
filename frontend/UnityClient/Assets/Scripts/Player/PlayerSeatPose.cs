using A305.Characters;
using UnityEngine;

namespace A305.Player
{
    /// <summary>카페 의자에 앉은 동안 기존 Humanoid 골격을 의자 높이에 맞춰 구부린다.</summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerSeatPose : MonoBehaviour
    {
        private PlayerMover mover;
        private CharacterController controller;
        private Transform visual;
        private Animator animator;
        private Transform hips;
        private readonly Transform[] upper = new Transform[2];
        private readonly Transform[] lower = new Transform[2];
        private readonly Transform[] feet = new Transform[2];
        private Behaviour[] poseCorrections;
        private bool[] correctionStates;
        private Vector3 originalVisualPosition;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private Transform seat;
        private float seatCameraYaw;

        public bool IsSeated => seat != null;

        private void Awake()
        {
            mover = GetComponent<PlayerMover>();
            controller = GetComponent<CharacterController>();
        }

        public bool Sit(Transform chair, Vector3 exitPosition, float cameraYaw)
        {
            if (seat != null || chair == null || mover.IsMovementLocked)
                return false;

            var appearance = GetComponent<PlayerCharacterAppearance>();
            visual = appearance != null ? appearance.VisualRoot : null;
            animator = visual != null ? visual.GetComponentInChildren<Animator>() : null;
            if (animator == null || !animator.isHuman)
                return false;

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            upper[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            upper[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lower[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lower[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            feet[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            feet[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || upper[0] == null || upper[1] == null || lower[0] == null
                || lower[1] == null || feet[0] == null || feet[1] == null)
                return false;

            standingPosition = new Vector3(exitPosition.x, transform.position.y, exitPosition.z);
            standingRotation = chair.rotation;
            seatCameraYaw = cameraYaw;
            originalVisualPosition = visual.localPosition;
            poseCorrections = visual.GetComponentsInChildren<Behaviour>(true);
            correctionStates = new bool[poseCorrections.Length];
            for (int i = 0; i < poseCorrections.Length; i++)
            {
                Behaviour correction = poseCorrections[i];
                if (correction is LittleMungGroundContact
                    || correction is LittleMungTerrainContact
                    || correction is LittleMungStairLocomotion
                    || correction is LittleMungArmClearance)
                {
                    correctionStates[i] = correction.enabled;
                    correction.enabled = false;
                }
            }
            GetComponent<CharacterAnimationDriver>()?.SetLocomotion(0f, true, 0f);
            mover.SetMovementLocked(true);
            mover.enabled = false;
            controller.enabled = false;
            seat = chair;
            transform.SetPositionAndRotation(
                new Vector3(chair.position.x, standingPosition.y, chair.position.z),
                chair.rotation
            );
            mover.CameraRig?.SnapView(seatCameraYaw);
            return true;
        }

        private void LateUpdate()
        {
            if (seat == null)
                return;

            // Animator가 매 프레임 기본 자세를 갱신한 뒤 골반을 쿠션 위로 옮긴다.
            visual.localPosition = originalVisualPosition;
            Vector3 cushion = seat.TransformPoint(new Vector3(0f, .46f, 0f));
            visual.position += cushion - hips.position;
            for (int side = 0; side < 2; side++)
            {
                float offset = side == 0 ? -.13f : .13f;
                Vector3 footTarget = seat.TransformPoint(
                    new Vector3(offset, .06f, .38f)
                );
                TwoBonePose.Reach(upper[side], lower[side], feet[side], footTarget,
                    seat.forward);
            }
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
            mover.CameraRig?.SnapView(seatCameraYaw);
            mover.SetMovementLocked(false);
            mover.enabled = true;
        }

        private void OnDisable() => Stand();
    }
}
