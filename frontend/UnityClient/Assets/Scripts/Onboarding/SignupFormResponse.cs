using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>AUTH-03 가입 정보 입력에 필요한 이메일과 약관 목록.</summary>
    public sealed class SignupFormResponse
    {
        public string Email { get; set; }
        public List<ConsentTerm> Consents { get; set; }
    }
}
