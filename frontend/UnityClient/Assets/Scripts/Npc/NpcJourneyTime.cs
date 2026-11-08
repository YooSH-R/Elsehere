using System;
using System.Collections.Generic;
using A305.Dialogue;
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

    /// <summary>같은 플레이 세션에서 거리 모습이 실내 직원에게 완료한 보행을 넘긴다.</summary>
    internal static class NpcJourneyLog
    {
        private static GameClock observedClock;
        private static readonly Dictionary<
            string,
            (NpcTravelContext Context, double Minute)
        > Completed = new();

        private static void Synchronize()
        {
            if (observedClock == GameClock.Shared)
                return;
            Completed.Clear();
            observedClock = GameClock.Shared;
        }

        internal static void Record(string code, NpcTravelContext context)
        {
            Synchronize();
            Completed[code] = (context.Copy(), GameClock.Shared.Minutes);
        }

        internal static NpcTravelContext Find(string code)
        {
            Synchronize();
            if (code == null || !Completed.TryGetValue(code, out var saved))
                return null;
            double age = GameClock.Shared.Minutes - saved.Minute;
            return age >= 0 && age < 1440 ? saved.Context.Copy() : null;
        }
    }
}
