using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 오른팔을 옆에서 굽혀 귀 바깥에 우산을 든다. 손목·손가락 자세는 걷기와 별도로 유지한다.
    /// 외형은 실행 중에 바뀌므로 펼 때 지금 외형의 팔 뼈대와 머리 크기를 읽는다. 플레이어 루트(CharacterController)에 붙인다.
    /// </summary>
    // 걷기 동작과 팔 겹침 보정(LittleMungArmClearance) 뒤에 손을 옮긴다.
    [DefaultExecutionOrder(650)]
    public sealed class UmbrellaHold : MonoBehaviour
    {
        [SerializeField]
        private HeldUmbrella umbrellaPrefab;

        [SerializeField]
        [Tooltip("기본 측면 거리 0.13m에 더할 손목의 바깥쪽 거리(m)")]
        private float gripBesideHead = .04f;

        [SerializeField]
        [Tooltip("귀 옆 머리 중심보다 손목을 앞에 둘 거리(m)")]
        private float gripForward = .06f;

        [SerializeField]
        [Tooltip("손목을 내리는 거리의 기준(m). 팔꿈치를 굽히기 위해 이 값의 40%를 적용합니다.")]
        private float gripBelowShoulder = .05f;

        [SerializeField]
        [Tooltip("머리 꼭대기와 캐노피 아랫면 사이 여유(m)")]
        private float headClearance = .06f;

        [SerializeField]
        [Tooltip("손잡이 위 연결부를 오른쪽 귀 바깥으로 기울이는 최소 각도")]
        private float leanDegrees = 14f;

        private HeldUmbrella umbrella;
        private Animator animator;
        private Transform upperArm;
        private Transform lowerArm;
        private Transform hand;
        private Bounds head;
        private SkinnedVertexSamples headSurface;
        private Transform middle;
        private Quaternion palmFrame;
        private Vector3 gripInHand;
        private readonly List<(Transform Bone, Quaternion Rotation, float Curl)> fingers = new();

        public bool IsOpen { get; private set; }

        /// <summary>펴 든 우산 모델. 처음 펴기 전에는 null이다.</summary>
        public HeldUmbrella Umbrella => umbrella;

        public Transform Hand => hand;

        public void SetOpen(bool open)
        {
            if (open && !Prepare())
                return;
            if (!open && IsOpen)
                foreach (var finger in fingers)
                    if (finger.Bone != null)
                        finger.Bone.localRotation = finger.Rotation;
            IsOpen = open;
            if (umbrella != null)
                umbrella.gameObject.SetActive(open);
        }

        // 지금 외형의 오른팔과 머리 높이를 읽고, 처음이면 우산을 만든다.
        private bool Prepare()
        {
            Animator current = GetComponentInChildren<Animator>();
            if (current == null || umbrellaPrefab == null)
                return false;
            if (current != animator)
            {
                animator = current;
                upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                PrepareGrip();
                head = HeadBounds();
            }

            if (upperArm == null || lowerArm == null || hand == null)
                return false;
            if (umbrella == null)
                umbrella = Instantiate(umbrellaPrefab, transform);
            return true;
        }

        private void PrepareGrip()
        {
            fingers.Clear();
            if (hand == null || middle == null)
                return;
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform pinky = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Vector3 along = hand.InverseTransformPoint(middle.position);
            Vector3 across = hand.InverseTransformDirection(index.position - pinky.position);
            palmFrame = Quaternion.LookRotation(along, across);
            gripInHand = along * .75f + Vector3.Cross(along, across).normalized * .015f;
            foreach (string finger in new[] { "Index", "Middle", "Ring", "Little" })
            {
                string[] joints = { "Proximal", "Intermediate", "Distal" };
                float[] angles = { 40, 60, 25 };
                for (int i = 0; i < joints.Length; i++)
                {
                    var id = (HumanBodyBones)
                        System.Enum.Parse(typeof(HumanBodyBones), "Right" + finger + joints[i]);
                    Transform bone = animator.GetBoneTransform(id);
                    if (bone != null)
                        fingers.Add((bone, bone.localRotation, angles[i]));
                }
            }
        }

        // 머리뼈 아래 정점으로 잰 머리 크기(플레이어 기준). 렌더러 경계는 넉넉하게 잡혀 있어 쓰지 않는다.
        private Bounds HeadBounds()
        {
            var names = new HashSet<string>();
            foreach (
                Transform bone in animator
                    .GetBoneTransform(HumanBodyBones.Head)
                    .GetComponentsInChildren<Transform>()
            )
                names.Add(bone.name);
            headSurface = new SkinnedVertexSamples(animator, names.Contains, outermostOnly: true);
            return headSurface.BoundsIn(transform);
        }

        private void LateUpdate()
        {
            if (!IsOpen || hand == null)
                return;
            head = headSurface.BoundsIn(transform);
            Vector3 shoulder = transform.InverseTransformPoint(upperArm.position);
            Vector3 wrist = transform.TransformPoint(
                new Vector3(
                    shoulder.x + .13f + gripBesideHead,
                    shoulder.y - gripBelowShoulder * .4f,
                    head.center.z + gripForward
                )
            );
            TwoBonePose.Reach(
                upperArm,
                lowerArm,
                hand,
                wrist,
                -transform.up + transform.right * .5f
            );

            // 짧은 팔은 귀까지 닿지 않는다. 손잡이 위 연결부만 머리 오른쪽 접평면 밖으로 기울인다.
            Quaternion tilt = transform.rotation;
            Vector3 holdAt = hand.position;
            for (float degrees = Mathf.Clamp(leanDegrees, 0, 60); degrees <= 60; degrees += 2)
            {
                tilt = Quaternion.AngleAxis(-degrees, transform.forward) * transform.rotation;
                if (middle != null)
                    hand.rotation =
                        Quaternion.LookRotation(tilt * Vector3.right, tilt * Vector3.up)
                        * Quaternion.Inverse(palmFrame);
                holdAt = middle != null ? hand.TransformPoint(gripInHand) : hand.position;
                if (headSurface.MinimumDistance(holdAt, tilt * Vector3.left) >= .025f)
                    break;
            }
            if (middle != null)
            {
                foreach (var finger in fingers)
                {
                    finger.Bone.localRotation = finger.Rotation;
                    finger.Bone.rotation =
                        Quaternion.AngleAxis(-finger.Curl, tilt * Vector3.up)
                        * finger.Bone.rotation;
                }
            }
            umbrella.transform.SetPositionAndRotation(holdAt, tilt);
            Vector3 localGrip = transform.InverseTransformPoint(holdAt);
            Vector3 stemDirection = transform.InverseTransformDirection(tilt * Vector3.up);
            float side = Mathf.Max(head.max.x + .03f, localGrip.x + .03f);
            Vector3 bend =
                localGrip
                + stemDirection * ((side - localGrip.x) / Mathf.Max(.01f, stemDirection.x));
            Vector3 center = new Vector3(side, head.max.y + headClearance + .06f, localGrip.z);
            float radius = .84f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                Vector2 edge = new Vector2(
                    head.center.x + head.extents.x * Mathf.Cos(angle),
                    head.center.z + head.extents.z * Mathf.Sin(angle)
                );
                radius = Mathf.Max(
                    radius,
                    Vector2.Distance(edge, new Vector2(center.x, center.z)) + headClearance
                );
            }
            umbrella.Fit(
                transform.TransformPoint(bend),
                transform.TransformPoint(center),
                transform.rotation,
                radius
            );
        }
    }
}
