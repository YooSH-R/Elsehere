namespace A305.World
{
    /// <summary>서로 다른 HUD 캔버스가 공유하는 오른쪽 상단 배치. 단위는 기준 캔버스 px다.</summary>
    public static class HudTopBarLayout
    {
        public const float Margin = 33.6f;
        public const float Size = 76.8f;
        public const float Gap = 14.4f;
        public const float PhoneWidth = Size * 153f / 165f;
        public const float SurveyWidth = Size * 71f / 106f;

        // 휴대폰이 맨 오른쪽이고, 설문 아이콘과 시계·코인 칩이 그 왼쪽으로 이어진다.
        public const float PhoneRight = Margin;
        public const float SurveyRight = PhoneRight + PhoneWidth + Gap;

        public static float StatusRight(bool surveyVisible) =>
            surveyVisible ? SurveyRight + SurveyWidth + Gap : SurveyRight;
    }
}
