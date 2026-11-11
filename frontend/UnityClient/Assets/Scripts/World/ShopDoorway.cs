using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>마을 안의 매장 문. 화면이 덮인 동안 문턱 반대편의 같은 층으로 이동한다.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShopDoorway : MonoBehaviour
    {
        private BoxCollider threshold;
        private Transform approachPoint;

        [SerializeField]
        private bool insideOnForwardSide;

        [SerializeField]
        [Tooltip("이 문으로 들어가면 불러오는 실내. 1층과 2층이 다른 가게면 층마다 다르다.")]
        private ShopInterior room;

        public ShopInterior Room => room;

        /// <summary>길 안내가 끝나는 문 밖 지점. 문턱 감지 영역에 들어가기 전에 도착한다.</summary>
        public Transform ApproachPoint
        {
            get
            {
                if (approachPoint == null)
                {
                    threshold = GetComponent<BoxCollider>();
                    Vector3 outward = insideOnForwardSide ? -transform.forward : transform.forward;
                    float halfDepth = threshold.size.z * Mathf.Abs(transform.lossyScale.z) / 2f;
                    approachPoint = new GameObject("GuideApproachPoint").transform;
                    approachPoint.SetParent(transform, false);
                    approachPoint.SetPositionAndRotation(
                        ThresholdCenter() + outward * (halfDepth + 1f),
                        Quaternion.LookRotation(outward)
                    );
                }
                return approachPoint;
            }
        }

        /// <summary>대화 노트의 [다시 연습]처럼 목적지를 골라 방문할 때도 일반 출입문 전환을 사용한다.</summary>
        public bool TryEnter(PlayerMover player)
        {
            if (!isActiveAndEnabled)
                return false;
            Vector3 inward = insideOnForwardSide ? transform.forward : -transform.forward;
            var capsule = player.GetComponent<CharacterController>();
            SpaceTravel.CrossShopDoor(player, PointBeyondThreshold(inward, capsule), room, true);
            return SpaceTravel.IsTraveling;
        }

        private void Awake()
        {
            threshold = GetComponent<BoxCollider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (
                !isActiveAndEnabled
                || !(other is CharacterController capsule)
                || !capsule.TryGetComponent(out PlayerMover player)
            )
            {
                return;
            }

            Vector3 normal = transform.forward;
            float entrySide = Mathf.Sign(
                Vector3.Dot(player.transform.position - ThresholdCenter(), normal)
            );
            Vector3 destination = PointBeyondThreshold(-entrySide * normal, capsule);
            bool entering = (entrySide < 0f) == insideOnForwardSide;
            SpaceTravel.CrossShopDoor(player, destination, room, entering);
        }

        /// <summary>영업이 끝난 매장에서 문턱 바깥쪽 같은 층에 밖을 보고 세운다.</summary>
        internal bool TryLeave(PlayerMover player, ShopInterior interior)
        {
            Vector3 outward = insideOnForwardSide ? -transform.forward : transform.forward;
            var capsule = player.GetComponent<CharacterController>();
            var outside = new Pose(
                PointBeyondThreshold(outward, capsule),
                Quaternion.LookRotation(outward)
            );
            return SpaceTravel.TryLeaveClosedShop(
                player,
                outside,
                interior,
                outside.rotation.eulerAngles.y
            );
        }

        private Vector3 ThresholdCenter()
        {
            Vector3 center = transform.TransformPoint(threshold.center);
            center.y = transform.position.y;
            return center;
        }

        // 캡슐 전체가 감지 영역을 벗어나야 도착 직후 같은 문에 다시 들어가지 않는다.
        private Vector3 PointBeyondThreshold(Vector3 direction, CharacterController capsule)
        {
            float halfDepth = threshold.size.z * Mathf.Abs(transform.lossyScale.z) / 2f;
            float clearance = capsule.bounds.extents.x + capsule.skinWidth + 0.15f;
            return ThresholdCenter() + direction * (halfDepth + clearance);
        }
    }
}
