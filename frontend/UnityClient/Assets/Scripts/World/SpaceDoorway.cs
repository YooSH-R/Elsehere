using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 다른 공간으로 이어지는 출입구. 플레이어가 감지 영역에 들어오면 연결된 공간으로 이동하고,
    /// 다른 공간에서 이 출입구로 들어오면 <see cref="ArrivalPoint"/>에 선다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SpaceDoorway : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이 공간 안에서 출입구를 구분하는 이름. 이어지는 공간 이름 + Door (예: CafeDoor)")]
        private string doorwayId;

        [SerializeField]
        [Tooltip("이동할 씬 이름. ERD scenes.unity_key와 같은 값")]
        private string destinationScene;

        [SerializeField]
        [Tooltip("이동한 공간에서 나타날 출입구 이름")]
        private string destinationDoorwayId;

        [SerializeField]
        [Tooltip("이 출입구로 들어온 플레이어가 설 위치와 방향. 감지 영역 밖에 둔다.")]
        private Transform arrivalPoint;

        public string DoorwayId => doorwayId;

        public string DestinationScene => destinationScene;

        public string DestinationDoorwayId => destinationDoorwayId;

        public Transform ArrivalPoint => arrivalPoint;

        private void Awake()
        {
            if (!IsConfigured())
            {
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 물리 메시지는 꺼진 컴포넌트에도 전달되므로 직접 확인한다.
            if (!enabled || !other.TryGetComponent(out PlayerMover player))
            {
                return;
            }

            if (SpaceTravel.TryTravel(destinationScene, destinationDoorwayId))
            {
                // 다음 공간을 불러오는 동안 문 밖으로 걸어 나가지 않게 한다.
                player.SetMovementLocked(true);
            }
        }

        private bool IsConfigured()
        {
            bool isConfigured = true;

            if (string.IsNullOrEmpty(doorwayId) || string.IsNullOrEmpty(destinationScene))
            {
                Debug.LogError("SpaceDoorway의 출입구 이름이나 이동할 씬이 비어 있습니다.", this);
                isConfigured = false;
            }

            if (arrivalPoint == null)
            {
                Debug.LogError(
                    $"SpaceDoorway {doorwayId}의 arrivalPoint가 연결되지 않았습니다.",
                    this
                );
                isConfigured = false;
            }

            if (!GetComponent<Collider>().isTrigger)
            {
                Debug.LogError($"SpaceDoorway {doorwayId}의 콜라이더가 Trigger가 아닙니다.", this);
                isConfigured = false;
            }

            return isConfigured;
        }
    }
}
