using System.Collections.Generic;
using A305.World;

namespace A305.Npc
{
    /// <summary>
    /// 근무 시작 전에 일터 문으로 들어간 직원. 출근은 신호 대기 여유까지 넣어 일찍 나서므로 근무 전에 문에 닿는다.
    /// 마을의 출퇴근 모습이 문에 닿으면 기록하고, 일터 장면은 기록이 있으면 근무 시작 전에도 직원을 안에 둔다.
    /// 장면을 옮겨도 남도록 정적으로 두고, 게임 시계가 바뀌면(새 게임·로그인) 비운다.
    /// </summary>
    public static class NpcWorkArrivals
    {
        private static readonly Dictionary<string, double> ShiftStartsByNpc =
            new Dictionary<string, double>();
        private static GameClock observedClock;

        /// <param name="shiftStart">들어가 기다리는 근무의 시작 게임 시각(<see cref="NpcDayPlan.NextWorkStart"/>)</param>
        public static void MarkArrived(string npcCode, double shiftStart)
        {
            Synchronize();
            ShiftStartsByNpc[npcCode] = shiftStart;
        }

        public static bool HasArrived(string npcCode, double shiftStart)
        {
            Synchronize();
            return npcCode != null
                && ShiftStartsByNpc.TryGetValue(npcCode, out double arrivedFor)
                && arrivedFor == shiftStart;
        }

        /// <summary>근무 중이거나, 다가오는 근무를 위해 이미 일터에 들어와 있는지</summary>
        public static bool IsAtWork(NpcLifeEntry npc, NpcDayPlan plan, double minutes)
        {
            if (plan.Phase == NpcDayPhase.Working)
                return true;
            double? shiftStart = NpcDayPlan.NextWorkStart(npc, minutes);
            return shiftStart.HasValue && HasArrived(npc.NpcId, shiftStart.Value);
        }

        private static void Synchronize()
        {
            if (observedClock == GameClock.Shared)
                return;
            observedClock = GameClock.Shared;
            ShiftStartsByNpc.Clear();
        }
    }
}
