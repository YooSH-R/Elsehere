using System.Collections.Generic;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 플레이어가 우산을 펴 든다. 오른팔을 몸 옆 어깨 높이로 뻗어 손잡이를 쥐게 하고, 우산대를 세워 캐노피가 머리 위를 덮게 한다.
    /// 외형은 실행 중에 바뀌므로 펼 때 지금 외형의 팔 뼈대와 머리 크기를 읽는다. 플레이어 루트(CharacterController)에 붙인다.
    /// </summary>
    // 걷기 동작과 팔 겹침 보정(LittleMungArmClearance) 뒤에 손을 옮긴다.
    [DefaultExecutionOrder(650)]
    public sealed class UmbrellaHold : MonoBehaviour
    {
        [SerializeField]
        private HeldUmbrella umbrellaPrefab;

        [SerializeField]
        [Tooltip(
            "손 목표를 머리 옆면에서 바깥으로 얼마나 둘지(m). 팔이 짧으면 닿는 데까지만 뻗는다"
        )]
        private float gripBesideHead = .04f;

        [SerializeField]
        [Tooltip("손잡이를 머리 중심보다 얼마나 앞에 쥘지(m)")]
        private float gripForward = .06f;

        [SerializeField]
        [Tooltip("손잡이를 어깨보다 얼마나 낮게 쥘지(m)")]
        private float gripBelowShoulder = .05f;

        [SerializeField]
        [Tooltip("머리 꼭대기와 캐노피 아랫면 사이 여유(m)")]
        private float headClearance = .06f;

        [SerializeField]
        [Tooltip("우산대를 머리 쪽으로 기울이는 각도")]
        private float leanDegrees = 14f;

        private HeldUmbrella umbrella;
        private Animator animator;
        private Transform upperArm;
        private Transform lowerArm;
        private Transform hand;
        private Bounds head;

        public bool IsOpen { get; private set; }

        /// <summary>펴 든 우산 모델. 처음 펴기 전에는 null이다.</summary>
        public HeldUmbrella Umbrella => umbrella;

        public Transform Hand => hand;

        public void SetOpen(bool open)
        {
            if (open && !Prepare())
                return;
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
                head = HeadBounds();
            }

            if (upperArm == null || lowerArm == null || hand == null)
                return false;
            if (umbrella == null)
                umbrella = Instantiate(umbrellaPrefab, transform);
            return true;
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
            return new SkinnedVertexSamples(animator, names.Contains).BoundsIn(transform);
        }

        private void LateUpdate()
        {
            if (!IsOpen || hand == null)
                return;
            float shoulderHeight = upperArm.position.y - transform.position.y;
            Vector3 grip = transform.TransformPoint(
                new Vector3(
                    head.max.x + gripBesideHead,
                    shoulderHeight - gripBelowShoulder,
                    head.center.z + gripForward
                )
            );
            TwoBonePose.Reach(upperArm, lowerArm, hand, grip, -transform.up + transform.forward);

            Vector3 holdAt = hand.position;
            Quaternion lean = Quaternion.AngleAxis(leanDegrees, transform.forward);
            umbrella.transform.SetPositionAndRotation(holdAt, lean * transform.rotation);
            umbrella.Fit(head.max.y + headClearance - (holdAt.y - transform.position.y));
        }
    }
}
