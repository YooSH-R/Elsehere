using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 오른손은 몸 앞에 두고 곧은 우산을 뒤쪽으로 기울여 든다. 손목·손가락 자세는 걷기와 별도로 유지한다.
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
        [Tooltip("머리 표면과 기울어진 캐노피 아랫면 사이 여유(m)")]
        private float headClearance = .06f;

        [SerializeField]
        [Tooltip("뒤로 기울이는 각도(35~45도). 뒷머리 안쪽 겹침을 허용하고 팔 자세를 유지합니다.")]
        private float leanDegrees = 35f;

        private HeldUmbrella umbrella;
        private Animator animator;
        private Transform upperArm;
        private Transform lowerArm;
        private Transform hand;
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
                PrepareHeadSurface();
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

        // 기울어진 우산 천을 머리 위에 두기 위해 실제 머리 정점을 모은다.
        private void PrepareHeadSurface()
        {
            var names = new HashSet<string>();
            foreach (
                Transform bone in animator
                    .GetBoneTransform(HumanBodyBones.Head)
                    .GetComponentsInChildren<Transform>()
            )
                names.Add(bone.name);
            headSurface = new SkinnedVertexSamples(animator, names.Contains, outermostOnly: true);
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

            // 뒷머리를 완전히 피하려고 팔을 비틀지 않는다. 손은 앞에 두고 우산만 뒤로 기울인다.
            // 기존 씬에 남은 작은 기울기 값에도 같은 자세가 적용되도록 범위를 제한한다.
            Quaternion tilt =
                Quaternion.AngleAxis(Mathf.Clamp(leanDegrees, 35, 45), -transform.right)
                * transform.rotation;
            Vector3 fingersForward = transform.TransformDirection(
                new Vector3(.6427876f, 0, .7660444f)
            );
            if (middle != null)
                hand.rotation =
                    Quaternion.LookRotation(fingersForward, tilt * Vector3.up)
                    * Quaternion.Inverse(palmFrame);
            Vector3 holdAt = middle != null ? hand.TransformPoint(gripInHand) : hand.position;
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
            const float radius = .84f;
            // 천의 평면이 머리 전체보다 바깥에 오도록 우산대 방향으로 길이를 잰다.
            float length =
                -headSurface.MinimumDistance(holdAt, -(tilt * Vector3.up)) + headClearance;
            umbrella.Fit(length, radius);
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
