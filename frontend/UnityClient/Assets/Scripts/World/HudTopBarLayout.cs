namespace A305.World
{
    /// <summary>서로 다른 HUD 캔버스가 공유하는 오른쪽 상단 배치. 단위는 기준 캔버스 px다.</summary>
    public static class HudTopBarLayout
    {
        public const float Margin = 33.6f;
        public const float Size = 76.8f;
        public const float Gap = 14.4f;
        public const float PhoneRight = Margin + Size + Gap;
        public const float SurveyRight = PhoneRight + Size + Gap;

        public static float StatusRight(bool surveyVisible) =>
            surveyVisible ? SurveyRight + Size + Gap : SurveyRight;
    }
}
