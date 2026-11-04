using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>마을 안의 매장 문. 화면이 덮인 동안 문턱 반대편의 같은 층으로 이동한다.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShopDoorway : MonoBehaviour
    {
        private BoxCollider threshold;

        [SerializeField]
        private bool insideOnForwardSide;

        [SerializeField]
        [Tooltip("이 문으로 들어가면 불러오는 실내. 1층과 2층이 다른 가게면 층마다 다르다.")]
        private ShopInterior room;

        public ShopInterior Room => room;

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

            Vector3 center = transform.TransformPoint(threshold.center);
            center.y = transform.position.y;
            Vector3 normal = transform.forward;
            float entrySide = Mathf.Sign(Vector3.Dot(player.transform.position - center, normal));

            // 캡슐 전체가 감지 영역을 벗어나야 도착 직후 같은 문에 다시 들어가지 않는다.
            float halfDepth = threshold.size.z * Mathf.Abs(transform.lossyScale.z) / 2f;
            float clearance = capsule.bounds.extents.x + capsule.skinWidth + 0.15f;
            Vector3 destination = center - entrySide * normal * (halfDepth + clearance);
            bool entering = (entrySide < 0f) == insideOnForwardSide;
            SpaceTravel.CrossShopDoor(player, destination, room, entering);
        }
    }
}
