namespace A305.Onboarding
{
    /// <summary>ONB-09 대화 난이도 설정 결과. 처음 설정하면 튜토리얼 단계로 넘어간다.</summary>
    public sealed class LevelSelectionResponse
    {
        public string LanguageCode { get; set; }
        public int SelectedLevel { get; set; }
        public OnboardingStep OnboardingStep { get; set; }
    }
}
