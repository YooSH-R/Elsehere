using A305.Characters;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 의자·벤치·소파에 앉은 동안 기존 Humanoid 골격을 앉는 면 높이에 맞춰 구부린다. 일어서면 앉기 전에 서 있던 곳으로 돌아간다.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerSeatPose : MonoBehaviour
    {
        // 허벅지 두께의 절반. 다리 시작점을 쿠션보다 이만큼 올려 허벅지가 쿠션에 묻히지 않게 한다.
        private const float ThighRadius = .06f;

        // 무릎을 앞 모서리보다 이만큼 앞에 두어 내려뜨린 정강이가 의자·벤치·소파 앞면에 묻히지 않게 한다.
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
            GetComponent<CharacterAnimationDriver>()?.SetLocomotion(0f, true, 0f);
            mover.SetMovementLocked(true);
            mover.enabled = false;
            controller.enabled = false;
            seat = seatPoint;
            transform.SetPositionAndRotation(
                new Vector3(seatPoint.position.x, standingPosition.y, seatPoint.position.z),
                seatPoint.rotation
            );
            mover.CameraRig?.SnapView(seatCameraYaw);
            return true;
        }

        private void LateUpdate()
        {
            if (seat == null)
                return;

            // Animator가 매 프레임 기본 자세를 갱신한 뒤 몸을 옮긴다. 리틀멍은 다리가 짧아 자리 가운데에 앉히면 다리가 쿠션에 묻히므로,
            // 아이처럼 자리 앞쪽에 앉혀 허벅지는 쿠션 위에 앞으로 눕히고 무릎 아래는 앞 모서리 밖으로 내려뜨린다.
            visual.localPosition = originalVisualPosition;
            float thigh = Vector3.Distance(upper[0].position, lower[0].position);
            Vector3 legRoot = (upper[0].position + upper[1].position) * .5f;
            Vector3 seatedLegRoot = seat.TransformPoint(
                new Vector3(
                    0f,
                    cushionTopMeters + ThighRadius,
                    frontEdgeMeters + KneeOverhang - thigh
                )
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
