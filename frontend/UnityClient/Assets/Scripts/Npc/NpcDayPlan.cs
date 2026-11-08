using System;

namespace A305.Npc
{
    public enum NpcDayPhase
    {
        Working,
        GoingToWork,
        LeavingWork,
        Leisure,
        GoingHome,
        Resting,
    }

    /// <summary>교대에서 출퇴근·산책·숙소 휴식을 계산한다. 같은 시각이면 씬을 다시 열어도 같은 상태다.</summary>
    public readonly struct NpcDayPlan
    {
        public NpcDayPlan(NpcDayPhase phase, double progress = 0, bool shortBreak = false)
        {
            Phase = phase;
            Progress = progress;
            IsShortBreak = shortBreak;
        }

        public NpcDayPhase Phase { get; }
        public double Progress { get; }
        public bool IsShortBreak { get; }
        public bool IsInTown => Phase != NpcDayPhase.Working && Phase != NpcDayPhase.Resting;

        public static NpcDayPlan At(NpcLifeEntry npc, double minutes, bool requiredForMission, double commuteMinutes = 60)
        {
            string active = npc.Role == "PASSERBY" ? "FREE" : "WORK";
            if (requiredForMission || npc.ActivityAt(minutes) == active)
                return new NpcDayPlan(NpcDayPhase.Working);

            double sinceWork = 1440;
            double untilWork = 1440;
            double dayMinute = (minutes % 1440 + 1440) % 1440;
            foreach (NpcScheduleBlock block in npc.Schedule)
            {
                if (block.Activity != active)
                    continue;
                sinceWork = Math.Min(sinceWork, (dayMinute - block.EndMinute + 1440) % 1440);
                untilWork = Math.Min(untilWork, (block.StartMinute - dayMinute + 1440) % 1440);
            }
            // 짧은 휴식은 근무지 앞에서 쉰다. 숙소까지 갔다가 곧바로 나오지 않는다.
            double gap = sinceWork + untilWork;
            if (gap <= 120)
            {
                double half = Math.Max(1, gap / 2);
                return sinceWork < half
                    ? new NpcDayPlan(NpcDayPhase.LeavingWork, sinceWork / half, true)
                    : new NpcDayPlan(NpcDayPhase.GoingToWork, 1 - untilWork / half, true);
            }
            if (untilWork <= 60)
                return new NpcDayPlan(NpcDayPhase.GoingToWork, 1 - untilWork / 60);
            if (sinceWork < 30)
                return new NpcDayPlan(NpcDayPhase.LeavingWork, sinceWork / 30);
            if (sinceWork < 120)
                return new NpcDayPlan(NpcDayPhase.Leisure, (sinceWork - 30) / 90);
            if (sinceWork < 150)
                return new NpcDayPlan(NpcDayPhase.GoingHome, (sinceWork - 120) / 30);
            return new NpcDayPlan(NpcDayPhase.Resting);
        }
    }
}
