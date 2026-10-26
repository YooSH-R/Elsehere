namespace A305.Onboarding
{
    /// <summary>ONB-03 시작·ONB-07 건너뛰기 요청. 어느 학습 언어의 테스트인지 담는다.</summary>
    public sealed class LevelTestLanguageRequest
    {
        public string LanguageCode { get; set; }
    }
}
