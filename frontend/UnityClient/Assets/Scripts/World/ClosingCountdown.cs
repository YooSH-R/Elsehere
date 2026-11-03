using System;

namespace A305.World
{
    /// <summary>
    /// 플레이어가 있는 장소의 영업이 끝난 뒤 마을로 내보내기까지 남은 게임 시간을 센다.
    /// 닫힌 것을 처음 본 게임 시각부터 <see cref="GraceMinutes"/>분을 세고, 그 사이에 다시 열면 멈춘다.
    /// 게임 시간으로 세므로 대화·튜토리얼로 게임 시계가 멈춘 동안에는 줄지 않는다.
    /// </summary>
    public sealed class ClosingCountdown
    {
        /// <summary>영업이 끝난 뒤 장소에 머물 수 있는 게임 시간(분)</summary>
        public const double GraceMinutes = 30;

        // 닫힌 것을 처음 본 게임 시각(1일차 0시부터 센 분). 열려 있으면 null.
        private double? closedSince;

        public ClosingPhase Phase { get; private set; } = ClosingPhase.Open;

        /// <summary>마을로 옮기기까지 남은 게임 시간(분). 남은 조각 시간은 1분으로 올려 센다.</summary>
        public int MinutesLeft { get; private set; }

        /// <param name="isOpen">장소가 지금 열려 있는지</param>
        /// <param name="gameMinutes">지금 게임 시각(1일차 0시부터 센 분)</param>
        public void Update(bool isOpen, double gameMinutes)
        {
            if (isOpen)
            {
                Reset();
                return;
            }

            closedSince ??= gameMinutes;
            double elapsed = Math.Max(0, gameMinutes - closedSince.Value);
            MinutesLeft = (int)Math.Ceiling(Math.Max(0, GraceMinutes - elapsed));
            Phase = MinutesLeft > 0 ? ClosingPhase.CountingDown : ClosingPhase.TimeToLeave;
        }

        /// <summary>다른 장소로 옮겼을 때처럼 센 것을 잊는다. 다음에 닫힌 것을 보면 처음부터 센다.</summary>
        public void Reset()
        {
            closedSince = null;
            MinutesLeft = 0;
            Phase = ClosingPhase.Open;
        }
    }
}
