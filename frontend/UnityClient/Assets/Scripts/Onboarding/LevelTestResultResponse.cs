namespace A305.Onboarding
{
    /// <summary>ONB-05~07 레벨 테스트를 끝낸 결과. 점수와 결과 레벨은 완료했을 때만 있다.</summary>
    public sealed class LevelTestResultResponse
    {
        public long TestId { get; set; }
        public LevelTestStatus Status { get; set; }
        public int? Score { get; set; }
        public int? ResultLevel { get; set; }
    }
}
