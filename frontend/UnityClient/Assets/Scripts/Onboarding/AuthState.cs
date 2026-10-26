using System.Runtime.Serialization;

namespace A305.Onboarding
{
    /// <summary>BOOT-01 로그인 상태. 가입 대기는 Google 로그인만 마치고 가입 정보를 내지 않은 상태다.</summary>
    public enum AuthState
    {
        [EnumMember(Value = "SIGNED_IN")]
        SignedIn,

        [EnumMember(Value = "SIGNUP_REQUIRED")]
        SignupRequired,
    }
}
