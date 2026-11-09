namespace A305.Npc
{
    /// <summary>일정 목표에 뒤처진 출퇴근 NPC의 서두르기 상태와 안전한 이동 속도.</summary>
    public sealed class NpcHurry
    {
        public const float WalkMetersPerSecond = 1.6f;
        public const float RunMetersPerSecond = 2.6f;

        public bool IsHurrying { get; private set; }

        public void Update(bool hasDeadline, float lagMeters)
        {
            if (!hasDeadline || lagMeters <= 1.5f)
                IsHurrying = false;
            else if (lagMeters >= 4)
                IsHurrying = true;
        }

        public float Speed(bool clearToRun) =>
            IsHurrying && clearToRun ? RunMetersPerSecond : WalkMetersPerSecond;
    }
}
