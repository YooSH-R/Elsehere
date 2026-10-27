using UnityEngine;

namespace A305.Player
{
    /// <summary>경사면 정렬, 접지 위치 고정, 계단을 넘는 발의 높이를 리틀 멍 사본에 적용한다.</summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Animator))]
    public sealed class LittleMungTerrainContact : MonoBehaviour
    {
        [SerializeField]
        private Vector3[] soleUp = new Vector3[2];

        private sealed class Foot
        {
            public Transform upper,
                lower,
                bone,
                toes;
            public SkinnedVertexSamples surface;
            public bool planted,
                hasPrevious;
            public Vector3 previousLocal,
                anchor,
                animatedLocal;
            public Quaternion anchorRotation,
                toeRotation;
            public RaycastHit support;
            public float length;
        }

        private readonly Foot[] feet = new Foot[2];
        private readonly Vector3[] targets = new Vector3[2];
        private readonly Quaternion[] rotations = new Quaternion[2];
        private readonly RaycastHit[] supports = new RaycastHit[2];
        private readonly bool[] hasSupport = new bool[2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Animator animator;
        private CharacterController controller;
        private Transform visual;
        private Vector3 originalVisualPosition,
            previousRoot;
        private Quaternion previousRootRotation;
        private float restSoleOffset,
            pelvisDrop;
        private bool hasReachedGround;
        private const float Clearance = .002f;

        public bool IsFootPlanted(int side) =>
            side >= 0 && side < 2 && feet[side] != null && feet[side].planted;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponentInParent<CharacterController>();
            visual = transform.parent;
            originalVisualPosition = visual.localPosition;
            previousRoot = controller.transform.position;
            previousRootRotation = controller.transform.rotation;
            var upperIds = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg };
            var lowerIds = new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
            var footIds = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            for (int side = 0; side < 2; side++)
            {
                string prefix = side == 0 ? "Left_" : "Right_";
                var foot = new Foot
                {
                    upper = animator.GetBoneTransform(upperIds[side]),
                    lower = animator.GetBoneTransform(lowerIds[side]),
                    bone = animator.GetBoneTransform(footIds[side]),
                    toes = animator.GetBoneTransform(
                        side == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes
                    ),
                    surface = new SkinnedVertexSamples(
                        animator,
                        name => name.StartsWith(prefix + "Foot") || name.StartsWith(prefix + "Toes")
                    ),
                };
                foot.length =
                    Vector3.Distance(foot.upper.position, foot.lower.position)
                    + Vector3.Distance(foot.lower.position, foot.bone.position);
                feet[side] = foot;
            }
            restSoleOffset =
                Mathf.Min(feet[0].surface.BoundsIn().min.y, feet[1].surface.BoundsIn().min.y)
                - controller.transform.position.y;
        }

        private void LateUpdate()
        {
            var root = controller.transform;
            bool teleported =
                Vector3.Distance(root.position, previousRoot) > .4f
                || Quaternion.Angle(root.rotation, previousRootRotation) > 45f;
            previousRoot = root.position;
            previousRootRotation = root.rotation;
            hasReachedGround |= controller.isGrounded;
            visual.localPosition =
                originalVisualPosition
                + visual.parent.InverseTransformVector(
                    Vector3.down * (restSoleOffset + (hasReachedGround ? controller.skinWidth : 0f))
                );
            bool idle = animator.GetFloat("Speed") < .05f;
            for (int side = 0; side < 2; side++)
                PlanFoot(side, idle, teleported);
            if (!controller.isGrounded)
            {
                pelvisDrop = 0;
                return;
            }

            // 한 발이 아래 디딤면에 남아 있을 때 골반을 내려 다리 길이 안에서 접지를 유지한다.
            float neededDrop = 0;
            for (int side = 0; side < 2; side++)
            {
                if (!hasSupport[side])
                    continue;
                var foot = feet[side];
                float horizontal = Vector3
                    .ProjectOnPlane(foot.upper.position - targets[side], Vector3.up)
                    .magnitude;
                float reach = foot.length * .995f;
                float vertical = Mathf.Sqrt(Mathf.Max(0, reach * reach - horizontal * horizontal));
                neededDrop = Mathf.Max(
                    neededDrop,
                    foot.upper.position.y - targets[side].y - vertical
                );
            }
            neededDrop = Mathf.Clamp(neededDrop, 0, .16f);
            pelvisDrop =
                neededDrop > pelvisDrop
                    ? neededDrop
                    : Mathf.MoveTowards(pelvisDrop, neededDrop, Time.deltaTime * .8f);
            visual.position += Vector3.down * pelvisDrop;
            for (int side = 0; side < 2; side++)
                SolveFoot(side);
        }

