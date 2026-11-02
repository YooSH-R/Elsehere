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

        /// <param name="destinationScene">이동할 씬 이름. Build Profile의 Scene List에 있어야 한다.</param>
        /// <param name="arrivalDoorwayId">도착할 출입구. 비우면 씬에 놓인 플레이어 위치에서 시작한다.</param>
        /// <returns>이동을 시작했으면 true. 이미 이동 중이거나 씬을 불러올 수 없으면 false.</returns>
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
            if (!state.TryBegin(destinationScene, arrivalDoorwayId, viewYawFromTravel))
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
