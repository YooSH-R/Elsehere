using System;
using A305.Player;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 씬을 바꿔 다른 공간으로 이동한다. 씬이 바뀌어도 도착할 출입구가 남도록 정적으로 둔다.
    /// 씬은 <see cref="SpaceTravelCurtain"/>이 화면을 덮은 채로 바꾸고,
    /// 새 씬의 <see cref="SpaceArrival"/>가 도착할 출입구를 받아 플레이어를 옮긴다.
    /// </summary>
    public static class SpaceTravel
    {
        private static SpaceTravelState state = new SpaceTravelState();

        public static bool IsTraveling => state.IsTraveling;

        /// <summary>다른 공간으로 가지 못하게 잠갔는지. 튜토리얼이 단계마다 정한다.</summary>
        public static bool IsLocked => state.IsLocked;

        /// <summary>잠겨 있어 이동을 거절했을 때 알린다. 잠근 쪽이 무엇을 먼저 할지 안내한다.</summary>
        public static event Action Refused;

        /// <summary>
        /// 영업시간이 아닌 장소에 들어가려다 거절했을 때 안내 문구("카페는 지금 영업시간이 아니에요 · 07:00–21:00")와 함께 알린다.
        /// </summary>
        public static event Action<string> ClosedPlaceRefused;

        /// <summary>문·[홈]으로 다른 공간에 가지 못하게 잠그거나 푼다.</summary>
        public static void SetLocked(bool locked)
        {
            if (locked)
            {
                state.Lock();
            }
            else
            {
                state.Unlock();
            }
        }

        /// <param name="destinationScene">이동할 씬 이름. Build Profile의 Scene List에 있어야 한다.</param>
        /// <param name="arrivalDoorwayId">도착할 출입구. 비우면 씬에 놓인 플레이어 위치에서 시작한다.</param>
        /// <returns>
        /// 이동을 시작했으면 true. 잠겨 있거나, 가려는 장소가 영업시간이 아니거나, 이미 이동 중이거나, 씬을 불러올 수 없으면 false.
        /// </returns>
        public static bool TryTravel(string destinationScene, string arrivalDoorwayId)
        {
            return TryTravel(destinationScene, arrivalDoorwayId, 0f);
        }

        /// <param name="viewYawFromTravel">문으로 들어가는 방향에서 카메라가 돌아가 있던 각도. 도착 후 같은 각도로 맞춘다.</param>
        public static bool TryTravel(
            string destinationScene,
            string arrivalDoorwayId,
            float viewYawFromTravel
        )
        {
            string placeCode = PlaceScenes.CodeOf(destinationScene);
            if (
                RefuseIfLocked()
                || RefuseIfClosed(placeCode, PlaceScenes.NameOf(placeCode))
                || !state.TryBegin(destinationScene, arrivalDoorwayId, viewYawFromTravel)
            )
            {
                return false;
            }

            return LoadDestination(destinationScene);
        }

        /// <summary>
        /// 영업이 끝난 장소에서 마을로 내보낸다. 튜토리얼 잠금과 상관없이 옮긴다. 마을은 늘 열려 있어 영업시간은 보지 않는다.
        /// </summary>
        /// <param name="exitToTown">이 장소에서 마을로 이어지는 출입구</param>
        /// <returns>이동을 시작했으면 true. 이미 이동 중이거나 씬을 불러올 수 없으면 false.</returns>
        public static bool TryLeaveClosedPlace(SpaceDoorway exitToTown)
        {
            if (
                !state.TryBegin(
                    exitToTown.DestinationScene,
                    exitToTown.DestinationDoorwayId,
                    evenIfLocked: true
                )
            )
            {
                return false;
            }

            return LoadDestination(exitToTown.DestinationScene);
        }

        private static bool LoadDestination(string destinationScene)
        {
            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError($"{destinationScene} 씬이 Scene List에 없어 이동하지 못했습니다.");
                state.Cancel();
                return false;
            }

            SpaceTravelCurtain.Travel(destinationScene);
            return true;
        }

        /// <summary>
        /// 마을 매장 문을 지난다. 들어갈 때는 매장이 영업시간이 아니면 거절하고, 나올 때는 막지 않는다.
        /// </summary>
        internal static void CrossShopDoor(
            PlayerMover player,
            Vector3 destination,
            ShopInterior interior,
            bool entering,
            Quaternion? arrivalRotation = null,
            float? arrivalViewYaw = null
        )
        {
            if (
                interior != null
                && entering
                && !Application.CanStreamedLevelBeLoaded(interior.SceneName)
            )
            {
                Debug.LogError($"실내 씬을 불러올 수 없습니다: {interior.SceneName}", interior);
                return;
            }
            if (
                player.IsMovementLocked
                || RefuseIfLocked()
                || (
                    entering
                    && interior != null
                    && RefuseIfClosed(interior.PlaceCode, interior.PlaceName)
                )
                || SpaceTravelCurtain.IsCoveringScreen
                || !state.TryBegin()
            )
            {
                return;
            }

            SpaceTravelCurtain.CrossShopDoor(
                player,
                new Pose(destination, arrivalRotation ?? player.transform.rotation),
                interior,
                entering,
                arrivalViewYaw,
                () => state.CompleteArrival()
            );
        }

        /// <summary>
        /// 영업이 끝난 마을 매장에서 문 밖으로 내보낸다. 튜토리얼 잠금과 상관없이 옮긴다.
        /// </summary>
        /// <returns>내보내기 시작했으면 true. 대화 중처럼 플레이어가 움직일 수 없거나 이미 이동 중이면 false.</returns>
        internal static bool TryLeaveClosedShop(
            PlayerMover player,
            Pose outside,
            ShopInterior interior,
            float arrivalViewYaw
        )
        {
            if (
                player.IsMovementLocked
                || SpaceTravelCurtain.IsCoveringScreen
                || !state.TryBegin(evenIfLocked: true)
            )
            {
                return false;
            }

            SpaceTravelCurtain.CrossShopDoor(
                player,
                outside,
                interior,
                false,
                arrivalViewYaw,
                () => state.CompleteArrival()
            );
            return true;
        }

        /// <summary>복습 전에 저장한 같은 공간의 자리와 실내를 복원한다. 새로운 물건 구매나 미션 진행은 하지 않는다.</summary>
        public static bool TryRestoreReviewPosition(
            PlayerMover player,
            Pose destination,
            ShopInterior savedInterior,
            float viewYaw
        )
        {
            if (
                player == null
                || player.IsMovementLocked
                || SpaceTravelCurtain.IsCoveringScreen
                || !state.TryBegin()
            )
                return false;
            ShopInterior changedInterior = savedInterior ?? ShopInterior.Occupied;
            SpaceTravelCurtain.CrossShopDoor(
                player,
                destination,
                changedInterior,
                savedInterior != null,
                viewYaw,
                () => state.CompleteArrival()
            );
            return true;
        }

        private static bool RefuseIfLocked()
        {
            if (!state.IsLocked)
            {
                return false;
            }

            Refused?.Invoke();
            return true;
        }

        private static bool RefuseIfClosed(string placeCode, string placeName)
        {
            if (OpeningHours.IsOpenNow(placeCode))
            {
                return false;
            }

            ClosedPlaceRefused?.Invoke(
                OpeningHoursText.ClosedNotice(placeName, OpeningHours.Current.HoursText(placeCode))
            );
            return true;
        }

        /// <summary>새 공간에 도착했을 때 한 번 호출해 이동을 끝낸다.</summary>
        /// <param name="viewYawFromTravel">도착 출입구에서 나오는 방향 기준으로 카메라를 돌릴 각도</param>
        /// <returns>도착할 출입구. 이동 없이 씬을 열었으면 null.</returns>
        public static string TakeArrivalDoorwayId(out float viewYawFromTravel)
        {
            return state.CompleteArrival(out viewYawFromTravel);
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 이전 이동 상태를 지운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            state = new SpaceTravelState();
        }
    }
}
