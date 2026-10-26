using System.Collections.Generic;

namespace A305.Onboarding
{
    /// <summary>AUTH-04 가입 정보.</summary>
    public sealed class SignupRequest
    {
        public string Nickname { get; set; }
        public AvatarType AvatarType { get; set; }
        public List<ConsentAgreement> Consents { get; set; }
    }
}
