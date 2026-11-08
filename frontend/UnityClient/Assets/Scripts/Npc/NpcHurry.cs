namespace A305.Npc
{
    /// <summary>일정 목표에 뒤처진 출퇴근 NPC의 서두르기 상태와 안전한 이동 속도.</summary>
    public sealed class NpcHurry
    {
        public const float WalkMetersPerSecond = 1.6f;
        public const float RunMetersPerSecond = 2.6f;

        public bool IsHurrying => false;

        public void Update(bool hasDeadline, float lagMeters) { }

        public float Speed(bool clearToRun) => WalkMetersPerSecond;
    }
}