        private void PlanFoot(int side, bool idle, bool teleported)
        {
            var foot = feet[side];
            var root = controller.transform;
            foot.length =
                Vector3.Distance(foot.upper.position, foot.lower.position)
                + Vector3.Distance(foot.lower.position, foot.bone.position);
            Vector3 local = root.InverseTransformPoint(foot.bone.position);
            float velocity = foot.hasPrevious
                ? (local.z - foot.previousLocal.z) / Mathf.Max(Time.deltaTime, .0001f)
                : 0;
            foot.previousLocal = local;
            foot.hasPrevious = true;
            bool released = false;
            if (foot.planted)
            {
                float horizontal = Vector3
                    .ProjectOnPlane(foot.upper.position - foot.anchor, Vector3.up)
                    .magnitude;
                if (
                    teleported
                    || !controller.isGrounded
                    || (
                        !idle
                        && (
                            velocity > .015f
                            || local.y > foot.animatedLocal.y + .045f
                            || horizontal > foot.length * .7f
                        )
                    )
                )
                {
                    foot.planted = false;
                    released = true;
                }
            }
            hasSupport[side] = FootGround(foot, out supports[side]);
            if (!controller.isGrounded || !hasSupport[side])
            {
                foot.planted = false;
                return;
            }
            var hit = supports[side];
            targets[side] = foot.bone.position;
            rotations[side] = foot.bone.rotation;
            float gap = foot.surface.MinimumDistance(hit.point, hit.normal);
            if (!foot.planted && !released && (idle || (velocity < -.015f && gap < .04f)))
            {
                foot.anchorRotation =
                    Quaternion.FromToRotation(
                        foot.bone.TransformDirection(soleUp[side]),
                        hit.normal
                    ) * foot.bone.rotation;
                foot.bone.rotation = foot.anchorRotation;
                gap = foot.surface.MinimumDistance(hit.point, hit.normal);
                foot.anchor = foot.bone.position + Vector3.up * ((Clearance - gap) / hit.normal.y);
                if (foot.toes != null)
                    foot.toeRotation = foot.toes.localRotation;
                foot.animatedLocal = local;
                foot.support = hit;
                foot.planted = true;
            }
            if (foot.planted)
            {
                targets[side] = foot.anchor;
                rotations[side] = foot.anchorRotation;
                supports[side] = foot.support;
            }
            else
            {
                // 앞꿈치가 다음 단 위에 걸쳐도 수직 면의 법선을 평균하지 않는다.
                float lift = Mathf.Max(0, (.008f - gap) / hit.normal.y);
                targets[side] += Vector3.up * lift;
            }
        }

        private void SolveFoot(int side)
        {
            if (!hasSupport[side])
                return;
            var foot = feet[side];
            var hit = supports[side];
            foot.bone.rotation = rotations[side];
            if (foot.planted && foot.toes != null)
                foot.toes.localRotation = foot.toeRotation;
            Vector3 bend = controller.transform.forward;
            TwoBonePose.Reach(foot.upper, foot.lower, foot.bone, targets[side], bend);
            for (int iteration = 0; iteration < 3; iteration++)
            {
                float distance = foot.surface.MinimumDistance(hit.point, hit.normal);
                float lift = (Clearance - distance) / hit.normal.y;
                if (lift < .0005f)
                    break;
                TwoBonePose.Reach(
                    foot.upper,
                    foot.lower,
                    foot.bone,
                    foot.bone.position + Vector3.up * lift,
                    bend
                );
            }
        }

        private bool FootGround(Foot foot, out RaycastHit ground)
        {
            var bounds = foot.surface.BoundsIn(controller.transform);
            var center = controller.transform.TransformPoint(bounds.center);
            bool found = Ground(center, out ground);
            for (int end = -1; end <= 1; end += 2)
                if (
                    Ground(
                        center + controller.transform.forward * (bounds.extents.z + .002f) * end,
                        out var candidate
                    ) && (!found || candidate.point.y > ground.point.y)
                )
                {
                    ground = candidate;
                    found = true;
                }
            return found;
        }

        private bool Ground(Vector3 point, out RaycastHit ground)
        {
            int count = Physics.RaycastNonAlloc(
                point + Vector3.up * .4f,
                Vector3.down,
                hits,
                .9f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            bool found = false;
            ground = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(controller.transform) || hit.normal.y < .55f)
                    continue;
                if (!found || hit.point.y > ground.point.y)
                {
                    ground = hit;
                    found = true;
                }
            }
            return found;
        }

        private void OnDisable()
        {
            if (visual != null)
                visual.localPosition = originalVisualPosition;
        }
    }
}
