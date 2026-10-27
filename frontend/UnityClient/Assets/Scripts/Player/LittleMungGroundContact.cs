using UnityEngine;

namespace A305.Player
{
    /// <summary>리틀 멍 사본의 충돌 여유 높이와 발 밑면을 지면에 맞춘다.</summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Animator))]
    public sealed class LittleMungGroundContact : MonoBehaviour
    {
        private Animator animator;
        private CharacterController controller;
        private Transform visual;
        private Vector3 originalVisualPosition;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Transform[] upper = new Transform[2];
        private readonly Transform[] lower = new Transform[2];
        private readonly Transform[] feet = new Transform[2];
        private SkinnedVertexSamples[] soles;
        private float restSoleOffset;
        private bool hasReachedGround;
        private const float SurfaceClearance = 0.001f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            controller = GetComponentInParent<CharacterController>();
            visual = transform.parent;
            originalVisualPosition = visual.localPosition;
            upper[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            upper[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lower[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lower[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            feet[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            feet[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            soles = new[]
            {
                new SkinnedVertexSamples(
                    animator,
                    name => name.StartsWith("Left_Foot") || name.StartsWith("Left_Toes")
                ),
                new SkinnedVertexSamples(
                    animator,
                    name => name.StartsWith("Right_Foot") || name.StartsWith("Right_Toes")
                ),
            };
            restSoleOffset =
                Mathf.Min(soles[0].BoundsIn().min.y, soles[1].BoundsIn().min.y)
                - controller.transform.position.y;
        }

        private void LateUpdate()
        {
            // 생성 직후 첫 Move 전에는 skinWidth만큼 올라와 있지 않다.
            hasReachedGround |= controller.isGrounded;
            float margin = hasReachedGround ? controller.skinWidth : 0f;
            // 실제 스킨 밑면의 배치 오차와 충돌 여유 높이를 시각 모델에서만 제외한다.
            visual.localPosition =
                originalVisualPosition
                + visual.parent.InverseTransformVector(Vector3.down * (margin + restSoleOffset));
            if (!controller.isGrounded)
                return;
            bool idle = animator.GetFloat("Speed") < 0.05f;
            for (int side = 0; side < 2; side++)
            {
                if (!TryGround(feet[side].position, out float ground))
                    continue;
                Vector3 bend = lower[side].position - upper[side].position;
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    float sole = soles[side].BoundsIn().min.y;
                    float lift = ground + SurfaceClearance - sole;
                    if (!idle && lift <= 0)
                        break; // 발을 드는 원래 보행 구간은 유지한다.
                    if (Mathf.Abs(lift) < 0.0005f)
                        break;
                    var target =
                        feet[side].position + Vector3.up * Mathf.Clamp(lift, -0.08f, 0.12f);
                    TwoBonePose.Reach(upper[side], lower[side], feet[side], target, bend);
                }
            }
        }

        private bool TryGround(Vector3 foot, out float height)
        {
            int count = Physics.RaycastNonAlloc(
                foot + Vector3.up * 0.35f,
                Vector3.down,
                hits,
                0.8f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            height = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(controller.transform) || hit.normal.y < 0.5f)
                    continue;
                height = Mathf.Max(height, hit.point.y);
            }
            return !float.IsNegativeInfinity(height);
        }

        private void OnDisable()
        {
            if (visual != null)
                visual.localPosition = originalVisualPosition;
        }
    }
}
