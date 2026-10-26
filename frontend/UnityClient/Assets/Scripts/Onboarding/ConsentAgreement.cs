namespace A305.Onboarding
{
    /// <summary>AUTH-04에 보내는 약관 동의. 서버가 보여 준 버전을 그대로 보낸다.</summary>
    public sealed class ConsentAgreement
    {
        public string ConsentType { get; set; }
        public string Version { get; set; }
    }
}
