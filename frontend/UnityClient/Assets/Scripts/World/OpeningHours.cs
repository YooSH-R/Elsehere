using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 게임 전체가 함께 보는 장소 영업시간. 장소 씬의 <see cref="OpeningHoursController"/>가 영업시간 파일을 넣고,
    /// 문은 들어가기 전에 지금 게임 시각에 열려 있는지 묻는다(<see cref="SpaceTravel"/>).
    /// 영업시간을 받기 전(영업시간 처리가 없는 Sandbox 씬)에는 모든 장소가 열려 있다.
    /// </summary>
    public static class OpeningHours
    {
        private const int MinutesPerDay = 24 * 60;

        public static PlaceHours Current { get; private set; } = PlaceHours.AlwaysOpen;

        public static void Use(PlaceHours placeHours)
        {
            Current = placeHours;
        }

        /// <summary>장소가 지금 게임 시각(<see cref="GameClock.Shared"/>)에 열려 있는지</summary>
        public static bool IsOpenNow(string placeCode)
        {
            int minuteOfDay = (int)(GameClock.Shared.Minutes % MinutesPerDay);
            return Current.IsOpen(placeCode, minuteOfDay);
        }

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 이전 영업시간을 지운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = PlaceHours.AlwaysOpen;
        }
    }
}
