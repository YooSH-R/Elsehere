namespace A305.Onboarding
{
    /// <summary>CONV-02 녹음 전사 결과. 레벨 테스트 말하기 답을 받아 적을 때 쓴다.</summary>
    public sealed class SpeechTranscription
    {
        public string SttRawText { get; set; }
        public long DurationMs { get; set; }
    }
}
