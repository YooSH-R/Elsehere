using A305.Characters;
using UnityEngine;

namespace A305.Player
{
    /// <summary>카페 의자에 앉은 동안 기존 Humanoid 골격을 의자 높이에 맞춰 구부린다.</summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerSeatPose : MonoBehaviour
    {
        // 카페 의자 기준(의자 원점에서 잰 값). 의자 충돌 메시로 잰 쿠션 윗면 높이와 앞 모서리까지의 거리다.
        private const float CushionTop = .33f;
        private const float FrontEdge = .30f;

        // 허벅지 두께의 절반. 다리 시작점을 쿠션보다 이만큼 올려 허벅지가 쿠션에 묻히지 않게 한다.
        private const float ThighRadius = .06f;

        // 무릎을 앞 모서리보다 이만큼 앞에 두어 내려뜨린 정강이가 의자 앞면에 묻히지 않게 한다.
        private const float KneeOverhang = .05f;

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
            if (
                hips == null
                || upper[0] == null
                || upper[1] == null
                || lower[0] == null
                || lower[1] == null
                || feet[0] == null
                || feet[1] == null
            )
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

            // Animator가 매 프레임 기본 자세를 갱신한 뒤 몸을 옮긴다. 리틀멍은 다리가 짧아 의자 가운데에 앉히면 다리가 쿠션에 묻히므로,
            // 아이처럼 의자 앞쪽에 앉혀 허벅지는 쿠션 위에 앞으로 눕히고 무릎 아래는 앞 모서리 밖으로 내려뜨린다.
            visual.localPosition = originalVisualPosition;
            float thigh = Vector3.Distance(upper[0].position, lower[0].position);
            Vector3 legRoot = (upper[0].position + upper[1].position) * .5f;
            Vector3 seatedLegRoot = seat.TransformPoint(
                new Vector3(0f, CushionTop + ThighRadius, FrontEdge + KneeOverhang - thigh)
            );
            visual.position += seatedLegRoot - legRoot;
            for (int side = 0; side < 2; side++)
            {
                float shin = Vector3.Distance(lower[side].position, feet[side].position);
                Vector3 knee = upper[side].position + seat.forward * thigh;
                Vector3 footTarget = knee - seat.up * shin + seat.forward * .01f;
                TwoBonePose.Reach(
                    upper[side],
                    lower[side],
                    feet[side],
                    footTarget,
                    seat.forward + seat.up
                );
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
