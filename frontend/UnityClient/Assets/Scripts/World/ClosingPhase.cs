namespace A305.World
{
    /// <summary><see cref="ClosingCountdown"/>이 본 장소의 영업 마감 단계</summary>
    public enum ClosingPhase
    {
        /// <summary>장소가 열려 있다.</summary>
        Open,

        /// <summary>영업이 끝나 마을로 옮기기까지 남은 시간을 센다.</summary>
        CountingDown,

        /// <summary>남은 시간이 다 지나 마을로 옮길 때다.</summary>
        TimeToLeave,
    }
}
