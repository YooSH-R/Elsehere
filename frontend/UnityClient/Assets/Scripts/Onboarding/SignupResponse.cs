namespace A305.Onboarding
{
    /// <summary>AUTH-04 가입 결과. 가입하면 같은 세션이 로그인 상태가 된다.</summary>
    public sealed class SignupResponse
    {
        public OnboardingUser User { get; set; }
    }
}
