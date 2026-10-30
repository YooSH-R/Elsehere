namespace A305.World
{
    /// <summary>서버가 알려 준 사용자의 게임 시각(WORLD-06·07).</summary>
    public sealed class GameClockTime
    {
        /// <summary>게임 날짜. 1일차부터 센다.</summary>
        public int Day { get; set; }

        /// <summary>하루 중 분(0~1439). 480이면 오전 8시.</summary>
        public int MinuteOfDay { get; set; }

        /// <summary>진행 중인 대화가 있어 시간이 멈췄는지.</summary>
        public bool Paused { get; set; }
    }
}
