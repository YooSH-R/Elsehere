namespace A305.Npc
{
    /// <summary>
    /// 시연 햄버거 가게 입장 순서. 입장하고 <see cref="RapDelaySeconds"/>초 뒤에 존박의 랩을 시작한다.
    /// 입장부터 존박의 대화 제안에 답할 때까지(거절·무시해 쉬거나 대화를 시작할 때까지) 장소 생활소음을 끈다.
    /// </summary>
    public sealed class ShowcaseWelcomeSequence
    {
        /// <summary>장면 전환 화면이 늦게 걷혀도 랩 앞부분을 놓치지 않게 입장 뒤 기다리는 시간. 전환 화면을 따로 기다리지 않는다.</summary>
        public const float RapDelaySeconds = 1.5f;

        private float waitedSeconds;

        /// <summary>랩을 시작했는지. 첫 걸음은 랩을 시작한 자리부터 센다.</summary>
        public bool RapStarted { get; private set; }

        /// <summary>장소 생활소음을 꺼 둘지</summary>
        public bool HushesPlace { get; private set; } = true;

        /// <summary>입장 뒤 시간을 센다. 랩을 시작할 때가 되면 한 번만 true를 돌려준다.</summary>
        public bool TickRap(float seconds)
        {
            if (RapStarted)
                return false;
            waitedSeconds += seconds;
            if (waitedSeconds < RapDelaySeconds)
                return false;
            RapStarted = true;
            return true;
        }

        /// <summary>존박의 다가가기·제안 상태를 본다. 거절·무시해 쉬거나 대화를 시작하면 생활소음을 다시 낸다.</summary>
        public void SeeOffer(PasserbyPhase phase)
        {
            if (phase == PasserbyPhase.Resting || phase == PasserbyPhase.Talking)
                HushesPlace = false;
        }
    }
}
