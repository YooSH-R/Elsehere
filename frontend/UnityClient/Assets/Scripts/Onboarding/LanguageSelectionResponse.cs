namespace A305.Onboarding
{
    /// <summary>ONB-02 학습 언어 선택 결과.</summary>
    public sealed class LanguageSelectionResponse
    {
        public string LanguageCode { get; set; }
        public OnboardingStep OnboardingStep { get; set; }
    }
}
