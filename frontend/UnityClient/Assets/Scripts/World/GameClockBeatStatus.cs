namespace A305.World
{
    /// <summary>게임 시간 신호 한 번의 결과.</summary>
    public enum GameClockBeatStatus
    {
        /// <summary>보낼 때가 아니거나 앞선 신호의 응답을 기다리는 중이라 보내지 않았다.</summary>
        NotDue,

        /// <summary>보내고 서버가 알려 준 게임 시각으로 바꿨다.</summary>
        Updated,

        /// <summary>보냈지만 실패했다. 마지막으로 받은 게임 시각을 그대로 둔다.</summary>
        Failed,
    }
}
