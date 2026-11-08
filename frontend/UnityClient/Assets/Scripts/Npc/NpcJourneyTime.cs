namespace A305.Npc
{
    /// <summary>기존 보행 속도와 게임 시각 배율로 이동에 필요한 시간을 계산한다.</summary>
    public static class NpcJourneyTime
    {
        public static double LeadMinutes(double meters, double signalSeconds, double marginMeters = 0) => 0;
        public static float WalkingProgress(double progress, double leadMinutes, double meters) => 0;
        public static bool CanVisit(double meters, double signalSeconds, double remainingMinutes, double marginMeters = 0) => false;
    }
}
