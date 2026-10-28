namespace A305.Onboarding
{
    /// <summary>BOOT-01 접속 상태. 가입 대기면 <see cref="User"/>가 없다.</summary>
    public sealed class BootstrapResponse
    {
        public AuthState AuthState { get; set; }
        public OnboardingUser User { get; set; }
        public string LastSceneCode { get; set; }
    }
}
