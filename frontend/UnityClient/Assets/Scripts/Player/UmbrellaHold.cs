using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 오른팔을 옆에서 굽혀 귀 바깥에 우산을 든다. 손목·손가락 자세는 걷기와 별도로 유지한다.
    /// 외형은 실행 중에 바뀌므로 펼 때 지금 외형의 팔 뼈대와 머리 크기를 읽는다. 플레이어 루트(CharacterController)에 붙인다.
    /// </summary>
    // 걷기·팔 겹침 보정과 착석 자세(PlayerSeatPose) 뒤에 손과 우산을 함께 옮긴다.
    [DefaultExecutionOrder(750)]
    public sealed class UmbrellaHold : MonoBehaviour
    {
        [SerializeField]
        private HeldUmbrella umbrellaPrefab;

        [SerializeField]
        [Tooltip("기본 측면 거리 0.14m에 더할 손목의 바깥쪽 거리(m)")]
        private float gripBesideHead = .04f;

        [SerializeField]
        [Tooltip("어깨에서 기본 6cm에 더할 손목의 전방 거리(m)")]
        private float gripForward = .06f;

        [SerializeField]
        [Tooltip("기본 4cm에 더할 손목을 내리는 거리(m)")]
        private float gripBelowShoulder = .05f;

        [SerializeField]
        [Tooltip("머리 꼭대기와 캐노피 아랫면 사이 여유(m)")]
        private float headClearance = .06f;

        [SerializeField]
        [Tooltip("곡선 기둥이 시작되는 손잡이의 최소 기울기. 머리와 겹치면 더 기울입니다.")]
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
        private bool folded;

        /// <summary>펴 든 우산 모델. 처음 펴기 전에는 null이다.</summary>
        public HeldUmbrella Umbrella => umbrella;

        public Transform Hand => hand;

        public void SetFolded(bool carried)
        {
            if (carried && !Prepare())
                return;
            SetOpen(false);
            folded = carried;
            if (umbrella != null)
                umbrella.gameObject.SetActive(carried);
        }

        public void SetOpen(bool open)
        {
            if (open && !Prepare())
                return;
            if (!open && (IsOpen || folded))
                foreach (var finger in fingers)
                    if (finger.Bone != null)
                        finger.Bone.localRotation = finger.Rotation;
            IsOpen = open;
            folded = false;
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
            if (folded && hand != null)
            {
                HoldFolded();
                return;
            }
            if (!IsOpen || hand == null)
                return;
            head = headSurface.BoundsIn(transform);
            Vector3 shoulder = transform.InverseTransformPoint(upperArm.position);
            Vector3 wrist = transform.TransformPoint(
                new Vector3(
                    shoulder.x + .14f + gripBesideHead,
                    shoulder.y - .04f - gripBelowShoulder,
                    shoulder.z + .06f + gripForward
                )
            );
            TwoBonePose.Reach(
                upperArm,
                lowerArm,
                hand,
                wrist,
                -transform.up + transform.right * .5f
            );

            // 곡선의 시작 접선을 머리 바깥으로 둔다. 제어점도 이 평면 바깥에 두어 머리를 통과하지 않는다.
            Quaternion tilt = transform.rotation;
            Vector3 holdAt = hand.position;
            Vector3 outward = transform.right;
            Vector3 tiltAxis = Vector3.Cross(transform.up, outward);
            Vector3 fingersForward = transform.TransformDirection(
                new Vector3(.6427876f, 0, .7660444f)
            );
            for (float degrees = Mathf.Clamp(leanDegrees, 0, 60); degrees <= 60; degrees += 2)
            {
                tilt = Quaternion.AngleAxis(degrees, tiltAxis) * transform.rotation;
                if (middle != null)
                    hand.rotation =
                        Quaternion.LookRotation(fingersForward, tilt * Vector3.up)
                        * Quaternion.Inverse(palmFrame);
                holdAt = middle != null ? hand.TransformPoint(gripInHand) : hand.position;
                Vector3 inward = Vector3.ProjectOnPlane(-outward, tilt * Vector3.up).normalized;
                if (headSurface.MinimumDistance(holdAt, inward) >= .03f)
                    break;
            }
            if (middle != null)
            {
                foreach (var finger in fingers)
                {
                    finger.Bone.localRotation = finger.Rotation;
                    finger.Bone.rotation =
                        Quaternion.AngleAxis(
                            -finger.Curl,
                            Vector3.ProjectOnPlane(tilt * Vector3.up, fingersForward).normalized
                        ) * finger.Bone.rotation;
                }
            }
            umbrella.transform.SetPositionAndRotation(holdAt, tilt);
            Vector3 localGrip = transform.InverseTransformPoint(holdAt);
            Vector3 stemDirection = transform.InverseTransformDirection(tilt * Vector3.up);
            float roofHeight = head.max.y + headClearance + .06f;
            float sideX = head.max.x + .06f;
            Vector3 side = localGrip + stemDirection * ((sideX - localGrip.x) / stemDirection.x);
            side.z = head.center.z + .03f;
            Vector3 center = new Vector3(side.x, roofHeight, side.z);
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
                transform.TransformPoint(side),
                transform.TransformPoint(center),
                transform.rotation,
                radius
            );
        }

        // 접은 우산을 내린 손에 쥐고, 끝을 몸 바깥·앞으로 조금 기울여 다리와 겹치지 않게 한다.
        // 손 높이에 맞춰 길이를 줄여 끝이 바닥을 뚫지 않게 한다.
        private void HoldFolded()
        {
            Vector3 shoulder = transform.InverseTransformPoint(upperArm.position);
            Vector3 wrist = transform.TransformPoint(shoulder + new Vector3(.15f, -.4f, .1f));
            TwoBonePose.Reach(upperArm, lowerArm, hand, wrist, transform.forward + transform.right);
            Vector3 down = (
                -transform.up + transform.right * .35f + transform.forward * .25f
            ).normalized;
            Quaternion hang = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, down).normalized,
                down
            );
            hand.rotation = hang * Quaternion.Inverse(palmFrame);
            foreach (var finger in fingers)
            {
                finger.Bone.localRotation = finger.Rotation;
                finger.Bone.rotation =
                    Quaternion.AngleAxis(finger.Curl, transform.right) * finger.Bone.rotation;
            }
            Vector3 grip = middle != null ? hand.TransformPoint(gripInHand) : hand.position;
            float aboveFloor = transform.InverseTransformPoint(grip).y - .04f;
            float length = Mathf.Clamp(
                aboveFloor / Vector3.Dot(down, -transform.up),
                .2f,
                HeldUmbrella.FoldedLength
            );
            umbrella.transform.SetPositionAndRotation(grip, hang);
            umbrella.FitFolded(length);
        }
    }
}
