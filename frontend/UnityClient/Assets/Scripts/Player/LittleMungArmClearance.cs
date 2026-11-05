using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace A305.Player
{
    /// <summary>리틀 멍 체형에서 머리/몸통 내부로 들어가는 손과 전완의 목표 위치를 바깥으로 옮긴다.</summary>
    [DefaultExecutionOrder(600)]
    [RequireComponent(typeof(Animator))]
    public sealed class LittleMungArmClearance : MonoBehaviour
    {
        [SerializeField, Min(0)]
        private float handPadding = .008f;

        [SerializeField]
        [Tooltip(
            "중심점 보정 후 소매 표면이 남는 외형에만 추가 보정합니다. 기존 플레이어 자세에는 적용하지 않습니다."
        )]
        private bool refineSurfaceContact;
        private Transform space;
        private SkinnedVertexSamples head;
        private SkinnedVertexSamples torso;
        private readonly Transform[] upper = new Transform[2];
        private readonly Transform[] lower = new Transform[2];
        private readonly Transform[] hands = new Transform[2];
        private SkinnedVertexSamples[] handSurface;
        private SkinnedVertexSamples[] forearmSurface;
        private SkinnedVertexSamples contactHead,
            contactTorso;
        private readonly SkinnedVertexSamples[] contactArms = new SkinnedVertexSamples[2];

        private void Awake() => Initialize(GetComponentInParent<CharacterController>());

        public void Initialize(CharacterController controller)
        {
            var animator = GetComponent<Animator>();
            space = controller.transform;
            var headNames = Descendants(animator.GetBoneTransform(HumanBodyBones.Head));
            var torsoNames = new HashSet<string>();
            foreach (
                var id in new[]
                {
                    HumanBodyBones.Hips,
                    HumanBodyBones.Spine,
                    HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest,
                }
            )
            {
                var bone = animator.GetBoneTransform(id);
                if (bone != null)
                    torsoNames.Add(bone.name);
            }
            head = new SkinnedVertexSamples(animator, headNames.Contains);
            torso = new SkinnedVertexSamples(animator, torsoNames.Contains);
            if (refineSurfaceContact)
            {
                contactHead = new SkinnedVertexSamples(animator, headNames.Contains, .75f);
                contactTorso = new SkinnedVertexSamples(animator, torsoNames.Contains, .75f);
            }
            upper[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            upper[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lower[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lower[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hands[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            hands[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            handSurface = new SkinnedVertexSamples[2];
            forearmSurface = new SkinnedVertexSamples[2];
            for (int side = 0; side < 2; side++)
            {
                var handNames = Descendants(hands[side]);
                string lowerName = lower[side].name;
                handSurface[side] = new SkinnedVertexSamples(animator, handNames.Contains);
                forearmSurface[side] = new SkinnedVertexSamples(
                    animator,
                    name => name == lowerName
                );
                if (refineSurfaceContact)
                    contactArms[side] = new SkinnedVertexSamples(
                        animator,
                        name => handNames.Contains(name) || name == lowerName,
                        .75f
                    );
            }
        }

        private static HashSet<string> Descendants(Transform bone) =>
            new(bone.GetComponentsInChildren<Transform>().Select(t => t.name));

        private void LateUpdate() => CorrectPose();

        public void CorrectPose()
        {
            var headBounds = head.BoundsIn(space);
            var torsoBounds = torso.BoundsIn(space);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                // 고정 반경은 외형 확대 시 실제 전완보다 작아져 턱 관통을 놓친다.
                float forearmRadius = forearmSurface[side]
                    .RadiusAroundAxis(
                        space.InverseTransformPoint(lower[side].position),
                        space.InverseTransformPoint(hands[side].position),
                        space
                    );
                for (int iteration = 0; iteration < 8; iteration++)
                {
                    var hand = handSurface[side].BoundsIn(space);
                    var forearm = forearmSurface[side].BoundsIn(space);
                    Vector3 handShift =
                        Outside(
                            hand.center,
                            Mathf.Max(hand.extents.x, hand.extents.y, hand.extents.z) + handPadding,
                            sign,
                            headBounds,
                            torsoBounds
                        ) - hand.center;
                    Vector3 armShift =
                        Outside(forearm.center, forearmRadius, sign, headBounds, torsoBounds)
                        - forearm.center;
                    Vector3 shift = handShift + armShift * 1.5f;
                    if (shift.sqrMagnitude < .000001f)
                        break;
                    // Swing the authored arm pose as a whole. A hand-position IK
                    // solve straightened short arms and counter-rotated wrists.
                    Vector3 reach = hands[side].position - upper[side].position;
                    Vector3 target = reach + space.TransformVector(shift);
                    if (reach.sqrMagnitude < 1e-10f || target.sqrMagnitude < 1e-10f)
                        break;
                    upper[side].rotation =
                        Quaternion.FromToRotation(reach, target) * upper[side].rotation;
                }
                if (refineSurfaceContact)
                    RefineContact(side);
            }
        }

        private void RefineContact(int side)
        {
            float Clearance() =>
                contactArms[side]
                    .ClearanceFrom(
                        contactHead.BoundsIn(space),
                        contactTorso.BoundsIn(space),
                        space
                    );
            float clearance = Clearance();
            // 중심점 보정 뒤 소매 끝·팔꿈치가 남은 경우에만 실제 표면을 확인한다.
            for (int iteration = 0; iteration < 12 && clearance < .93f; iteration++)
            {
                Transform bestJoint = null;
                Quaternion bestRotation = default;
                float best = clearance;
                for (int jointIndex = 0; jointIndex < 2; jointIndex++)
                {
                    Transform joint = jointIndex == 0 ? upper[side] : lower[side];
                    Quaternion original = joint.rotation;
                    for (int axis = 0; axis < 3; axis++)
                    for (int direction = -1; direction <= 1; direction += 2)
                    {
                        var worldAxis = space.TransformDirection(
                            axis == 0 ? Vector3.right
                            : axis == 1 ? Vector3.up
                            : Vector3.forward
                        );
                        joint.rotation = Quaternion.AngleAxis(direction * 3f, worldAxis) * original;
                        float candidate = Clearance();
                        if (candidate > best + .0001f)
                        {
                            best = candidate;
                            bestJoint = joint;
                            bestRotation = joint.rotation;
                        }
                    }
                    joint.rotation = original;
                }
                if (bestJoint == null)
                    break;
                bestJoint.rotation = bestRotation;
                clearance = best;
            }
        }

        private static Vector3 Outside(
            Vector3 point,
            float padding,
            float sign,
            Bounds head,
            Bounds torso
        )
        {
            // 몸통은 좌우로, 큰 머리는 턱 아래로 피한다. 팔 길이를 늘리거나 모션 속도를 바꾸지 않는다.
            Vector3 bodyRadius = torso.extents + Vector3.one * padding;
            Vector3 bodyOffset = point - torso.center;
            float yz =
                bodyOffset.y * bodyOffset.y / (bodyRadius.y * bodyRadius.y)
                + bodyOffset.z * bodyOffset.z / (bodyRadius.z * bodyRadius.z);
            if (yz < 1)
            {
                float boundary = bodyRadius.x * Mathf.Sqrt(1 - yz);
                if (bodyOffset.x * sign < boundary)
                    point.x = torso.center.x + sign * boundary;
            }
            Vector3 radius = head.extents + Vector3.one * padding;
            Vector3 offset = point - head.center;
            float xz =
                offset.x * offset.x / (radius.x * radius.x)
                + offset.z * offset.z / (radius.z * radius.z);
            if (xz < 1)
            {
                float bottom = head.center.y - radius.y * Mathf.Sqrt(1 - xz);
                if (point.y > bottom && point.y < head.max.y + padding)
                    point.y = bottom;
            }
            return point;
        }
    }
}
