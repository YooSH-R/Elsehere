namespace A305.Onboarding
{
    /// <summary>AUTH-03 약관 하나. 문서가 없는 항목(만 14세 확인)은 <see cref="Url"/>이 null이다.</summary>
    public sealed class ConsentTerm
    {
        public string ConsentType { get; set; }
        public string Version { get; set; }
        public bool Required { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
    }
}
