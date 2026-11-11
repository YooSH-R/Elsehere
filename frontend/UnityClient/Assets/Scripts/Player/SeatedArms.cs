using UnityEngine;

namespace A305.Player
{
    /// <summary>앉은 동안 양손을 허벅지 앞에 두고, 일어서면 원래 팔 자세를 복원한다.</summary>
    public sealed class SeatedArms
    {
        private readonly Transform[] upperArms = new Transform[2];
        private readonly Transform[] forearms = new Transform[2];
        private readonly Transform[] hands = new Transform[2];
        private readonly Transform[] thighs = new Transform[2];
        private readonly Transform[] knees = new Transform[2];
        private readonly Quaternion[,] standingRotations = new Quaternion[2, 3];
        private readonly Quaternion[] palmFrames = new Quaternion[2];
        private readonly bool[] hasPalmFrame = new bool[2];

        public static SeatedArms Find(Animator animator)
        {
            if (animator == null || !animator.isHuman)
                return null;

            var pose = new SeatedArms();
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                pose.upperArms[side] = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm
                );
                pose.forearms[side] = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm
                );
                pose.hands[side] = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
                );
                pose.thighs[side] = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg
                );
                pose.knees[side] = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg
                );
                if (
                    pose.upperArms[side] == null
                    || pose.forearms[side] == null
                    || pose.hands[side] == null
                    || pose.thighs[side] == null
                    || pose.knees[side] == null
                )
                    return null;
                pose.standingRotations[side, 0] = pose.upperArms[side].localRotation;
                pose.standingRotations[side, 1] = pose.forearms[side].localRotation;
                pose.standingRotations[side, 2] = pose.hands[side].localRotation;

                Transform middle = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal
                );
                Transform index = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal
                );
                Transform little = animator.GetBoneTransform(
                    left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal
                );
                if (middle != null && index != null && little != null)
                {
                    Vector3 fingers = middle.position - pose.hands[side].position;
                    Vector3 palmNormal =
                        Vector3.Cross(index.position - little.position, fingers) * (left ? 1 : -1);
                    pose.palmFrames[side] =
                        Quaternion.Inverse(pose.hands[side].rotation)
                        * Quaternion.LookRotation(fingers, palmNormal);
                    pose.hasPalmFrame[side] = true;
                }
            }
            return pose;
        }

        /// <summary>다리를 앉는 자세로 구부린 뒤 호출한다. 팔 길이는 유지하고 손바닥을 허벅지 쪽으로 향하게 한다.</summary>
        public void Apply(Transform seat)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 outward = seat.right * (side == 0 ? -1 : 1);
                float upperLength = Vector3.Distance(
                    upperArms[side].position,
                    forearms[side].position
                );
                // 짧은 팔이 무릎까지 억지로 늘어나지 않게 관절 계산은 실제 팔 길이에서 멈춘다.
                // 손목을 허벅지 표면 위에 두고 팔꿈치를 바깥으로 굽혀 소매가 몸통에 묻히지 않게 한다.
                Vector3 wrist =
                    Vector3.Lerp(thighs[side].position, knees[side].position, .95f)
                    + seat.up * (upperLength * .8f)
                    + outward * (upperLength * .3f);
                TwoBonePose.Reach(
                    upperArms[side],
                    forearms[side],
                    hands[side],
                    wrist,
                    outward - seat.up * .3f
                );
                if (hasPalmFrame[side])
                    hands[side].rotation =
                        Quaternion.LookRotation(seat.forward, -seat.up)
                        * Quaternion.Inverse(palmFrames[side]);
                else
                    hands[side].localRotation = standingRotations[side, 2];
            }
        }

        public void Restore()
        {
            for (int side = 0; side < 2; side++)
            {
                upperArms[side].localRotation = standingRotations[side, 0];
                forearms[side].localRotation = standingRotations[side, 1];
                hands[side].localRotation = standingRotations[side, 2];
            }
        }
    }
}
