namespace A305.Onboarding
{
    /// <summary>USER-02·03 선택 동의 하나와 지금 동의했는지. 음성 처리(VOICE)와 AI 개선(TRAINING_*) 동의다.</summary>
    public sealed class ConsentStatus
    {
        public string ConsentType { get; set; }
        public string Version { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public bool Agreed { get; set; }
    }
}
