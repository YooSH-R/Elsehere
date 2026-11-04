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
        /// <returns>이동을 시작했으면 true. 잠겨 있거나, 이미 이동 중이거나, 씬을 불러올 수 없으면 false.</returns>
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
            if (
                RefuseIfLocked()
                || !state.TryBegin(destinationScene, arrivalDoorwayId, viewYawFromTravel)
            )
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError($"{destinationScene} 씬이 Scene List에 없어 이동하지 못했습니다.");
                state.Cancel();
                return false;
            }

            SpaceTravelCurtain.Travel(destinationScene);
            return true;
        }

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

        private static bool RefuseIfLocked()
        {
            if (!state.IsLocked)
            {
                return false;
            }

            Refused?.Invoke();
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
