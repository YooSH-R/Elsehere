namespace A305.Tutorial
{
    /// <summary>
    /// 걷기·둘러보기처럼 해내면 끝나는 단계는 [다음으로]를 누르지 않아도 완료 표시를 잠깐 보인 뒤 저절로 넘어간다.
    /// 할 일이 없는 도착 인사는 [시작하기]를 눌러야 넘어간다.
    /// </summary>
    public sealed class TutorialAutoAdvance
    {
        /// <summary>완료 표시를 보이는 시간</summary>
        public const float DoneSeconds = 1f;

        private bool isCounting;
        private TutorialStep countingStep;
        private float doneSeconds;

        /// <returns>이번 프레임에 다음 단계로 넘길지</returns>
        public bool Tick(TutorialStep step, bool canContinue, float deltaSeconds)
        {
            if (!canContinue || step == TutorialStep.Arrival)
            {
                isCounting = false;
                return false;
            }

            if (!isCounting || step != countingStep)
            {
                isCounting = true;
                countingStep = step;
                doneSeconds = 0f;
            }

            doneSeconds += deltaSeconds;
            return doneSeconds >= DoneSeconds;
        }
    }
}
