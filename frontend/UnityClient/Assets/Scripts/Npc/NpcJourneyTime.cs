using System;
using A305.World;

namespace A305.Npc
{
    /// <summary>기존 보행 속도와 게임 시각 배율로 이동에 필요한 시간을 계산한다.</summary>
    public static class NpcJourneyTime
    {
        public static double LeadMinutes(
            double meters,
            double signalSeconds,
            double marginMeters = 0
        ) =>
            Math.Max(
                1,
                Math.Ceiling(
                    ((meters + marginMeters) / NpcHurry.WalkMetersPerSecond + signalSeconds)
                        * GameClock.GameMinutesPerRealSecond
                )
            );

        public static float WalkingProgress(double progress, double leadMinutes, double meters)
        {
            double walkingMinutes =
                meters / NpcHurry.WalkMetersPerSecond * GameClock.GameMinutesPerRealSecond;
            return walkingMinutes <= 0
                ? 1
                : (float)Math.Min(1, Math.Max(0, progress * leadMinutes / walkingMinutes));
        }

        public static bool CanVisit(
            double meters,
            double signalSeconds,
            double remainingMinutes,
            double marginMeters = 0
        ) =>
            LeadMinutes(meters, signalSeconds, marginMeters)
                + NpcRoutine.LookAroundMinSeconds * GameClock.GameMinutesPerRealSecond
            <= remainingMinutes;
    }
}
