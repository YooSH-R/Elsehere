using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// Humanoid 골격을 의자·벤치·소파의 앉는 면 높이에 맞춰 구부린다. 플레이어(<see cref="PlayerSeatPose"/>)와 NPC가 함께 쓴다.
    /// 리틀멍은 다리가 짧아 자리 가운데에 앉히면 다리가 쿠션에 묻히므로, 아이처럼 자리 앞쪽에 앉혀 허벅지는 쿠션 위에 앞으로 눕히고
    /// 무릎 아래는 앞 모서리 밖으로 내려뜨린다.
    /// </summary>
    public sealed class SeatedLegs
    {
        // 허벅지 두께의 절반. 다리 시작점을 쿠션보다 이만큼 올려 허벅지가 쿠션에 묻히지 않게 한다.
        private const float ThighRadius = .06f;

        // 무릎을 앞 모서리보다 이만큼 앞에 두어 내려뜨린 정강이가 의자·벤치·소파 앞면에 묻히지 않게 한다.
        private const float KneeOverhang = .05f;

        private readonly Transform[] upper;
        private readonly Transform[] lower;
        private readonly Transform[] feet;

        private SeatedLegs(Transform[] upper, Transform[] lower, Transform[] feet)
        {
            this.upper = upper;
            this.lower = lower;
            this.feet = feet;
        }

        /// <summary>Humanoid 다리 뼈를 찾는다. Humanoid가 아니거나 뼈가 빠졌으면 null</summary>
        public static SeatedLegs Find(Animator animator)
        {
            if (animator == null || !animator.isHuman)
                return null;

            Transform[] upper =
            {
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
            };
            Transform[] lower =
            {
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
            };
            Transform[] feet =
            {
                animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                animator.GetBoneTransform(HumanBodyBones.RightFoot),
            };
            if (
                animator.GetBoneTransform(HumanBodyBones.Hips) == null
                || System.Array.Exists(upper, bone => bone == null)
                || System.Array.Exists(lower, bone => bone == null)
                || System.Array.Exists(feet, bone => bone == null)
            )
                return null;

            return new SeatedLegs(upper, lower, feet);
        }

        /// <summary>
        /// Animator가 이번 프레임의 기본 자세를 갱신한 뒤(LateUpdate) 부른다. 모델을 서 있던 위치에서 자리 앞쪽으로 옮기고 다리를 구부린다.
        /// </summary>
        /// <param name="visual">골격을 담은 모델 루트</param>
        /// <param name="standingLocalPosition">앉기 전 모델의 localPosition</param>
        /// <param name="seat">앉을 자리. 원점은 바닥, 앞(+z)은 앉아서 보는 방향이다.</param>
        public void Apply(
            Transform visual,
            Vector3 standingLocalPosition,
            Transform seat,
            float cushionTopMeters,
            float frontEdgeMeters
        )
        {
            visual.localPosition = standingLocalPosition;
            float thigh = Vector3.Distance(upper[0].position, lower[0].position);
            Vector3 legRoot = (upper[0].position + upper[1].position) * .5f;
            Vector3 seatedLegRoot = seat.TransformPoint(
                new Vector3(0f, cushionTopMeters + ThighRadius, frontEdgeMeters + KneeOverhang - thigh)
            );
            visual.position += seatedLegRoot - legRoot;
            for (int side = 0; side < 2; side++)
            {
                float shin = Vector3.Distance(lower[side].position, feet[side].position);
                Vector3 knee = upper[side].position + seat.forward * thigh;
                Vector3 footTarget = knee - seat.up * shin + seat.forward * .01f;
                TwoBonePose.Reach(upper[side], lower[side], feet[side], footTarget, seat.forward + seat.up);
            }
        }
    }
}
