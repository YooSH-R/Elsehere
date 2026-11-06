using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>AUTH-03 가입 정보 입력에 필요한 이메일과 약관 목록.</summary>
    public sealed class SignupFormResponse
    {
        public string Email { get; set; }
        public List<ConsentTerm> Consents { get; set; }

        /// <summary>게스트가 가입하는 중이면 게스트 때 캐릭터와 음성 처리 동의. 아니면 null이다.</summary>
        public ConvertingGuest Guest { get; set; }
    }
}
