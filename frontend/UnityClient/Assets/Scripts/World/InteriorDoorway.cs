using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 같은 씬 안에서 건물 실내와 문 밖을 잇는 출입구. 문은 닫아 둔 채, 플레이어가 감지 영역에 들어오면
    /// 다른 공간으로 갈 때처럼 화면을 덮고(<see cref="SpaceTravelCurtain"/>) 짝 출입구의 <see cref="ArrivalPoint"/>에 세운다.
    /// 씬을 바꾸지 않으므로 순찰차처럼 마을에서 이어지는 일은 그대로 진행된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InteriorDoorway : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "짝 출입구에서 이 출입구로 넘어온 플레이어가 설 위치와 방향. 감지 영역 밖에 둔다."
        )]
        private Transform arrivalPoint;

        [SerializeField]
        [Tooltip("이 출입구에 닿으면 플레이어가 나타날 짝 출입구")]
        private InteriorDoorway destination;

        public Transform ArrivalPoint => arrivalPoint;

        public InteriorDoorway Destination => destination;

        private void Awake()
        {
            if (arrivalPoint == null || destination == null)
            {
                Debug.LogError($"{name}의 InteriorDoorway 연결이 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (!GetComponent<Collider>().isTrigger)
            {
                Debug.LogError($"{name}의 InteriorDoorway 콜라이더가 Trigger가 아닙니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 물리 메시지는 꺼진 컴포넌트에도 전달되므로 직접 확인한다.
            // 대화 중이거나 이미 옮기는 중이면 이동이 잠겨 있으므로 다시 옮기지 않는다.
            if (
                !enabled
                || !other.TryGetComponent(out PlayerMover player)
                || player.IsMovementLocked
            )
            {
                return;
            }

            // 문으로 들어가는 방향(도착 방향의 반대) 기준으로 카메라가 돌아가 있던 각도를 짝 출입구에서도 이어 간다.
            // 누르던 키가 짝 출입구에서 멀어지는 쪽으로 걸어, 문을 다시 밟지 않는다.
            PlayerCameraRig cameraRig = player.CameraRig;
            float viewYawFromTravel =
                cameraRig != null
                    ? Mathf.DeltaAngle(arrivalPoint.eulerAngles.y + 180f, cameraRig.ViewYawDegrees)
                    : 0f;
            // 화면이 덮이는 동안 문 밖으로 걸어 나가지 않게 한다.
            var room = GetComponentInParent<ShopInterior>();
            if (room != null)
            {
                Transform arrival = destination.ArrivalPoint;
                bool entering = !LeadsOutOf(room);
                SpaceTravel.CrossShopDoor(
                    player,
                    arrival.position,
                    room,
                    entering,
                    arrival.rotation,
                    arrival.eulerAngles.y + viewYawFromTravel
                );
                return;
            }
            player.SetMovementLocked(true);
            SpaceTravelCurtain.CoverWhile(() =>
            {
                Transform arrival = destination.ArrivalPoint;
                player.Teleport(arrival.position, arrival.rotation);
                if (cameraRig != null)
                {
                    cameraRig.SnapView(arrival.eulerAngles.y + viewYawFromTravel);
                }

                player.SetMovementLocked(false);
            });
        }

        /// <summary>이 출입구가 건물 실내에서 문 밖으로 나가는 쪽인지. 실내는 건물의 뒤쪽(-z)에 있다.</summary>
        internal bool LeadsOutOf(ShopInterior room) =>
            room.transform.InverseTransformPoint(destination.ArrivalPoint.position).z >= 0f;

        /// <summary>영업이 끝난 건물에서 짝 출입구(문 밖)의 도착 지점으로 내보낸다.</summary>
        internal bool TryLeave(PlayerMover player, ShopInterior room)
        {
            Transform arrival = destination.ArrivalPoint;
            return SpaceTravel.TryLeaveClosedShop(
                player,
                new Pose(arrival.position, arrival.rotation),
                room,
                arrival.eulerAngles.y
            );
        }
    }
}
