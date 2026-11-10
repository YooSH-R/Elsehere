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
        // 행인의 가게 들르기(홀수 시 정각부터 한 시간): 가게 45분, 제자리로 돌아오기 15분
        public const double ErrandStrollMinutes = 60;
        public const double ErrandVisitMinutes = 45;
        public const double ErrandReturnMinutes = 15;

        public NpcDayPlan(
            NpcDayPhase phase,
            double progress = 0,
            bool shortBreak = false,
            bool errand = false
        )
        {
            Phase = phase;
            Progress = progress;
            IsShortBreak = shortBreak;
            IsErrand = errand;
        }

        public NpcDayPhase Phase { get; }
        public double Progress { get; }
        public bool IsShortBreak { get; }

        /// <summary>행인이 자유 시간 중에 가게에 들렀다 제자리로 돌아오는 중인지(<see cref="NpcDayPhase.Leisure"/>·<see cref="NpcDayPhase.GoingToWork"/>)</summary>
        public bool IsErrand { get; }
        public bool IsInTown => Phase != NpcDayPhase.Working && Phase != NpcDayPhase.Resting;

        public static NpcDayPlan At(
            NpcLifeEntry npc,
            double minutes,
            bool requiredForMission,
            double commuteMinutes = 60
        )
        {
            bool passerby = npc.Role == "PASSERBY";
            string active = passerby ? "FREE" : "WORK";
            if (requiredForMission)
                return new NpcDayPlan(NpcDayPhase.Working);
            if (npc.ActivityAt(minutes) == active)
                return passerby ? FreeTime(npc, minutes) : new NpcDayPlan(NpcDayPhase.Working);

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
            double leadMinutes = Math.Min(gap, Math.Max(1, commuteMinutes));
            if (untilWork <= leadMinutes)
                return new NpcDayPlan(NpcDayPhase.GoingToWork, 1 - untilWork / leadMinutes);
            if (sinceWork < 30)
                return new NpcDayPlan(NpcDayPhase.LeavingWork, sinceWork / 30);
            if (sinceWork < 120)
                return new NpcDayPlan(NpcDayPhase.Leisure, (sinceWork - 30) / 90);
            if (sinceWork < 150)
                return new NpcDayPlan(NpcDayPhase.GoingHome, (sinceWork - 120) / 30);
            return new NpcDayPlan(NpcDayPhase.Resting);
        }

        // 행인은 자유 시간 중 홀수 시 정각부터 한 시간 동안 가게에 들렀다가 끝 15분에 제자리로 돌아오고,
        // 나머지 시간은 제자리 근처를 걷는다. 자유 시간이 시작하고 첫 한 시간은 걷고, 자유 시간 안에 끝나지 않으면 들르지 않는다.
        private static NpcDayPlan FreeTime(NpcLifeEntry npc, double minutes)
        {
            double dayMinute = (minutes % 1440 + 1440) % 1440;
            int hour = (int)(dayMinute / 60);
            double intoErrand = dayMinute - hour * 60;
            NpcScheduleBlock block = npc.Schedule.Find(candidate => candidate.Covers(dayMinute));
            double length = (block.EndMinute - block.StartMinute + 1440) % 1440;
            if (length == 0)
                length = 1440;
            double errandStart = (dayMinute - block.StartMinute + 1440) % 1440 - intoErrand;
            if (
                hour % 2 == 0
                || errandStart < ErrandStrollMinutes
                || errandStart + ErrandVisitMinutes + ErrandReturnMinutes > length
            )
                return new NpcDayPlan(NpcDayPhase.Working);
            return intoErrand < ErrandVisitMinutes
                ? new NpcDayPlan(NpcDayPhase.Leisure, intoErrand / ErrandVisitMinutes, errand: true)
                : new NpcDayPlan(
                    NpcDayPhase.GoingToWork,
                    (intoErrand - ErrandVisitMinutes) / ErrandReturnMinutes,
                    errand: true
                );
        }
    }
}
