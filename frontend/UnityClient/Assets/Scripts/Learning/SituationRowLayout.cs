namespace A305.Learning
{
    /// <summary>
    /// 피드백 표현 칸의 첫 줄. 영어 표현 뒤에 자리가 있으면 한국어 뜻을 같은 줄에 붙이고, 모자라면 다음 줄로 내린다.
    /// </summary>
    public static class SituationRowLayout
    {
        /// <summary>영어 표현과 뜻 사이 간격(시안 10px × 1.2)</summary>
        public const float Gap = 12;

        public static bool MeaningOnSameLine(
            float phraseWidth,
            float meaningWidth,
            float rowWidth
        ) => phraseWidth + Gap + meaningWidth <= rowWidth;
    }
}
