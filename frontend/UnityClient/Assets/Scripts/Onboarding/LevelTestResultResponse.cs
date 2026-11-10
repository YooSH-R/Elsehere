namespace A305.Onboarding
{
    /// <summary>ONB-05~07 레벨 테스트를 끝낸 결과. 점수와 결과 레벨은 완료했을 때만 있다.</summary>
    public sealed class LevelTestResultResponse
    {
        public long TestId { get; set; }
        public LevelTestStatus Status { get; set; }

        /// <summary>맞힌 듣기 문항과 통과한 말하기 문항 수</summary>
        public int? Score { get; set; }

        /// <summary>추천 난이도. 초급(1) 또는 중급(2)</summary>
        public int? ResultLevel { get; set; }

        public int? ListeningScore { get; set; }
        public int? SpeakingPassed { get; set; }

        /// <summary>결과 화면의 한 줄 피드백. 말하기 답이 없으면 null이다.</summary>
        public string Feedback { get; set; }
    }
}
