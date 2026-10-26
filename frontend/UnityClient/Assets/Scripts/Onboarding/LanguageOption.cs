namespace A305.Onboarding
{
    /// <summary>ONB-01 학습 언어 카드. 고를 수 없으면 "준비 중"으로 보인다.</summary>
    public sealed class LanguageOption
    {
        public string LanguageCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Available { get; set; }
    }
}
