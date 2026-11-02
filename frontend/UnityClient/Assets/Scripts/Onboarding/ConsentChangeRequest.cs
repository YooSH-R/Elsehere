namespace A305.Onboarding
{
    /// <summary>USER-03 선택 동의 켜기·끄기 요청.</summary>
    public sealed class ConsentChangeRequest
    {
        public string ConsentType { get; set; }
        public bool Agreed { get; set; }
    }
}
